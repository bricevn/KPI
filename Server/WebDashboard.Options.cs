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

    // Ids d'onglets NATIFS (shell.jsx NAV_IDS + options) : une page modulaire ne peut pas les réutiliser
    // (sinon conflit de routage). Validé au save.
    private static readonly HashSet<string> ReservedPageIds = new(StringComparer.OrdinalIgnoreCase)
    { "dashboard", "charts", "anomalies", "issues", "calendar", "velocity", "comparison", "options" };

    // GET /api/pages → { ok, dashboard } (ADMIN) : la config des pages modulaires, pour l'éditeur.
    private IResult ServePages(HttpContext ctx)
    {
        var deny = RequireAdmin(ctx); if (deny != null) return deny;
        var d = _config.Dashboard ?? new DashboardConfig();
        return Results.Json(new
        {
            ok = true,
            dashboard = new
            {
                schemaVersion = d.SchemaVersion,
                defaultPageId = d.DefaultPageId ?? "",
                pages = (d.Pages ?? new()).Select(p => new
                {
                    id = p.Id, kind = p.Kind,
                    nav = new { label = p.Nav.Label, labelKey = p.Nav.LabelKey, icon = p.Nav.Icon, order = p.Nav.Order, showFilters = p.Nav.ShowFilters, badgeSource = p.Nav.BadgeSource },
                    layout = new { cols = p.Layout.Cols, gap = p.Layout.Gap, rowUnit = p.Layout.RowUnit },
                    widgets = (p.Widgets ?? new()).Select(w => new { id = w.Id, type = w.Type, data = w.Data, layout = new { w = w.Layout.W, h = w.Layout.H, x = w.Layout.X, y = w.Layout.Y }, @params = w.Params ?? new() }),
                }),
            },
        });
    }

    // POST /api/pages → écrit la section Dashboard (pages modulaires). ADMIN. Calqué sur SaveWorkTime :
    // n'écrit QUE la section Dashboard (préserve tout le reste), runtime + source, hot-reload.
    // Validation légère côté serveur (structure/ids/bornes) ; la validation profonde (type ∈ window.KPI,
    // data ∈ window.KPIData) est faite côté éditeur JS — le serveur ne peut pas exécuter le registre.
    private async Task<IResult> SavePagesAsync(HttpContext ctx)
    {
        var deny = RequireAdmin(ctx); if (deny != null) return deny;
        var b = await ReadJsonBody(ctx);
        static string? Str(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
        static int Int(JsonNode? n, int def) => n is JsonValue v && v.TryGetValue<int>(out var i) ? i : def;
        static bool Bool(JsonNode? n, bool def) => n is JsonValue v && v.TryGetValue<bool>(out var x) ? x : def;
        static string ParamVal(JsonNode? n) => n is JsonValue v ? (v.TryGetValue<string>(out var s) ? s : v.ToJsonString()) : "";

        var schemaVersion = Int(b?["schemaVersion"], 1);
        var defaultPageId = (Str(b?["defaultPageId"]) ?? "").Trim();
        var outPages = new JsonArray();
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pn in (b?["pages"] as JsonArray) ?? new JsonArray())
        {
            if (pn is not JsonObject po) continue;
            var id = (Str(po["id"]) ?? "").Trim().ToLowerInvariant();
            if (!Regex.IsMatch(id, @"^[a-z0-9][a-z0-9-]{0,63}$"))
                return Results.Json(new { ok = false, error = $"Id de page invalide : « {id} » (minuscules, chiffres, tirets)." });
            if (ReservedPageIds.Contains(id))
                return Results.Json(new { ok = false, error = $"Id de page réservé (conflit avec un onglet natif) : « {id} »." });
            if (!seenIds.Add(id))
                return Results.Json(new { ok = false, error = $"Id de page en double : « {id} »." });

            var navIn = po["nav"] as JsonObject ?? new JsonObject();
            var layIn = po["layout"] as JsonObject ?? new JsonObject();
            var cols = Math.Clamp(Int(layIn["cols"], 12), 1, 24);
            var nav = new JsonObject
            {
                ["Label"] = (Str(navIn["label"]) ?? "").Trim(),
                ["LabelKey"] = (Str(navIn["labelKey"]) ?? "").Trim(),
                ["Icon"] = (Str(navIn["icon"]) ?? "").Trim(),
                ["Order"] = Int(navIn["order"], 100),
                ["ShowFilters"] = Bool(navIn["showFilters"], true),
                ["BadgeSource"] = (Str(navIn["badgeSource"]) ?? "").Trim(),
            };
            var layout = new JsonObject
            {
                ["Cols"] = cols,
                ["Gap"] = (Str(layIn["gap"]) ?? "var(--space-4)").Trim(),
                ["RowUnit"] = Math.Clamp(Int(layIn["rowUnit"], 88), 24, 400),
            };

            var widgetsOut = new JsonArray();
            var seenW = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var wn in (po["widgets"] as JsonArray) ?? new JsonArray())
            {
                if (wn is not JsonObject wo) continue;
                var wid = (Str(wo["id"]) ?? "").Trim();
                if (wid.Length == 0) wid = "w" + (widgetsOut.Count + 1);
                if (!seenW.Add(wid))
                    return Results.Json(new { ok = false, error = $"Id de widget en double dans « {id} » : « {wid} »." });
                var type = (Str(wo["type"]) ?? "").Trim();
                if (!Regex.IsMatch(type, @"^[A-Za-z0-9_]{1,64}$"))
                    return Results.Json(new { ok = false, error = $"Type de widget invalide dans « {id} » : « {type} »." });
                var data = (Str(wo["data"]) ?? "").Trim();
                var wlIn = wo["layout"] as JsonObject ?? new JsonObject();
                var wl = new JsonObject
                {
                    ["W"] = Math.Clamp(Int(wlIn["w"], 4), 1, cols),
                    ["H"] = Math.Clamp(Int(wlIn["h"], 1), 1, 24),
                    ["X"] = Int(wlIn["x"], -1),
                    ["Y"] = Int(wlIn["y"], -1),
                };
                var paramsOut = new JsonObject();
                foreach (var kv in (wo["params"] as JsonObject) ?? new JsonObject())
                {
                    if (kv.Key.Contains(':'))
                        return Results.Json(new { ok = false, error = $"Clé de paramètre invalide (« : » interdit) : « {kv.Key} »." });
                    paramsOut[kv.Key] = ParamVal(kv.Value);
                }
                widgetsOut.Add(new JsonObject { ["Id"] = wid, ["Type"] = type, ["Data"] = data, ["Layout"] = wl, ["Params"] = paramsOut });
            }
            outPages.Add(new JsonObject { ["Id"] = id, ["Kind"] = "modular", ["Nav"] = nav, ["Layout"] = layout, ["Widgets"] = widgetsOut });
        }
        if (defaultPageId.Length > 0 && !seenIds.Contains(defaultPageId))
            return Results.Json(new { ok = false, error = $"defaultPageId inconnu : « {defaultPageId} »." });

        var dashObj = new JsonObject { ["SchemaVersion"] = schemaVersion, ["DefaultPageId"] = defaultPageId, ["Pages"] = outPages };

        JsonObject root;
        try { root = (JsonNode.Parse(await File.ReadAllTextAsync(RuntimeConfigPath())) as JsonObject) ?? new JsonObject(); }
        catch { root = new JsonObject(); }
        root["Dashboard"] = dashObj; // n'écrit QUE cette section, préserve tout le reste
        var outText = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        try
        {
            await WriteFileAtomicAsync(RuntimeConfigPath(), outText);
            var src = SourceConfigPath();
            if (src != null && !string.Equals(src, RuntimeConfigPath(), StringComparison.OrdinalIgnoreCase))
                await WriteFileAtomicAsync(src, outText);
        }
        catch (Exception e) { Console.Error.WriteLine("SavePages KO : " + e); return Results.Json(new { ok = false, error = "Écriture de la configuration impossible." }); }
        try { _config = BuildConfig(); _payloadCache.Clear(); }
        catch (Exception e) { Console.Error.WriteLine("SavePages reload KO : " + e); }
        return Results.Json(new { ok = true, pages = outPages.Count });
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

    // POST /api/setup/oauth { clientId, clientSecret, authority } → écrit Auth.ClientId/ClientSecret/Authority
    // dans appsettings.json, recharge la config, et INVALIDE le cache des options OAuth → reconfiguration À CHAUD
    // (le bouton SSO devient actif sans redémarrage). Le Secret n'est pas renvoyé au client.
}
