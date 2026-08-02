using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading.RateLimiting;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kpi.Config;
using Kpi.Export;
using Kpi.Export.Models;
using Kpi.Pipeline;
using Kpi.Views;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Authentication.OAuth.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.RateLimiting;
using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kpi.Server;

// API de l'onglet Options (projets/labels/milestones GitLab en direct, calcul du temps, sauvegarde de la config phases/équipes).
public sealed partial class WebDashboard
{
    // --- Édition de la config depuis le dashboard (ADMIN) -------------------------------------------------
    /// <summary>Serveur GitLab du périmètre courant (claim de session, repli sur le 1er configuré).</summary>
    private ServerConfig? CurrentServer(HttpContext ctx)
        => ServerById(ctx.User.FindFirst(ServerClaim)?.Value) ?? _config.ResolveServers().FirstOrDefault();

    // GET /api/options/projects → TOUS les projets accessibles au token de groupe STOCKÉ (+ flag imported).
    private async Task<IResult> OptionsProjectsAsync(HttpContext ctx)
    {
        var deny = RequireAdmin(ctx); if (deny != null) return deny;
        var server = CurrentServer(ctx);
        if (server == null || string.IsNullOrWhiteSpace(server.BaseUrl) || string.IsNullOrWhiteSpace(server.GroupToken)
            || !Uri.TryCreate(server.BaseUrl, UriKind.Absolute, out var baseUri))
            return Results.Json(new { ok = false, error = "Aucun serveur GitLab configuré." });
        var http = server.AllowSelfSignedCertificates ? _sharedHttpRelaxed : _sharedHttp;
        var imported = new HashSet<int>(_config.Export.ProjectIds ?? new());
        var projects = new List<object>();
        var seen = new HashSet<int>();
        for (int page = 1; page <= 10; page++)
        {
            var pj = await GlGet(http, baseUri, $"/api/v4/projects?membership=true&simple=true&per_page=100&page={page}&order_by=name&sort=asc", server.GroupToken, ctx.RequestAborted);
            if (pj is not JsonArray arr || arr.Count == 0) break;
            foreach (var p in arr)
            {
                if (p is not JsonObject po) continue;
                var id = po["id"] is JsonValue iv && iv.TryGetValue<int>(out var ii) ? ii : 0;
                if (id == 0 || !seen.Add(id)) continue;
                projects.Add(new
                {
                    id,
                    name = po["name"]?.GetValue<string>() ?? "",
                    group = po["namespace"]?["path"]?.GetValue<string>() ?? "",
                    groupFull = po["namespace"]?["full_path"]?.GetValue<string>() ?? "",
                    imported = imported.Contains(id),
                });
            }
            if (arr.Count < 100) break;
        }
        return Results.Json(new { ok = true, projects });
    }

    // GET /api/options/labels?projectIds=4,11 → labels (incl. ancêtres de groupe) des projets choisis.
    private async Task<IResult> OptionsLabelsAsync(HttpContext ctx)
    {
        var deny = RequireAdmin(ctx); if (deny != null) return deny;
        var server = CurrentServer(ctx);
        if (server == null || string.IsNullOrWhiteSpace(server.BaseUrl) || string.IsNullOrWhiteSpace(server.GroupToken)
            || !Uri.TryCreate(server.BaseUrl, UriKind.Absolute, out var baseUri))
            return Results.Json(new { ok = false, error = "Aucun serveur GitLab configuré." });
        var http = server.AllowSelfSignedCertificates ? _sharedHttpRelaxed : _sharedHttp;
        var set = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pid in (ctx.Request.Query["projectIds"].ToString() ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var enc = Uri.EscapeDataString(pid);
            for (int page = 1; page <= 5; page++)
            {
                var lj = await GlGet(http, baseUri, $"/api/v4/projects/{enc}/labels?per_page=100&page={page}&include_ancestor_groups=true&with_counts=false", server.GroupToken, ctx.RequestAborted);
                if (lj is not JsonArray arr || arr.Count == 0) break;
                foreach (var l in arr) { var n = l?["name"]?.GetValue<string>(); if (!string.IsNullOrWhiteSpace(n)) set.Add(n); }
                if (arr.Count < 100) break;
            }
        }
        return Results.Json(new { ok = true, labels = set });
    }

    // GET /api/options/milestones?projectIds=4,11 → milestones (triées, récentes d'abord) des projets
    // choisis, récupérées EN DIRECT sur GitLab. Indispensable AVANT la 1re extraction : le catalogue
    // local (availableMilestones du payload) est encore vide, or la régénération se cible par milestone.
    private async Task<IResult> OptionsMilestonesAsync(HttpContext ctx)
    {
        var deny = RequireAdmin(ctx); if (deny != null) return deny;
        var server = CurrentServer(ctx);
        if (server == null || string.IsNullOrWhiteSpace(server.BaseUrl) || string.IsNullOrWhiteSpace(server.GroupToken)
            || !Uri.TryCreate(server.BaseUrl, UriKind.Absolute, out var baseUri))
            return Results.Json(new { ok = false, error = "Aucun serveur GitLab configuré." });
        var http = server.AllowSelfSignedCertificates ? _sharedHttpRelaxed : _sharedHttp;
        // projectIds absent/vide → tous les projets configurés du serveur.
        var pids = (ctx.Request.Query["projectIds"].ToString() ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        if (pids.Count == 0) pids = (server.ProjectIds ?? new()).ToList();
        var items = new List<(string title, string date)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pid in pids)
        {
            var enc = Uri.EscapeDataString(pid);
            var mj = await GlGet(http, baseUri, $"/api/v4/projects/{enc}/milestones?per_page=100&state=all", server.GroupToken, ctx.RequestAborted);
            foreach (var m in mj?.AsArray() ?? new JsonArray())
            {
                var title = m?["title"]?.GetValue<string>() ?? "";
                if (string.IsNullOrWhiteSpace(title) || !seen.Add(title)) continue;
                items.Add((title, m?["due_date"]?.GetValue<string>() ?? m?["start_date"]?.GetValue<string>() ?? ""));
            }
        }
        var milestones = items
            .OrderByDescending(x => x.date, StringComparer.Ordinal)   // ISO yyyy-MM-dd → tri lexical OK
            .ThenByDescending(x => x.title, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.title).ToList();
        return Results.Json(new { ok = true, milestones });
    }

    // POST /api/options/worktime { workStartHour, workEndHour, workingDaysOnly, holidays:[], minPhaseMinutes }
    // → persiste la fenêtre de temps ouvré + anti-bruit (Options → Calcul du temps), hot-reload.
    private async Task<IResult> SaveWorkTimeAsync(HttpContext ctx)
    {
        var deny = RequireAdmin(ctx); if (deny != null) return deny;
        var b = await ReadJsonBody(ctx);
        var start = b?["workStartHour"]?.GetValue<int>() ?? 9;
        var end = b?["workEndHour"]?.GetValue<int>() ?? 19;
        var daysOnly = b?["workingDaysOnly"]?.GetValue<bool>() ?? true;
        var noise = b?["minPhaseMinutes"]?.GetValue<int>() ?? 0;
        if (start < 0 || start > 23 || end < 1 || end > 24 || end <= start)
            return Results.Json(new { ok = false, error = "Plage horaire invalide (début 0-23, fin 1-24, fin > début)." });
        if (noise < 0 || noise > 24 * 60)
            return Results.Json(new { ok = false, error = "Seuil anti-bruit invalide (0 à 1440 minutes)." });
        var holidays = new JsonArray();
        var seenH = new HashSet<string>();
        foreach (var h in (b?["holidays"] as JsonArray) ?? new JsonArray())
        {
            var s = (h?.GetValue<string>() ?? "").Trim();
            if (s.Length == 0) continue;
            if (!Regex.IsMatch(s, @"^\d{4}-\d{2}-\d{2}$") || !DateTime.TryParse(s, out _))
                return Results.Json(new { ok = false, error = $"Jour férié invalide : « {s} » (format aaaa-mm-jj)." });
            if (seenH.Add(s)) holidays.Add(s);
        }

        JsonObject root;
        try { root = (JsonNode.Parse(await File.ReadAllTextAsync(RuntimeConfigPath())) as JsonObject) ?? new JsonObject(); }
        catch { root = new JsonObject(); }
        var ex = root["Export"] as JsonObject ?? new JsonObject(); root["Export"] = ex;
        ex["WorkStartHour"] = start;
        ex["WorkEndHour"] = end;
        ex["WorkingDaysOnly"] = daysOnly;
        ex["Holidays"] = holidays;
        ex["MinPhaseMinutes"] = noise;
        var outText = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        try { await WriteFileAtomicAsync(RuntimeConfigPath(), outText); }
        catch (Exception e) { Console.Error.WriteLine("SaveWorkTime KO : " + e); return Results.Json(new { ok = false, error = "Écriture de la configuration impossible." }); }
        // Hot-reload : les prochains payloads embarquent la nouvelle fenêtre (recalcul côté client au rechargement).
        try { _config = BuildConfig(); _payloadCache.Clear(); }
        catch (Exception e) { Console.Error.WriteLine("SaveWorkTime reload KO : " + e); }
        return Results.Json(new { ok = true });
    }

    // Rôle d'une période à l'écriture (Piste 2) : lit `role` (active|wait|nogc) ; repli sur `timed` si un
    // vieux client ne l'envoie pas encore (timed:true → active, timed:false → nogc). Le `Timed` persisté
    // en est dérivé (Role != "nogc").
    private static string PeriodRole(JsonObject po)
    {
        static string? S(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
        var r = (S(po["role"]) ?? "").Trim().ToLowerInvariant();
        if (r == "active" || r == "wait" || r == "nogc") return r;
        var timed = po["timed"] is JsonValue tv && tv.TryGetValue<bool>(out var tb) ? tb : true;
        return timed ? "active" : "nogc";
    }

    // POST /api/options → sauvegarde des sections Export (projets/phases/labels/équipes, global + par projet) ET
    // Servers[<courant>].ProjectIds. PRÉSERVE GroupToken/BaseUrl/Auth (Teams écrites si transmises, sinon préservées).
    // Hot-reload ; refetch optionnel.
    // Réutilise la validation de SetupSaveAsync (clés de période uniques, hex strict, label→période croisée).
    private async Task<IResult> SaveOptionsAsync(HttpContext ctx)
    {
        var deny = RequireAdmin(ctx); if (deny != null) return deny;
        var b = await ReadJsonBody(ctx);
        static string? Str(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

        var projectIds = ((b?["projectIds"] as JsonArray) ?? new JsonArray())
            .Select(n => n is JsonValue v && v.TryGetValue<int>(out var i) ? i : 0).Where(i => i > 0).Distinct().ToList();
        if (projectIds.Count == 0) return Results.Json(new { ok = false, error = "Sélectionnez au moins un projet." });

        var periodsArr = new JsonArray();
        var validKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in (b?["periods"] as JsonArray) ?? new JsonArray())
        {
            if (p is not JsonObject po) continue;
            var key = (Str(po["key"]) ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(key) || key == "none" || !validKeys.Add(key)) continue;
            var name = (Str(po["name"]) ?? "").Trim(); if (name.Length == 0) name = key;
            var color = (Str(po["color"]) ?? "").Trim(); if (!Regex.IsMatch(color, "^#[0-9a-fA-F]{6}$")) color = "#cccccc";
            var role = PeriodRole(po);
            periodsArr.Add(new JsonObject { ["Key"] = key, ["Name"] = name, ["Color"] = color, ["Role"] = role, ["Timed"] = role != "nogc" });
        }
        var trackedLabels = new List<string>();
        var labelPhases = new Dictionary<string, string>();
        foreach (var kv in ((b?["labelPhases"] as JsonObject) ?? new JsonObject()))
        {
            var ph = Str(kv.Value) ?? "none";
            if (ph != "none" && validKeys.Count > 0 && !validKeys.Contains(ph)) ph = "none";
            labelPhases[kv.Key] = ph;
            if (ph != "none") trackedLabels.Add(kv.Key);
        }
        var projectsArr = new JsonArray();
        foreach (var p in (b?["projects"] as JsonArray) ?? new JsonArray())
        {
            if (p is not JsonObject po) continue;
            var pid = po["id"] is JsonValue piv && piv.TryGetValue<int>(out var pii) ? pii : 0; if (pid == 0) continue;
            projectsArr.Add(new JsonObject { ["Id"] = pid, ["Name"] = (Str(po["name"]) ?? "").Trim(), ["Group"] = (Str(po["group"]) ?? "").Trim() });
        }

        JsonObject root;
        try { root = (JsonNode.Parse(await File.ReadAllTextAsync(RuntimeConfigPath())) as JsonObject) ?? new JsonObject(); }
        catch { root = new JsonObject(); }

        // Serveur courant : on met à jour SES ProjectIds, sans toucher au token ni à l'URL.
        var serverId = CurrentServer(ctx)?.Id;
        if (root["Servers"] is JsonArray serversArr && serverId != null)
            foreach (var sNode in serversArr)
                if (sNode is JsonObject so && string.Equals(so["Id"]?.GetValue<string>(), serverId, StringComparison.OrdinalIgnoreCase))
                { so["ProjectIds"] = new JsonArray(projectIds.Select(i => JsonValue.Create(i.ToString())).ToArray()); break; }

        var ex = root["Export"] as JsonObject ?? new JsonObject(); root["Export"] = ex;
        ex["ProjectIds"] = new JsonArray(projectIds.Select(i => JsonValue.Create(i)).ToArray());
        ex["LabelPhases"] = JsonSerializer.SerializeToNode(labelPhases);
        ex["TrackedLabels"] = new JsonArray(trackedLabels.Select(s => JsonValue.Create(s)).ToArray());
        // Labels transversaux (noms exacts, dédoublonnés). Écrits seulement si le client les envoie.
        if (b?["transversalLabels"] is JsonArray tvArr)
        {
            var tv = new JsonArray(); var seenTv = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var n in tvArr) { var s = (Str(n) ?? "").Trim(); if (s.Length > 0 && seenTv.Add(s)) tv.Add(s); }
            ex["TransversalLabels"] = tv;
        }
        if (b?["projects"] is JsonArray) ex["Projects"] = projectsArr;
        if (b?["periods"] is JsonArray) ex["Periods"] = periodsArr;
        if (b?["periodsByProject"] is JsonObject pbp)
        {
            var outPbp = new JsonObject();
            foreach (var kv in pbp)
            {
                if (kv.Value is not JsonArray parr) continue;
                var arr = new JsonArray(); var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in parr)
                {
                    if (p is not JsonObject po) continue;
                    var key = (Str(po["key"]) ?? "").Trim().ToLowerInvariant();
                    if (string.IsNullOrEmpty(key) || key == "none" || !keys.Add(key)) continue;
                    var name = (Str(po["name"]) ?? "").Trim(); if (name.Length == 0) name = key;
                    var color = (Str(po["color"]) ?? "").Trim(); if (!Regex.IsMatch(color, "^#[0-9a-fA-F]{6}$")) color = "#cccccc";
                    var role = PeriodRole(po);
                    arr.Add(new JsonObject { ["Key"] = key, ["Name"] = name, ["Color"] = color, ["Role"] = role, ["Timed"] = role != "nogc" });
                }
                outPbp[kv.Key] = arr;
            }
            ex["PeriodsByProject"] = outPbp;
        }
        if (b?["labelPhasesByProject"] is JsonObject lbp)
        {
            var outLbp = new JsonObject();
            foreach (var kv in lbp)
            {
                if (kv.Value is not JsonObject m) continue;
                var mm = new JsonObject();
                foreach (var e in m) mm[e.Key] = (Str(e.Value) ?? "none");
                outLbp[kv.Key] = mm;
            }
            ex["LabelPhasesByProject"] = outLbp;
        }
        // Équipes éditées dans l'onglet Options : { name, members:[username] } — lead = 1er membre (pas de
        // champ « lead » en config). N'écrit QUE si le client transmet "teams" (sinon préserve l'existant) ;
        // TeamGroups (mapping équipe→groupe GitLab, posé au /setup) est laissé intact.
        if (b?["teams"] is JsonArray teamsArr)
        {
            var teams = new JsonObject();
            foreach (var t in teamsArr)
            {
                if (t is not JsonObject to) continue;
                var name = (Str(to["name"]) ?? "").Trim();
                if (name.Length == 0 || teams.ContainsKey(name)) continue;
                var arr = new JsonArray(); var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var m in (to["members"] as JsonArray) ?? new JsonArray())
                {
                    var u = (Str(m) ?? "").Trim();
                    if (u.Length > 0 && seen.Add(u)) arr.Add(JsonValue.Create(u));
                }
                teams[name] = arr;
            }
            ex["Teams"] = teams;
            // Rôles LEAD : chaque équipe (lead = 1er membre, ordering client via orderTeam) → compte 'group'
            // dans accounts.json, lu par ResolveAccount pour donner à ce membre le rôle lead (scope équipe).
            // Les autres membres retombent en scope individuel (auto-provision). Préserve comptes 'user' + vues.
            await WriteTeamLeadAccountsAsync(teams);
        }
        // Équipes PAR PROJET : { "projectId": [ {name, members:[username]} ] } → { name → members } par projet.
        // N'écrit QUE si transmis (sinon préserve l'existant).
        if (b?["teamsByProject"] is JsonObject tbp)
        {
            var outTbp = new JsonObject();
            foreach (var kv in tbp)
            {
                if (kv.Value is not JsonArray tarr) continue;
                var teamsObj = new JsonObject();
                foreach (var t in tarr)
                {
                    if (t is not JsonObject to) continue;
                    var name = (Str(to["name"]) ?? "").Trim();
                    if (name.Length == 0 || teamsObj.ContainsKey(name)) continue;
                    var arr = new JsonArray(); var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var m in (to["members"] as JsonArray) ?? new JsonArray())
                    { var u = (Str(m) ?? "").Trim(); if (u.Length > 0 && seen.Add(u)) arr.Add(JsonValue.Create(u)); }
                    teamsObj[name] = arr;
                }
                outTbp[kv.Key] = teamsObj;
            }
            ex["TeamsByProject"] = outTbp;
        }

        var outText = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        try
        {
            await WriteFileAtomicAsync(RuntimeConfigPath(), outText);
            var src = SourceConfigPath();
            if (src != null && !string.Equals(src, RuntimeConfigPath(), StringComparison.OrdinalIgnoreCase))
                await WriteFileAtomicAsync(src, outText);
        }
        catch (Exception e) { Console.Error.WriteLine("SaveOptions write KO : " + e); return Results.Json(new { ok = false, error = "Écriture de la configuration impossible." }); }

        try { _config = BuildConfig(); _memberCache.Clear(); _payloadCache.Clear(); }
        catch (Exception e) { Console.Error.WriteLine("SaveOptions reload KO : " + e); return Results.Json(new { ok = false, error = "Enregistré, mais rechargement échoué (redémarrez le serveur)." }); }

        var refetch = b?["refetch"] is JsonValue rv && rv.TryGetValue<bool>(out var rb) && rb;
        if (refetch) StartSetupFetch(ctx);
        return Results.Json(new { ok = true, refetch });
    }

    // POST /api/options/canny { apiKey, subdomain? } → connexion externe Canny (ADMIN). Valide la clé auprès
    // de l'API Canny AVANT de la chiffrer/persister (enc:v1:). Clé vide + clé existante = conservée (permet de
    // modifier le sous-domaine sans re-saisir). La clé n'est JAMAIS renvoyée au client. Hot-reload.
    // Génère/actualise output/accounts.json à partir des équipes : un compte 'group' par équipe ayant au
    // moins un membre (subject=équipe, leads=[1er membre]). Préserve les comptes non-'group' (user) + les vues.
    // C'est ce que ResolveAccount lit pour attribuer le rôle LEAD (scope équipe) au 1er membre de chaque équipe.
    private async Task WriteTeamLeadAccountsAsync(JsonObject teams)
    {
        try
        {
            var path = AccountsPath();
            JsonObject root;
            try { root = (JsonNode.Parse(await File.ReadAllTextAsync(path)) as JsonObject) ?? new JsonObject(); }
            catch { root = new JsonObject(); }

            var kept = new JsonArray();
            if (root["accounts"] is JsonArray existing)
                foreach (var a in existing)
                    if (a is JsonObject ao && (ao["type"]?.GetValue<string>() ?? "") != "group")
                        kept.Add(ao.DeepClone());

            foreach (var kv in teams)
            {
                if (kv.Value is not JsonArray members || members.Count == 0) continue;
                var lead = members[0]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(lead)) continue;
                kept.Add(new JsonObject
                {
                    ["type"] = "group",
                    ["subject"] = kv.Key,
                    ["username"] = kv.Key,
                    ["leads"] = new JsonArray(JsonValue.Create(lead)),
                    ["viewId"] = "",
                });
            }
            root["accounts"] = kept;
            root["views"] ??= new JsonArray();

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await WriteFileAtomicAsync(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception e) { Console.Error.WriteLine("WriteTeamLeadAccounts KO : " + e); }
    }

    private async Task<IResult> SaveCannyAsync(HttpContext ctx)
    {
        var deny = RequireAdmin(ctx); if (deny != null) return deny;
        return await WriteCannyConnectionAsync(ctx);
    }

    // Cœur PARTAGÉ (Options admin + étape « Connexions externes » du /setup) : valide la clé auprès de
    // l'API Canny, la chiffre (enc:v1:) et l'enregistre. La garde d'accès est faite par l'appelant.
    private async Task<IResult> WriteCannyConnectionAsync(HttpContext ctx)
    {
        var b = await ReadJsonBody(ctx);
        var apiKey = (b?["apiKey"]?.GetValue<string>() ?? "").Trim();
        var subdomain = (b?["subdomain"]?.GetValue<string>() ?? "").Trim();
        var hasExisting = _config.ExternalConnections?.Canny?.Configured ?? false;
        if (apiKey.Length == 0 && !hasExisting)
            return Results.Json(new { ok = false, error = "Clé API Canny requise." });

        // Valide la clé fournie auprès de l'API Canny (un simple boards/list) avant de l'enregistrer.
        if (apiKey.Length > 0)
        {
            try
            {
                using var client = new Kpi.Canny.CannyClient(new Kpi.Config.CannyConfig { ApiKey = apiKey, RequestTimeoutSeconds = 20 });
                await client.ListSimpleAsync<Kpi.Canny.CannyBoardRaw>("boards/list", "boards", ctx.RequestAborted);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("SaveCanny test KO : " + e.Message);
                return Results.Json(new { ok = false, error = "Connexion Canny refusée (clé API invalide ?)." });
            }
        }

        JsonObject root;
        try { root = (JsonNode.Parse(await File.ReadAllTextAsync(RuntimeConfigPath())) as JsonObject) ?? new JsonObject(); }
        catch { root = new JsonObject(); }
        var ext = root["ExternalConnections"] as JsonObject ?? new JsonObject(); root["ExternalConnections"] = ext;
        var canny = ext["Canny"] as JsonObject ?? new JsonObject(); ext["Canny"] = canny;
        // Clé fournie → chiffrée ; sinon on conserve la valeur déjà stockée (chiffrée) telle quelle.
        if (apiKey.Length > 0) canny["ApiKey"] = SecureStore.ProtectSecret(apiKey);
        if (subdomain.Length > 0) canny["Subdomain"] = subdomain;

        var outText = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        try
        {
            await WriteFileAtomicAsync(RuntimeConfigPath(), outText);
            var src = SourceConfigPath();
            if (src != null && !string.Equals(src, RuntimeConfigPath(), StringComparison.OrdinalIgnoreCase))
                await WriteFileAtomicAsync(src, outText);
        }
        catch (Exception e) { Console.Error.WriteLine("SaveCanny write KO : " + e); return Results.Json(new { ok = false, error = "Écriture de la configuration impossible." }); }

        try { _config = BuildConfig(); _payloadCache.Clear(); }
        catch (Exception e) { Console.Error.WriteLine("SaveCanny reload KO : " + e); return Results.Json(new { ok = false, error = "Enregistré, mais rechargement échoué (redémarrez le serveur)." }); }

        return Results.Json(new { ok = true, connected = _config.ExternalConnections?.Canny?.Configured ?? false });
    }

    // POST /api/setup/oauth { clientId, clientSecret, authority } → écrit Auth.ClientId/ClientSecret/Authority
    // dans appsettings.json, recharge la config, et INVALIDE le cache des options OAuth → reconfiguration À CHAUD
    // (le bouton SSO devient actif sans redémarrage). Le Secret n'est pas renvoyé au client.
}
