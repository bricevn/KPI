using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;

namespace Kpi.Server;

/// <summary>
/// Dashboard MODULAIRE — pages PAR UTILISATEUR (modèle « tout par utilisateur »). Store séparé
/// <c>user-pages.json</c> indexé par username GitLab ; endpoints <c>/api/my-pages</c> accessibles à tout
/// utilisateur connecté mais n'écrivant QUE sous son propre compte. Aucun secret ; layouts uniquement.
/// Détail : docs/design/dashboard-modulaire.md. Partie de la classe partielle <see cref="WebDashboard"/>.
/// </summary>
public sealed partial class WebDashboard
{
    // Ids d'onglets NATIFS réservés (une page modulaire ne peut pas les réutiliser → conflit de routage).
    // ⚠️ DOIT rester synchronisé avec NAV_IDS de Assets/app/shell.jsx (+ 'options','pageeditor') : miroir
    // manuel de part et d'autre de la frontière JS/C#.
    private static readonly HashSet<string> ReservedPageIds = new(StringComparer.OrdinalIgnoreCase)
    { "dashboard", "charts", "anomalies", "issues", "calendar", "velocity", "comparison", "options", "pageeditor" };

    // Plafonds anti-abus (utilisateur authentifié, données auto-infligées, mais bornées) : cf. securite-et-donnees.md.
    private const int MaxPages = 50, MaxWidgetsPerPage = 50, MaxParamsPerWidget = 30;

    // Valide + normalise un modèle de dashboard (body JSON) en JsonObject camelCase (id/nav/layout/widgets).
    // Retourne (dash, null) ou (null, message). Validation légère (structure/ids/bornes) ; la validation
    // profonde (type ∈ window.KPI, data ∈ window.KPIData) est faite côté éditeur JS (le serveur ne peut
    // pas exécuter le registre).
    private static (JsonObject? dash, string? error) NormalizeDashboard(JsonNode? b)
    {
        static string? Str(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
        // Tolère les nombres JSON fractionnaires (l'input type=number peut en produire) : arrondi puis clamp appelant.
        static int Int(JsonNode? n, int def)
        {
            if (n is JsonValue v)
            {
                if (v.TryGetValue<int>(out var i)) return i;
                if (v.TryGetValue<double>(out var d)) return (int)Math.Round(d);
            }
            return def;
        }
        static bool Bool(JsonNode? n, bool def) => n is JsonValue v && v.TryGetValue<bool>(out var x) ? x : def;
        static string ParamVal(JsonNode? n) => n is JsonValue v ? (v.TryGetValue<string>(out var s) ? s : v.ToJsonString()) : "";
        static string Trunc(string? s, int max) { s = (s ?? "").Trim(); return s.Length > max ? s.Substring(0, max) : s; }

        var schemaVersion = Int(b?["schemaVersion"], 1);
        var defaultPageId = Trunc(Str(b?["defaultPageId"]), 64);
        var pagesIn = (b?["pages"] as JsonArray) ?? new JsonArray();
        if (pagesIn.Count > MaxPages) return (null, $"Trop de pages ({pagesIn.Count}) — maximum {MaxPages}.");
        var outPages = new JsonArray();
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pn in pagesIn)
        {
            if (pn is not JsonObject po) continue;
            var id = (Str(po["id"]) ?? "").Trim().ToLowerInvariant();
            if (!Regex.IsMatch(id, @"^[a-z0-9][a-z0-9-]{0,63}$")) return (null, $"Id de page invalide : « {id} » (minuscules, chiffres, tirets).");
            if (ReservedPageIds.Contains(id)) return (null, $"Id de page réservé (conflit avec un onglet natif) : « {id} ».");
            if (!seenIds.Add(id)) return (null, $"Id de page en double : « {id} ».");

            var navIn = po["nav"] as JsonObject ?? new JsonObject();
            var layIn = po["layout"] as JsonObject ?? new JsonObject();
            var cols = Math.Clamp(Int(layIn["cols"], 12), 1, 24);
            var nav = new JsonObject
            {
                ["label"] = Trunc(Str(navIn["label"]), 120),
                ["labelKey"] = Trunc(Str(navIn["labelKey"]), 64),
                ["icon"] = Trunc(Str(navIn["icon"]), 64),
                ["order"] = Int(navIn["order"], 100),
                ["showFilters"] = Bool(navIn["showFilters"], true),
                ["badgeSource"] = Trunc(Str(navIn["badgeSource"]), 64),
            };
            var layout = new JsonObject
            {
                ["cols"] = cols,
                ["gap"] = Trunc(Str(layIn["gap"]) is { Length: > 0 } g ? g : "var(--space-4)", 64),
                ["rowUnit"] = Math.Clamp(Int(layIn["rowUnit"], 88), 24, 400),
            };

            var widgetsIn = (po["widgets"] as JsonArray) ?? new JsonArray();
            if (widgetsIn.Count > MaxWidgetsPerPage) return (null, $"Trop de widgets dans « {id} » ({widgetsIn.Count}) — maximum {MaxWidgetsPerPage}.");
            var widgetsOut = new JsonArray();
            var seenW = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var wn in widgetsIn)
            {
                if (wn is not JsonObject wo) continue;
                var wid = (Str(wo["id"]) ?? "").Trim();
                // Id vide → 'w<n>' GARANTI unique (évite un rejet total si un id explicite entre en collision).
                if (wid.Length == 0) { var n = widgetsOut.Count + 1; while (seenW.Contains("w" + n)) n++; wid = "w" + n; }
                if (!seenW.Add(wid)) return (null, $"Id de widget en double dans « {id} » : « {wid} ».");
                var type = (Str(wo["type"]) ?? "").Trim();
                if (!Regex.IsMatch(type, @"^[A-Za-z0-9_]{1,64}$")) return (null, $"Type de widget invalide dans « {id} » : « {type} ».");
                var data = Trunc(Str(wo["data"]), 64);
                var wlIn = wo["layout"] as JsonObject ?? new JsonObject();
                var wl = new JsonObject { ["w"] = Math.Clamp(Int(wlIn["w"], 4), 1, cols), ["h"] = Math.Clamp(Int(wlIn["h"], 1), 1, 24), ["x"] = Int(wlIn["x"], -1), ["y"] = Int(wlIn["y"], -1) };
                var paramsIn = (wo["params"] as JsonObject) ?? new JsonObject();
                if (paramsIn.Count > MaxParamsPerWidget) return (null, $"Trop de paramètres pour un widget de « {id} » ({paramsIn.Count}) — maximum {MaxParamsPerWidget}.");
                var paramsOut = new JsonObject();
                foreach (var kv in paramsIn)
                {
                    if (kv.Key.Contains(':')) return (null, $"Clé de paramètre invalide (« : » interdit) : « {kv.Key} ».");
                    paramsOut[Trunc(kv.Key, 64)] = Trunc(ParamVal(kv.Value), 512);
                }
                widgetsOut.Add(new JsonObject { ["id"] = wid, ["type"] = type, ["data"] = data, ["layout"] = wl, ["params"] = paramsOut });
            }
            outPages.Add(new JsonObject { ["id"] = id, ["kind"] = "modular", ["nav"] = nav, ["layout"] = layout, ["widgets"] = widgetsOut });
        }
        if (defaultPageId.Length > 0 && !seenIds.Contains(defaultPageId)) return (null, $"defaultPageId inconnu : « {defaultPageId} ».");
        return (new JsonObject { ["schemaVersion"] = schemaVersion, ["defaultPageId"] = defaultPageId, ["pages"] = outPages }, null);
    }

    // --- Store par utilisateur (indexé par username ; portable entre appareils, gitignoré) --------------
    private static string UserPagesPath() => Path.Combine(AppContext.BaseDirectory, "user-pages.json");
    private static readonly SemaphoreSlim _userPagesLock = new(1, 1);
    private static JsonObject ReadUserPagesRoot()
    {
        try { return (JsonNode.Parse(File.ReadAllText(UserPagesPath())) as JsonObject) ?? new JsonObject(); }
        catch { return new JsonObject(); }
    }
    // Tableau camelCase des pages perso d'un utilisateur (pour injection window.__USER_PAGES__). "[]" si aucune.
    private static string UserPagesJson(string? user)
    {
        if (string.IsNullOrWhiteSpace(user)) return "[]";
        var mine = ReadUserPagesRoot()[user] as JsonObject;
        return ((mine?["pages"] as JsonArray) ?? new JsonArray()).ToJsonString();
    }

    // GET /api/my-pages → { ok, dashboard } : pages PERSO de l'utilisateur connecté (tout utilisateur).
    private IResult ServeMyPages(HttpContext ctx)
    {
        var user = ctx.User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(user)) return Results.Json(new { ok = false, error = "Non authentifié." }, statusCode: 401);
        var mine = ReadUserPagesRoot()[user] as JsonObject;
        return Results.Json(new { ok = true, dashboard = mine ?? new JsonObject { ["schemaVersion"] = 1, ["defaultPageId"] = "", ["pages"] = new JsonArray() } });
    }

    // POST /api/my-pages → écrit les pages PERSO de l'utilisateur CONNECTÉ (indexées par son username,
    // jamais pour un autre compte). Accepte pages:[] (vider = supprimer toutes ses pages).
    private async Task<IResult> SaveMyPagesAsync(HttpContext ctx)
    {
        var user = ctx.User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(user)) return Results.Json(new { ok = false, error = "Non authentifié." }, statusCode: 401);
        var (dash, error) = NormalizeDashboard(await ReadJsonBody(ctx));
        if (error != null) return Results.Json(new { ok = false, error });
        await _userPagesLock.WaitAsync();
        try
        {
            var root = ReadUserPagesRoot();
            root[user] = dash;
            await WriteFileAtomicAsync(UserPagesPath(), root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception e) { Console.Error.WriteLine("SaveMyPages KO : " + e); return Results.Json(new { ok = false, error = "Écriture impossible." }); }
        finally { _userPagesLock.Release(); }
        return Results.Json(new { ok = true, pages = (dash!["pages"] as JsonArray)?.Count ?? 0 });
    }
}
