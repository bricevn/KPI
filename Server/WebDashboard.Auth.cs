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

// Authentification, rôles GitLab et résolution de compte (RequireAdmin, member cache, /api/me).
public sealed partial class WebDashboard
{
    // --- Résolution de compte / rôles (étape 3) -------------------------

    private IResult? RequireAdmin(HttpContext ctx)
        => IsAdminLogin(ctx.User.Identity?.Name)
            ? null
            : Results.Text("Réservé aux administrateurs.", "text/plain; charset=utf-8", Encoding.UTF8, StatusCodes.Status403Forbidden);

    /// <summary>Accès aux endpoints de l'assistant : OUVERT tant que l'instance n'est pas configurée
    /// (1re mise en service après un clone), sinon réservé aux admins. Verrouille le bootstrap dès qu'il est fait.</summary>
    private IResult? RequireSetupAccess(HttpContext ctx)
        => (!IsConfigured() || IsAdminLogin(ctx.User.Identity?.Name))
            ? null
            : Results.Text("Réservé aux administrateurs.", "text/plain; charset=utf-8", Encoding.UTF8, StatusCodes.Status403Forbidden);

    private bool IsAdminLogin(string? login)
    {
        if (string.IsNullOrWhiteSpace(login)) return false;
        // Les admins viennent UNIQUEMENT de Auth.AdminUsers (appsettings.json, fichier serveur).
        // Volontairement NON modifiable via l'app : SaveConfigAsync préserve la section Auth,
        // et accounts.json ne peut plus promouvoir d'admin. Seul un accès au serveur change la liste.
        return _config.Auth.AdminUsers.Any(u => string.Equals(u, login, StringComparison.OrdinalIgnoreCase));
    }

    // --- Rôles GitLab : accès réservé aux membres du projet ---------------
    // Cache des access levels (username -> niveau ou null si non-membre), positif ET négatif.
    private readonly ConcurrentDictionary<string, (int? level, DateTime until)> _memberCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly TimeSpan MemberCacheTtl = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Access level GitLab de <paramref name="username"/> sur le projet configuré, résolu avec le
    /// token de SERVICE (le PAT de l'utilisateur n'a besoin que de read_user). null = non membre.
    /// En cas d'échec du lookup (GitLab injoignable), on prolonge la dernière valeur connue
    /// (dégradation douce pour les sessions actives) ; un inconnu reste refusé (fail-closed).
    /// </summary>
    /// <summary>Comptes techniques GitLab (porteurs des project/group access tokens) : jamais de session.</summary>
    private static bool IsBotUsername(string username)
        => Regex.IsMatch(username ?? "", @"^(project|group)_\d+_bot\d*$", RegexOptions.IgnoreCase);

    private const string ServerClaim = "kpi:server";

    // Serveur configuré correspondant à une instance GitLab (par hôte) / à un Id.
    private ServerConfig? ServerForInstance(string instance)
    {
        if (!Uri.TryCreate(instance, UriKind.Absolute, out var iu)) return null;
        foreach (var s in _config.ResolveServers())
            if (Uri.TryCreate(s.BaseUrl, UriKind.Absolute, out var su) && string.Equals(su.Host, iu.Host, StringComparison.OrdinalIgnoreCase))
                return s;
        return null;
    }
    private ServerConfig? ServerById(string? id)
        => string.IsNullOrEmpty(id) ? null : _config.ResolveServers().FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Access level max de <paramref name="username"/> sur les PROJETS d'un serveur, résolu avec le
    /// token de groupe du serveur. null = non membre. Cloisonné : cache par (serveur, username).
    /// Échec lookup → prolonge la dernière valeur connue (dégradation douce) ; inconnu refusé (fail-closed).
    /// Les bots (porteurs des tokens) sont traités comme non-membres.
    /// </summary>
    private async Task<int?> GetServerAccessLevelAsync(ServerConfig server, string username, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(username) || IsBotUsername(username)) return null;
        var cacheKey = server.Id + "|" + username;
        if (_memberCache.TryGetValue(cacheKey, out var hit) && hit.until > DateTime.UtcNow) return hit.level;
        try
        {
            var http = server.AllowSelfSignedCertificates ? _sharedHttpRelaxed : _sharedHttp;
            int? best = null;
            foreach (var pid in server.ProjectIds ?? new List<string>())
            {
                var url = $"{server.BaseUrl.TrimEnd('/')}/api/v4/projects/{Uri.EscapeDataString(pid)}/members/all?query={Uri.EscapeDataString(username)}&per_page=100";
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Add("PRIVATE-TOKEN", server.GroupToken);
                req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                using var resp = await http.SendAsync(req, ct);
                if (!resp.IsSuccessStatusCode) continue;
                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
                foreach (var m in doc.RootElement.EnumerateArray())
                    if (m.TryGetProperty("username", out var u) && string.Equals(u.GetString(), username, StringComparison.OrdinalIgnoreCase)
                        && m.TryGetProperty("access_level", out var al))
                    { var lvl = al.GetInt32(); if (best == null || lvl > best) best = lvl; break; }
            }
            _memberCache[cacheKey] = (best, DateTime.UtcNow.Add(MemberCacheTtl));
            return best;
        }
        catch
        {
            if (_memberCache.TryGetValue(cacheKey, out var stale)) { _memberCache[cacheKey] = (stale.level, DateTime.UtcNow.Add(MemberCacheTtl)); return stale.level; }
            return null;
        }
    }

    /// <summary>Session autorisée ? (admin, ou membre des projets du SERVEUR de la session — révocable à chaud.)</summary>
    private async Task<bool> IsAllowedAsync(HttpContext ctx)
    {
        var login = ctx.User.Identity?.Name ?? "";
        if (IsAdminLogin(login)) return true;
        var server = ServerById(ctx.User.FindFirst(ServerClaim)?.Value) ?? _config.ResolveServers().FirstOrDefault();
        if (server == null) return false;
        return await GetServerAccessLevelAsync(server, login, ctx.RequestAborted) != null;
    }

    private static bool LeadsContain(JsonNode? account, string login)
    {
        var leads = account?["leads"]?.AsArray();
        if (leads == null) return false;
        foreach (var l in leads)
            if (string.Equals(l?.GetValue<string>(), login, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    // Compte résolu (rôle / périmètre / vue) pour une identité GitLab.
    private sealed class Resolved
    {
        public string Login = "";
        public string Role = "user";       // admin | group | user
        public string DisplayName = "";
        public string ScopeType = "user";  // all | team | user
        public string? ScopeValue;
        public string? ViewId;
        public string? DefaultTab;
        public bool AutoProvisioned;
        public List<string> Tabs = new();
        public List<string> Milestones = new();
        public bool MilestonesLocked;
        public List<string> Labels = new();
        public bool LabelsLocked;
    }

    // Login effectif : un ADMIN peut impersonifier via ?as=<login> (pour prévisualiser une vue).
    private string EffectiveLogin(HttpContext ctx, out bool impersonating)
    {
        impersonating = false;
        var real = ctx.User.Identity?.Name ?? "";
        var asUser = ctx.Request.Query["as"].ToString();
        if (!string.IsNullOrWhiteSpace(asUser) && IsAdminLogin(real)) { impersonating = true; return asUser; }
        return real;
    }

    private Resolved ResolveAccount(string login)
    {
        var r = new Resolved { Login = login, DisplayName = login };
        JsonArray? accounts = null, views = null;
        try
        {
            using var s = new FileStream(AccountsPath(), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var root = JsonNode.Parse(s);
            accounts = root?["accounts"]?.AsArray();
            views = root?["views"]?.AsArray();
        }
        catch { }

        JsonNode? FindView(string? id)
        {
            if (views == null || string.IsNullOrEmpty(id)) return null;
            foreach (var v in views) if ((v?["id"]?.GetValue<string>() ?? "") == id) return v;
            return null;
        }
        var contentTabs = new[] { "dashboard", "charts", "issues", "events", "calendar", "velocity" };
        void ApplyView(JsonNode? v)
        {
            r.ViewId = v?["id"]?.GetValue<string>();
            r.DefaultTab = v?["defaultTab"]?.GetValue<string>();
            var vt = v?["tabs"]?.AsArray();
            r.Tabs = (vt != null && vt.Count > 0)
                ? vt.Select(t => t?.GetValue<string>() ?? "").Where(s => s.Length > 0).ToList()
                : contentTabs.ToList();
            var ms = v?["filters"]?["milestones"];
            if (ms != null) { r.Milestones = ms["values"]?.AsArray()?.Select(x => x?.GetValue<string>() ?? "").Where(s => s.Length > 0).ToList() ?? new(); r.MilestonesLocked = ms["locked"]?.GetValue<bool>() ?? false; }
            var lb = v?["filters"]?["labels"];
            if (lb != null) { r.Labels = lb["values"]?.AsArray()?.Select(x => x?.GetValue<string>() ?? "").Where(s => s.Length > 0).ToList() ?? new(); r.LabelsLocked = lb["locked"]?.GetValue<bool>() ?? false; }
        }

        if (IsAdminLogin(login))
        {
            r.Role = "admin"; r.ScopeType = "all"; r.ScopeValue = null;
            r.Tabs = contentTabs.Concat(new[] { "options" }).ToList();
            return r;
        }
        if (accounts != null)
        {
            foreach (var a in accounts)
                if ((a?["type"]?.GetValue<string>() ?? "") == "group" && LeadsContain(a, login))
                {
                    r.Role = "group"; r.ScopeType = "team"; r.ScopeValue = a?["subject"]?.GetValue<string>();
                    r.DisplayName = a?["username"]?.GetValue<string>() ?? login;
                    ApplyView(FindView(a?["viewId"]?.GetValue<string>()));
                    return r;
                }
            foreach (var a in accounts)
                if ((a?["type"]?.GetValue<string>() ?? "") == "user"
                    && string.Equals(a?["subject"]?.GetValue<string>(), login, StringComparison.OrdinalIgnoreCase))
                {
                    r.Role = "user"; r.ScopeType = "user"; r.ScopeValue = login;
                    r.DisplayName = a?["username"]?.GetValue<string>() ?? login;
                    ApplyView(FindView(a?["viewId"]?.GetValue<string>()));
                    return r;
                }
        }
        // Auto-provision : tout salarié connecté non listé → vue individuelle par défaut.
        r.Role = "user"; r.ScopeType = "user"; r.ScopeValue = login; r.AutoProvisioned = true;
        ApplyView(FindView(_config.Auth.DefaultViewId));
        return r;
    }

    private object ResolveMe(HttpContext ctx)
    {
        if (!(ctx.User.Identity?.IsAuthenticated ?? false) || string.IsNullOrEmpty(ctx.User.Identity?.Name))
            return new { authenticated = false };
        var login = EffectiveLogin(ctx, out var impersonating);
        var r = ResolveAccount(login);
        return new
        {
            authenticated = true,
            login = r.Login,
            role = r.Role,
            displayName = r.DisplayName,
            scope = new { type = r.ScopeType, value = r.ScopeValue },
            viewId = r.ViewId,
            defaultTab = r.DefaultTab,
            tabs = r.Tabs,
            filters = new
            {
                milestones = new { values = r.Milestones, locked = r.MilestonesLocked },
                labels = new { values = r.Labels, locked = r.LabelsLocked }
            },
            autoProvisioned = r.AutoProvisioned,
            impersonating,
            canImpersonate = IsAdminLogin(ctx.User.Identity?.Name) // l'utilisateur RÉELLEMENT connecté est admin
        };
    }

}
