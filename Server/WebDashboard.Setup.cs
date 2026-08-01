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

// Assistant de première mise en service /setup : test de connexion, labels, OAuth, sauvegarde de la configuration et extraction initiale.
public sealed partial class WebDashboard
{
    // --- Assistant de première mise en service (/setup) -----------------

    // Configuré ? = au moins un serveur effectif (Servers v2 OU bloc GitLab legacy via ResolveServers)
    // a une URL + un token. Sinon `/` redirige vers /setup.
    private bool IsConfigured() =>
        _config.ResolveServers().Any(s =>
            !string.IsNullOrWhiteSpace(s.BaseUrl) && !string.IsNullOrWhiteSpace(s.GroupToken));

    private static async Task<JsonNode?> ReadJsonBody(HttpContext ctx)
    {
        // Corps vide / JSON malformé → null (les appelants tolèrent null via b?[...]) plutôt qu'une 500.
        try { using var r = new StreamReader(ctx.Request.Body); return JsonNode.Parse(await r.ReadToEndAsync()); }
        catch { return null; }
    }

    // Garde anti-SSRF du setup : URL absolue http/https + (si Auth.Authority défini) même hôte.
    // Pendant le bootstrap (Authority non défini), admin-only suffit ; sinon on verrouille sur l'autorité.
    private bool SetupHostAllowed(Uri baseUri)
    {
        if (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps) return false;
        if (!string.IsNullOrWhiteSpace(_config.Auth.Authority)
            && Uri.TryCreate(_config.Auth.Authority, UriKind.Absolute, out var au))
            return string.Equals(baseUri.Host, au.Host, StringComparison.OrdinalIgnoreCase);
        return true;
    }

    // GET GitLab avec le token fourni par l'assistant (HttpClient PARTAGÉ — pas de socket exhaustion).
    private async Task<JsonNode?> GlGet(HttpClient http, Uri baseUri, string path, string token, CancellationToken ct)
    {
        // Robuste : toute erreur réseau/parse (instance injoignable, DNS, TLS, JSON) → null (pas de 500).
        // L'appelant traduit null en message clair ("Connexion refusée…") dans l'assistant.
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, new Uri(baseUri, path));
            req.Headers.Add("PRIVATE-TOKEN", token);
            using var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return JsonNode.Parse(await resp.Content.ReadAsStringAsync(ct));
        }
        catch { return null; }
    }

    // POST /api/setup/test → { ok, projects:[{id,name,group}], groups:[{name,members:[{username,name,role}]}] }
    private async Task<IResult> SetupTestAsync(HttpContext ctx)
    {
        var deny = RequireSetupAccess(ctx); if (deny != null) return deny;
        var b = await ReadJsonBody(ctx);
        var baseUrl = (b?["baseUrl"]?.GetValue<string>() ?? "").Trim().TrimEnd('/');
        var token   = (b?["token"]?.GetValue<string>() ?? "").Trim();
        var selfS   = b?["selfSigned"]?.GetValue<bool>() ?? false;
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) || string.IsNullOrWhiteSpace(token))
            return Results.Json(new { ok = false, error = "URL ou token invalide." });
        if (!SetupHostAllowed(baseUri))
            return Results.Json(new { ok = false, error = "Instance non autorisée (différente de l'autorité configurée)." });

        var http = selfS ? _sharedHttpRelaxed : _sharedHttp;
        var me = await GlGet(http, baseUri, "/api/v4/user", token, ctx.RequestAborted);
        if (me is null) return Results.Json(new { ok = false, error = "Connexion refusée. Vérifiez l'URL et le token." });

        var projects = new List<object>();
        var pj = await GlGet(http, baseUri, "/api/v4/projects?membership=true&simple=true&per_page=100&order_by=name&sort=asc", token, ctx.RequestAborted);
        foreach (var p in pj?.AsArray() ?? new JsonArray())
        {
            var pid = p!["id"]!.GetValue<int>();
            // Milestones du projet (récentes d'abord) : alimentent le sélecteur « Milestone de départ
            // de l'export » du récap. Tri par date due/start décroissante (sans date → en dernier).
            var milestones = new List<string>();
            var mj = await GlGet(http, baseUri, $"/api/v4/projects/{pid}/milestones?per_page=100&state=all", token, ctx.RequestAborted);
            milestones = (mj?.AsArray() ?? new JsonArray())
                .Select(m => new
                {
                    title = m?["title"]?.GetValue<string>() ?? "",
                    date  = m?["due_date"]?.GetValue<string>() ?? m?["start_date"]?.GetValue<string>() ?? ""
                })
                .Where(m => !string.IsNullOrWhiteSpace(m.title))
                .OrderByDescending(m => m.date, StringComparer.Ordinal)   // ISO yyyy-MM-dd → tri lexical OK
                .ThenByDescending(m => m.title, StringComparer.OrdinalIgnoreCase)
                .Select(m => m.title)
                .ToList();
            projects.Add(new { id = pid, name = p["name"]!.GetValue<string>(),
                group = p["namespace"]?["path"]?.GetValue<string>() ?? "",
                // full_path du namespace : clé STABLE pour rattacher un projet à son groupe (= group.name = full_path).
                groupFull = p["namespace"]?["full_path"]?.GetValue<string>() ?? "",
                milestones });
        }

        var groups = new List<object>();
        var gj = await GlGet(http, baseUri, "/api/v4/groups?per_page=100&order_by=name", token, ctx.RequestAborted);
        foreach (var g in gj?.AsArray() ?? new JsonArray())
        {
            var gid = g!["id"]!.GetValue<int>();
            var members = new List<object>();
            var mj = await GlGet(http, baseUri, $"/api/v4/groups/{gid}/members?per_page=100", token, ctx.RequestAborted);
            foreach (var m in mj?.AsArray() ?? new JsonArray())
            {
                var lvl = m!["access_level"]?.GetValue<int>() ?? 0; // 40 Maintainer / 50 Owner → lead ; sinon membre
                members.Add(new { username = m["username"]!.GetValue<string>(),
                    name = m["name"]?.GetValue<string>() ?? m["username"]!.GetValue<string>(),
                    role = lvl >= 40 ? "lead" : "member" });
            }
            groups.Add(new { name = g["full_path"]?.GetValue<string>() ?? g["name"]!.GetValue<string>(), members });
        }
        return Results.Json(new { ok = true, projects, groups });
    }

    // POST /api/setup/labels { baseUrl, token, selfSigned, projectIds:[] }
    //   → { ok, labels:[...], total, perProject:[{id,count,ok}] }
    // perProject permet de distinguer « projet sans label » (ok:true,count:0) d'un « échec d'accès » (ok:false).
    private async Task<IResult> SetupLabelsAsync(HttpContext ctx)
    {
        var deny = RequireSetupAccess(ctx); if (deny != null) return deny;
        var b = await ReadJsonBody(ctx);
        var baseUrl = (b?["baseUrl"]?.GetValue<string>() ?? "").Trim().TrimEnd('/');
        var token   = (b?["token"]?.GetValue<string>() ?? "").Trim();
        var selfS   = b?["selfSigned"]?.GetValue<bool>() ?? false;
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) || !SetupHostAllowed(baseUri))
            return Results.Json(new { ok = false, error = "Instance non autorisée." });

        var http = selfS ? _sharedHttpRelaxed : _sharedHttp;
        var set = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var perProject = new JsonArray();
        foreach (var pidNode in (b?["projectIds"] as JsonArray) ?? new JsonArray())
        {
            // ProjectIds tolérant : nombre (id) OU chaîne (id ou chemin "namespace/projet").
            var pid = pidNode is JsonValue v && v.TryGetValue<int>(out var iv) ? iv.ToString() : (pidNode?.GetValue<string>() ?? "");
            if (string.IsNullOrWhiteSpace(pid)) continue;
            var enc = Uri.EscapeDataString(pid);
            int count = 0; bool ok = false;
            // include_ancestor_groups=true : les labels Prod:: sont souvent définis au niveau GROUPE.
            // Pagination (jusqu'à 5×100) pour les projets riches en labels.
            for (int page = 1; page <= 5; page++)
            {
                var lj = await GlGet(http, baseUri, $"/api/v4/projects/{enc}/labels?per_page=100&page={page}&include_ancestor_groups=true&with_counts=false", token, ctx.RequestAborted);
                if (lj is not JsonArray arr) break; // null = échec requête (accès/réseau) ou réponse inattendue
                ok = true;
                if (arr.Count == 0) break;
                foreach (var l in arr) { var n = l?["name"]?.GetValue<string>(); if (!string.IsNullOrWhiteSpace(n)) { set.Add(n); count++; } }
                if (arr.Count < 100) break;
            }
            perProject.Add(new JsonObject { ["id"] = pid, ["count"] = count, ["ok"] = ok });
        }
        return Results.Json(new { ok = true, labels = set, total = set.Count, perProject });
    }


    private async Task<IResult> SetupOAuthSaveAsync(HttpContext ctx)
    {
        var deny = RequireSetupAccess(ctx); if (deny != null) return deny;
        var b = await ReadJsonBody(ctx);
        var clientId     = (b?["clientId"]?.GetValue<string>() ?? "").Trim();
        var clientSecret = (b?["clientSecret"]?.GetValue<string>() ?? "").Trim();
        var authority    = (b?["authority"]?.GetValue<string>() ?? "").Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(clientId))
            return Results.Json(new { ok = false, error = "Application ID requis." });
        // Instance EXPLICITE et obligatoire : plus de repli silencieux sur l'Authority existante (c'était le piège
        // qui laissait gitlab.com quand le champ n'était pas renseigné → SSO vers gitlab.com public).
        if (string.IsNullOrWhiteSpace(authority))
            return Results.Json(new { ok = false, error = "Renseignez l'URL de l'instance GitLab (ex. https://gitlab.exemple.com)." });
        if (!Uri.TryCreate(authority, UriKind.Absolute, out var au) || (au.Scheme != Uri.UriSchemeHttp && au.Scheme != Uri.UriSchemeHttps))
            return Results.Json(new { ok = false, error = "URL d'instance invalide (http/https requis)." });

        JsonObject root;
        try { root = (JsonNode.Parse(await File.ReadAllTextAsync(RuntimeConfigPath())) as JsonObject) ?? new JsonObject(); }
        catch { root = new JsonObject(); }
        var auth = root["Auth"] as JsonObject ?? new JsonObject(); root["Auth"] = auth;
        // Secret : requis à la première config ; en RECONFIGURATION (champ laissé vide), on CONSERVE l'existant
        // (permet de corriger l'instance seule sans recoller le secret).
        var existingSecret = (auth["ClientSecret"]?.GetValue<string>() ?? "").Trim();
        if (string.IsNullOrWhiteSpace(clientSecret) && string.IsNullOrWhiteSpace(existingSecret))
            return Results.Json(new { ok = false, error = "Secret requis." });
        auth["Authority"] = authority;
        auth["ClientId"] = clientId;
        if (!string.IsNullOrWhiteSpace(clientSecret)) auth["ClientSecret"] = SecureStore.ProtectSecret(clientSecret); // secret OAuth chiffré au repos
        if (auth["CallbackPath"] == null) auth["CallbackPath"] = "/signin-gitlab";
        // Cert auto-signé / CA interne : porté par l'étape 1 (avant l'enregistrement du serveur), persisté sur Auth
        // pour que le backchannel OAuth tolère le TLS dès le bootstrap. Conserve une valeur déjà posée si omise.
        var selfSigned = b?["selfSigned"]?.GetValue<bool>() ?? (auth["AllowSelfSignedCertificates"]?.GetValue<bool>() ?? false);
        auth["AllowSelfSignedCertificates"] = selfSigned;

        var outText = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        try
        {
            await WriteFileAtomicAsync(RuntimeConfigPath(), outText);
            var src = SourceConfigPath();
            if (src != null && !string.Equals(src, RuntimeConfigPath(), StringComparison.OrdinalIgnoreCase))
                await WriteFileAtomicAsync(src, outText);
        }
        catch (Exception e) { Console.Error.WriteLine("SetupOAuthSave write KO : " + e); return Results.Json(new { ok = false, error = "Écriture de la configuration impossible." }); }

        try { _config = BuildConfig(); }
        catch (Exception e) { Console.Error.WriteLine("SetupOAuthSave reload KO : " + e); return Results.Json(new { ok = false, error = "Enregistré, mais rechargement échoué (redémarrez le serveur)." }); }

        // Reconfiguration À CHAUD : vider le cache des options du schéma « gitlab » → reconstruites (via le
        // delegate AddOAuth) avec les nouveaux identifiants au prochain challenge. Pas de redémarrage requis.
        try { (ctx.RequestServices.GetService(typeof(Microsoft.Extensions.Options.IOptionsMonitorCache<Microsoft.AspNetCore.Authentication.OAuth.OAuthOptions>)) as Microsoft.Extensions.Options.IOptionsMonitorCache<Microsoft.AspNetCore.Authentication.OAuth.OAuthOptions>)?.TryRemove("gitlab"); }
        catch (Exception e) { Console.Error.WriteLine("OAuth options cache invalidation KO : " + e); }

        return Results.Json(new { ok = true });
    }

    // POST /api/setup/canny { apiKey } → étape « Connexions externes » du wizard (facultative). Ouvert au
    // bootstrap (RequireSetupAccess), sinon admin-only. Valide + chiffre la clé (cœur partagé avec les Options).
    private async Task<IResult> SetupCannySaveAsync(HttpContext ctx)
    {
        var deny = RequireSetupAccess(ctx); if (deny != null) return deny;
        return await WriteCannyConnectionAsync(ctx);
    }

    // POST /api/setup { baseUrl, token, selfSigned, timeout, projectIds, labelPhases, teams } → écrit appsettings.json
    private async Task<IResult> SetupSaveAsync(HttpContext ctx)
    {
        var deny = RequireSetupAccess(ctx); if (deny != null) return deny;
        var bootstrap = !IsConfigured(); // 1re mise en service : on pourra écrire Auth (admin) ; sinon Auth verrouillé
        var b = await ReadJsonBody(ctx);
        var baseUrl = (b?["baseUrl"]?.GetValue<string>() ?? "").Trim().TrimEnd('/');
        var token   = (b?["token"]?.GetValue<string>() ?? "").Trim();
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(token))
            return Results.Json(new { ok = false, error = "Connexion manquante." });
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) || !SetupHostAllowed(baseUri))
            return Results.Json(new { ok = false, error = "Instance non autorisée." });

        var projectIds = ((b?["projectIds"] as JsonArray) ?? new JsonArray()).Select(n => n!.GetValue<int>()).ToList();
        if (projectIds.Count == 0) return Results.Json(new { ok = false, error = "Sélectionnez au moins un projet." });

        // Catalogue des périodes (phases) — normalisé en PascalCase pour matcher le DTO PeriodDefinition
        // (binding tolérant à la casse, mais on reste explicite). « none » exclu (marqueur, pas une période).
        // Extraction robuste contre les types JSON inattendus (admin pouvant envoyer un body malformé).
        static string? Str(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
        var periodsArr = new JsonArray();
        var validPeriodKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in (b?["periods"] as JsonArray) ?? new JsonArray())
        {
            if (p is not JsonObject po) continue;
            var key = (Str(po["key"]) ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(key) || key == "none" || !validPeriodKeys.Add(key)) continue;
            var name = (Str(po["name"]) ?? "").Trim();
            if (string.IsNullOrEmpty(name)) name = key;                       // « » → repli sur la clé
            var color = (Str(po["color"]) ?? "").Trim();
            if (!Regex.IsMatch(color, "^#[0-9a-fA-F]{6}$")) color = "#cccccc"; // hex strict, sinon défaut
            var role = PeriodRole(po);
            periodsArr.Add(new JsonObject
            {
                ["Key"]   = key,
                ["Name"]  = name,
                ["Color"] = color,
                ["Role"]  = role,
                ["Timed"] = role != "nogc",
            });
        }

        var trackedLabels = new List<string>();
        var labelPhases = new Dictionary<string, string>();
        foreach (var kv in ((b?["labelPhases"] as JsonObject) ?? new JsonObject()))
        {
            var ph = kv.Value?.GetValue<string>() ?? "none";
            // Validation croisée : un label pointant vers une période inexistante est rétrogradé en « none »
            // (pas de key orpheline). Si aucune période n'est transmise → on accepte tel quel (rétro-compat).
            if (ph != "none" && validPeriodKeys.Count > 0 && !validPeriodKeys.Contains(ph)) ph = "none";
            labelPhases[kv.Key] = ph;
            if (ph != "none") trackedLabels.Add(kv.Key);
        }

        var teams = new JsonObject();
        var teamGroups = new JsonObject();           // nom d'équipe → groupPath (full_path du groupe GitLab)
        foreach (var t in b?["teams"]?.AsArray() ?? new JsonArray())
        {
            var name = t!["name"]!.GetValue<string>();
            var arr = new JsonArray();
            foreach (var m in t["members"]?.AsArray() ?? new JsonArray())
                arr.Add(m!["username"]!.GetValue<string>());
            teams[name] = arr;
            var gp = (Str(t["groupPath"]) ?? "").Trim();
            if (gp.Length > 0) teamGroups[name] = gp;
        }

        // Projets importés AVEC nom + namespace (l'onglet Options du dashboard ne peut pas dériver les noms des IDs).
        var projectsArr = new JsonArray();
        foreach (var p in (b?["projects"] as JsonArray) ?? new JsonArray())
        {
            if (p is not JsonObject po) continue;
            var pid = po["id"] is JsonValue piv && piv.TryGetValue<int>(out var pii) ? pii : 0;
            if (pid == 0) continue;
            projectsArr.Add(new JsonObject
            {
                ["Id"]    = pid,
                ["Name"]  = (Str(po["name"]) ?? "").Trim(),
                ["Group"] = (Str(po["group"]) ?? "").Trim(),
            });
        }

        // Merge non destructif : la section Auth (admins, OAuth) est PRÉSERVÉE telle quelle (non modifiable via l'app).
        JsonObject root;
        try { root = (JsonNode.Parse(await File.ReadAllTextAsync(RuntimeConfigPath())) as JsonObject) ?? new JsonObject(); }
        catch { root = new JsonObject(); }

        var selfSigned = b?["selfSigned"]?.GetValue<bool>() ?? false;
        var timeout = b?["timeout"]?.GetValue<int>() ?? 60;
        var serverId = DeriveServerId(baseUri);

        // 1c-D : on n'écrit plus le bloc GitLab legacy ; on retire un éventuel bloc résiduel pour une config propre.
        if (root["GitLab"] != null) root.Remove("GitLab");

        // v2 — entrée Servers cloisonnée (token de GROUPE, projets sélectionnés). Insert OU update par Id
        // (dérivé de l'hôte) → relancer /setup pour une autre instance AJOUTE un serveur sans écraser les autres.
        var serversArr = root["Servers"] as JsonArray;
        if (serversArr == null) { serversArr = new JsonArray(); root["Servers"] = serversArr; }
        JsonObject? entry = null;
        foreach (var sNode in serversArr)
            if (sNode is JsonObject so && string.Equals(so["Id"]?.GetValue<string>(), serverId, StringComparison.OrdinalIgnoreCase))
            { entry = so; break; }
        if (entry == null) { entry = new JsonObject(); serversArr.Add(entry); }
        entry["Id"] = serverId;
        entry["BaseUrl"] = baseUrl;
        entry["GroupToken"] = SecureStore.ProtectSecret(token); // jamais de token GitLab en clair au repos
        entry["ProjectIds"] = new JsonArray(projectIds.Select(i => JsonValue.Create(i.ToString())).ToArray());
        entry["AllowSelfSignedCertificates"] = selfSigned;
        entry["RequestTimeoutSeconds"] = timeout;

        var ex = root["Export"] as JsonObject ?? new JsonObject(); root["Export"] = ex;
        ex["TrackedLabels"] = new JsonArray(trackedLabels.Select(s => JsonValue.Create(s)).ToArray());
        ex["LabelPhases"] = JsonSerializer.SerializeToNode(labelPhases);
        ex["Teams"] = teams;
        ex["TeamGroups"] = teamGroups;
        ex["ProjectIds"] = new JsonArray(projectIds.Select(i => JsonValue.Create(i)).ToArray());
        // Projets importés (nom + namespace) — n'écrit que si le client les transmet (sinon préserve l'existant).
        if (b?["projects"] is JsonArray) ex["Projects"] = projectsArr;
        // v4 — milestone à IMPORTER par projet (périmètre de la 1re extraction — pas une borne ; les
        // runs globaux rafraîchissent ensuite les milestones déjà importées).
        // Clés = ids de projets SÉLECTIONNÉS uniquement ; valeurs vides ignorées (= tout l'historique).
        if (b?["startMilestones"] is JsonObject smIn)
        {
            var outSm = new JsonObject();
            foreach (var kv in smIn)
            {
                if (!int.TryParse(kv.Key, out var smPid) || !projectIds.Contains(smPid)) continue;
                var smTitle = (Str(kv.Value) ?? "").Trim();
                if (smTitle.Length > 0) outSm[smPid.ToString()] = smTitle;
            }
            ex["StartMilestones"] = outSm;
        }
        // Catalogue des périodes : on n'écrit QUE si le wizard a transmis le champ (même vide = volonté
        // explicite de « pas de phase »). Champ absent (client ancien) → on préserve l'éventuel existant.
        if (b?["periods"] is JsonArray) ex["Periods"] = periodsArr;

        // v3 — PHASES PAR PROJET (mode « Par projet » du wizard). Persistées en plus du global ; un projet
        // absent retombe sur le global. ⚠ Stage 1 : écrites mais PAS encore consommées par le dashboard (Stage 2).
        if (b?["periodsByProject"] is JsonObject pbp)
        {
            var outPbp = new JsonObject();
            foreach (var kv in pbp)
            {
                if (kv.Value is not JsonArray parr) continue;
                var arr = new JsonArray();
                var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
                foreach (var e in m) { mm[e.Key] = (Str(e.Value) ?? "none"); }
                outLbp[kv.Key] = mm;
            }
            ex["LabelPhasesByProject"] = outLbp;
        }

        // BOOTSTRAP (1re mise en service) : établir le 1er admin + l'autorité (login/OAuth). C'est la SEULE
        // écriture de Auth via l'app ; une fois configuré, Auth est verrouillé (cf. SaveConfigAsync préserve Auth).
        if (bootstrap)
        {
            // Admin de la 1re mise en service. Source PRIORITAIRE : le compte GitLab qui a ouvert la SESSION
            // OAuth pour atteindre /setup (ctx.User) → pas d'injection possible. Repli rétro-compatible : le(s)
            // username(s) du body `admins` (ancien flux où l'admin n'était pas encore authentifié). Au moins un requis.
            var admins = new JsonArray();
            var oauthLogin = ctx.User.Identity?.Name ?? "";
            if (!string.IsNullOrWhiteSpace(oauthLogin)) admins.Add(JsonValue.Create(oauthLogin));
            else foreach (var a in b?["admins"]?.AsArray() ?? new JsonArray())
            { var u = (Str(a) ?? "").Trim(); if (u.Length > 0) admins.Add(JsonValue.Create(u)); }
            if (admins.Count == 0) return Results.Json(new { ok = false, error = "Connectez-vous via GitLab (ou indiquez au moins un compte administrateur)." });
            var auth = root["Auth"] as JsonObject ?? new JsonObject(); root["Auth"] = auth;
            auth["Authority"] = baseUrl;     // verrouille l'instance de login sur ce host
            auth["AdminUsers"] = admins;
            if (auth["CallbackPath"] == null) auth["CallbackPath"] = "/signin-gitlab";
        }

        var outText = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        try
        {
            await WriteFileAtomicAsync(RuntimeConfigPath(), outText);
            var src = SourceConfigPath();
            if (src != null && !string.Equals(src, RuntimeConfigPath(), StringComparison.OrdinalIgnoreCase))
                await WriteFileAtomicAsync(src, outText);
        }
        catch (Exception e) { Console.Error.WriteLine("SetupSave write KO : " + e); return Results.Json(new { ok = false, error = "Écriture de la configuration impossible." }); }

        try { _config = BuildConfig(); _memberCache.Clear(); _payloadCache.Clear(); }
        catch (Exception e) { Console.Error.WriteLine("SetupSave reload KO : " + e); return Results.Json(new { ok = false, error = "Configuration enregistrée, mais rechargement échoué (redémarrez le serveur)." }); }

        // Fetch-all multi-serveurs en arrière-plan (best-effort) : extrait les projets sélectionnés
        // et écrit les données CHIFFRÉES sous output/<serverId>/. Le dashboard suit l'avancement via /api/status.
        StartSetupFetch(ctx);

        // bootstrap : la session est anonyme et l'instance vient de devenir « configurée » → le frontend
        // redirige vers /login (l'admin se connecte ; l'extraction tourne en fond). Sinon (admin) → loader.
        return Results.Json(new { ok = true, jobId = "setup", bootstrap });
    }

    /// <summary>Identifiant de serveur stable dérivé de l'hôte de l'instance (segment de dossier, [a-z0-9-]).</summary>
    private static string DeriveServerId(Uri baseUri)
    {
        var host = baseUri.Host ?? "";
        var r = new string(host.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-').ToArray()).Trim('-');
        return string.IsNullOrEmpty(r) ? "default" : r;
    }

    /// <summary>Lance l'extraction multi-serveurs en tâche de fond après une mise en service réussie
    /// (réutilise le verrou/état/CTS du refresh ; RunRefreshAsync route vers le multi-serveur car Servers est configuré).</summary>
    private void StartSetupFetch(HttpContext ctx)
    {
        if (!_refreshLock.Wait(0)) return; // une acquisition tourne déjà → ne pas doubler
        // État posé SYNCHRONEMENT (avant le Task.Run) : sinon le 1er poll /api/setup/progress verrait
        // Running=false et conclurait 'done' à tort → redirection prématurée vers le dashboard.
        _state.Reset();
        _state.Running = true;
        _state.StartedAt = DateTime.UtcNow;
        var appStopping = ctx.RequestServices.GetService(typeof(IHostApplicationLifetime)) as IHostApplicationLifetime;
        var serverCt = appStopping?.ApplicationStopping ?? CancellationToken.None;
        _ = Task.Run(() => RunRefreshAsync(new List<string>(), null, serverCt));
    }

    /// <summary>Progression du scrap post-setup, mappée sur l'état du job (loader temps réel côté /setup).</summary>
    private IResult SetupProgress(HttpContext ctx)
    {
        var deny = RequireSetupAccess(ctx); if (deny != null) return deny;
        var s = _state.Snapshot();
        var status = s.running ? "running" : (!string.IsNullOrEmpty(s.lastError) ? "error" : "done");
        var percent = s.total > 0 ? Math.Min(99, (int)Math.Round(s.current * 100.0 / s.total)) : (s.running ? 3 : 100);
        if (status == "done") percent = 100;
        double? eta = null;
        var started = _state.StartedAt;
        if (status == "running" && percent > 0 && started != null)
        {
            var el = (DateTime.UtcNow - started.Value).TotalSeconds;
            if (el > 0) eta = Math.Round(el / percent * (100 - percent));
        }
        return Results.Json(new
        {
            status,
            percent,
            stage = "issues",
            project = (string?)null,
            message = status == "done" ? "Terminé"
                : status == "error" ? (s.lastError ?? "Erreur")
                : (s.total > 0 ? $"Extraction des données… ({s.current}/{s.total})" : "Démarrage de l'extraction…"),
            etaSeconds = eta,
            counts = new { issues = new[] { s.current, s.total } },
            error = string.IsNullOrEmpty(s.lastError) ? null : s.lastError,
        });
    }

}
