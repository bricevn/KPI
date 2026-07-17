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

/// <summary>
/// Serveur du dashboard (ASP.NET Core / Kestrel), bindé sur localhost — l'exposition publique passe
/// par un reverse proxy (TLS en amont). Authentification par SSO GitLab (OAuth) + rôles GitLab.
/// Classe PARTIELLE, découpée par domaine :
///   WebDashboard.cs         — bootstrap, routes, refresh, payload/data, état.
///   WebDashboard.Auth.cs    — authentification, rôles, résolution de compte.
///   WebDashboard.Setup.cs   — assistant /setup (test, labels, OAuth, sauvegarde, fetch initial).
///   WebDashboard.Options.cs — API de l'onglet Options (config phases/équipes, calcul du temps).
/// </summary>
public sealed partial class WebDashboard
{
    private volatile AppConfig _config; // rechargé à chaud via /api/config (volatile : visibilité multi-thread)
    private readonly RefreshState _state = new();
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private CancellationTokenSource? _refreshCts;
    private readonly object _ctsLock = new();

    // HTTP partagé (réutilise le pool de connexions) pour les appels GitLab par requête (login token,
    // résolution des membres) : un HttpClient neuf par requête épuise les sockets sous charge.
    private static readonly HttpClient _sharedHttp = BuildSharedHttp(strict: true);
    private static readonly HttpClient _sharedHttpRelaxed = BuildSharedHttp(strict: false);
    private static HttpClient BuildSharedHttp(bool strict)
    {
        var h = new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) };
        if (!strict) h.SslOptions.RemoteCertificateValidationCallback = (_, _, _, _) => true;
        return new HttpClient(h) { Timeout = TimeSpan.FromSeconds(15) };
    }
    private HttpClient SharedHttp => (_config.ResolveServers().FirstOrDefault()?.AllowSelfSignedCertificates ?? false) ? _sharedHttpRelaxed : _sharedHttp;

    private WebDashboard(AppConfig config) => _config = config;

    public static async Task RunAsync(AppConfig config, int port, CancellationToken ct)
    {
        // Migrations de config au repos (secrets chiffrés + semis rétro-compat des labels transversaux).
        // La config EN MÉMOIRE reste exploitable (secrets déjà déchiffrés, transversaux synchronisés).
        await MigrateConfigAtRestAsync(config);

        var self = new WebDashboard(config);

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = AppContext.BaseDirectory
        });
        // Bind localhost uniquement : l'exposition publique passe par un reverse proxy (TLS).
        builder.WebHost.UseUrls($"http://localhost:{port}");
        builder.Logging.AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "HH:mm:ss "; });

        // --- Authentification : OAuth GitLab + Personal Access Token (page /login) --------
        var authCfg = config.Auth;
        var authBuilder = builder.Services.AddAuthentication(o =>
        {
            o.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            // La page /login designée gère le choix OAuth/token. Le challenge par défaut doit
            // donc rester le cookie (→ redirection vers /login via OnRedirectToLogin), sinon
            // l'utilisateur non authentifié saute directement vers GitLab sans voir la page.
            o.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        })
        .AddCookie(o =>
        {
            o.Cookie.Name = "gle_session";
            o.Cookie.HttpOnly = true;
            o.Cookie.SameSite = SameSiteMode.Lax;
            o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; // http local OK ; https imposé en prod (proxy)
            o.ExpireTimeSpan = TimeSpan.FromHours(8);
            o.SlidingExpiration = true;
            o.LoginPath = "/login";
            o.LogoutPath = "/logout";
            // /api/* → 401 plutôt qu'une redirection (exploitable en fetch).
            o.Events.OnRedirectToLogin = ctx =>
            {
                if (ctx.Request.Path.StartsWithSegments("/api"))
                {
                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                }
                ctx.Response.Redirect(ctx.RedirectUri);
                return Task.CompletedTask;
            };
        });

        // Data Protection : clés persistées → sessions conservées au redémarrage et partagées entre
        // instances (déploiement mondial). En multi-instance, monter ce dossier sur un volume PARTAGÉ
        // (ou remplacer par PersistKeysToStackExchangeRedis / Azure Blob).
        var dp = builder.Services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "dp-keys")))
            .SetApplicationName("Kpi");
        // Windows : clé maîtresse dp-keys chiffrée via DPAPI (même politique que SecureStore) — sans
        // ça, le XML en clair à côté du binaire rend le chiffrement au repos contournable.
        if (OperatingSystem.IsWindows()) dp.ProtectKeysWithDpapi();

        // OAuth GitLab enregistré INCONDITIONNELLEMENT : les identifiants sont relus EN DIRECT depuis la config
        // (self._config.Auth) à chaque (re)construction des options. Non configuré → placeholders (la validation
        // ne jette pas) ; le challenge est de toute façon gardé par OAuthConfigured (live) dans /auth/oauth.
        // Après saisie des creds via /api/setup/oauth, on invalide le cache d'options → reconfig À CHAUD (sans redémarrage).
        authBuilder.AddOAuth("gitlab", o =>
        {
            var a = self._config.Auth;
            var configured = a.OAuthConfigured;
            var gl = (configured ? a.Authority : "https://oauth.invalid").TrimEnd('/');
            o.ClientId = configured ? a.ClientId : "unconfigured";
            o.ClientSecret = configured ? a.ClientSecret : "unconfigured";
            o.CallbackPath = string.IsNullOrWhiteSpace(a.CallbackPath) ? "/signin-gitlab" : a.CallbackPath;
            o.AuthorizationEndpoint = gl + "/oauth/authorize";
            o.TokenEndpoint = gl + "/oauth/token";
            o.UserInformationEndpoint = gl + "/api/v4/user";
            o.Scope.Add("read_user");
            o.SaveTokens = false;
            // Self-hosted à certificat auto-signé / CA interne : relâcher la validation TLS du BACKCHANNEL OAuth
            // (sinon l'échange code→token ET l'appel /api/v4/user échouent). Lu EN DIRECT : flag Auth posé par
            // /api/setup/oauth (bootstrap, avant que le serveur ne soit enregistré), ou repli sur le serveur
            // correspondant à l'autorité une fois configuré. Reconstruit à chaud à l'invalidation du cache d'options.
            var relaxTls = a.AllowSelfSignedCertificates
                || (configured && (self.ServerForInstance(a.Authority)?.AllowSelfSignedCertificates ?? false));
            if (relaxTls)
                o.BackchannelHttpHandler = new SocketsHttpHandler
                {
                    SslOptions = { RemoteCertificateValidationCallback = (_, _, _, _) => true }
                };
            o.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
            o.ClaimActions.MapJsonKey(ClaimTypes.Name, "username");
            o.ClaimActions.MapJsonKey("display_name", "name");
            o.ClaimActions.MapJsonKey(ClaimTypes.Email, "email");
            o.Events.OnCreatingTicket = async ctx =>
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, ctx.Options.UserInformationEndpoint);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ctx.AccessToken);
                using var resp = await ctx.Backchannel.SendAsync(req, ctx.HttpContext.RequestAborted);
                resp.EnsureSuccessStatusCode();
                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                ctx.RunClaimActions(doc.RootElement);
                var uname = ctx.Identity?.Name ?? "";
                // Bootstrap (non configuré) : tout compte GitLab valide passe (il deviendra l'admin). Gardé par
                // !IsConfigured() : une fois Auth.AdminUsers écrit, la vérification d'appartenance redevient obligatoire.
                if (self.IsConfigured())
                {
                    var oauthServer = self.ServerForInstance(self._config.Auth.Authority);
                    if (!self.IsAdminLogin(uname) && (oauthServer == null || await self.GetServerAccessLevelAsync(oauthServer, uname, ctx.HttpContext.RequestAborted) == null))
                        ctx.Fail("Compte GitLab non membre des projets du serveur.");
                    else if (oauthServer != null)
                        ctx.Identity?.AddClaim(new Claim(ServerClaim, oauthServer.Id));
                }
            };
            o.Events.OnRemoteFailure = ctx =>
            {
                // Échec/annulation : revenir à la cible de retour si connue (en popup → /auth/popup-done, qui
                // referme la fenêtre et laisse l'ouvrant relire /api/me). Sinon /login. Validé anti open-redirect.
                ctx.Response.Redirect(SafeLocalReturn(ctx.Properties?.RedirectUri, "/login"));
                ctx.HandleResponse();
                return Task.CompletedTask;
            };
        });
        builder.Services.AddAuthorization(o =>
        {
            // Tout exige une authentification, sauf endpoints marqués AllowAnonymous (/login, /auth/oauth, /api/auth/token).
            o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        });

        // Localisation FR/EN (i18n hybride : le serveur choisit la culture, le client rend les chaînes).
        // Sélection : cookie .AspNetCore.Culture (posé par /set-lang) → Accept-Language ; défaut FR.
        builder.Services.AddLocalization();
        builder.Services.Configure<RequestLocalizationOptions>(o =>
        {
            var cultures = Kpi.Localization.Loc.Supported.Select(c => new CultureInfo(c)).ToArray();
            o.DefaultRequestCulture = new RequestCulture(Kpi.Localization.Loc.Default);
            o.SupportedCultures = cultures;
            o.SupportedUICultures = cultures;
        });

        // Derrière un reverse proxy : récupérer le vrai schéma (https), l'hôte et l'IP cliente.
        // L'app étant liée à localhost, seul le proxy l'atteint → on fait confiance aux en-têtes transmis.
        builder.Services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
            // Ne faire confiance aux en-têtes XFF/Proto/Host QUE s'ils proviennent du reverse proxy
            // (loopback, même hôte). Sinon un client pourrait usurper son IP (rate-limit) ou l'hôte
            // (anti-CSRF). Si le proxy est sur une autre machine, ajouter son IP réelle ici.
            o.KnownIPNetworks.Clear();
            o.KnownProxies.Clear();
            o.KnownProxies.Add(System.Net.IPAddress.Loopback);
            o.KnownProxies.Add(System.Net.IPAddress.IPv6Loopback);
            o.ForwardLimit = 1;
        });

        // Rate-limit anti-bruteforce sur le login (par IP cliente).
        builder.Services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(
                ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { Window = TimeSpan.FromMinutes(1), PermitLimit = 10, QueueLimit = 0 }));
        });

        var app = builder.Build();

        app.UseForwardedHeaders();
        // Pose CultureInfo.CurrentUICulture (cookie/Accept-Language) AVANT auth/endpoints → vues localisées.
        app.UseRequestLocalization();

        // En-têtes de sécurité (anti-sniff, anti-clickjacking, CSP). 'unsafe-inline' requis car le
        // dashboard embarque scripts/styles inline ; le reste verrouille les sources.
        app.Use(async (ctx, next) =>
        {
            var h = ctx.Response.Headers;
            h["X-Content-Type-Options"] = "nosniff";
            h["X-Frame-Options"] = "DENY";
            h["Referrer-Policy"] = "same-origin";
            h["Content-Security-Policy"] =
                "default-src 'self'; script-src 'self' 'unsafe-inline' 'unsafe-eval'; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
                "font-src 'self' https://fonts.gstatic.com; " +
                "img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
            h["Permissions-Policy"] = "geolocation=(), microphone=(), camera=(), payment=(), usb=()";
            if (ctx.Request.IsHttps)
                h["Strict-Transport-Security"] = "max-age=63072000; includeSubDomains";
            await next();
        });

        // Protection CSRF : sur une requête modifiante, si l'en-tête Origin est présent il DOIT
        // correspondre à l'hôte. (Le cookie SameSite=Lax bloque déjà le POST cross-site authentifié.)
        app.Use(async (ctx, next) =>
        {
            var m = ctx.Request.Method;
            if (HttpMethods.IsPost(m) || HttpMethods.IsPut(m) || HttpMethods.IsDelete(m))
            {
                var origin = ctx.Request.Headers["Origin"].ToString();
                if (!string.IsNullOrEmpty(origin))
                {
                    if (!Uri.TryCreate(origin, UriKind.Absolute, out var ou)
                        || !string.Equals(ou.Host, ctx.Request.Host.Host, StringComparison.OrdinalIgnoreCase))
                    {
                        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                        await ctx.Response.WriteAsync("Origine non autorisée (CSRF).");
                        return;
                    }
                }
                else
                {
                    // Pas d'Origin : exiger Sec-Fetch-Site same-origin/none (posé par le navigateur,
                    // non falsifiable par du JS cross-site). Absent (très vieux client) → refus prudent.
                    var sfs = ctx.Request.Headers["Sec-Fetch-Site"].ToString();
                    if (sfs != "same-origin" && sfs != "none")
                    {
                        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                        await ctx.Response.WriteAsync("Origine non autorisée (CSRF).");
                        return;
                    }
                }
            }
            await next();
        });

        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();

        // Les handlers async renvoyant Task<IResult> avec un paramètre HttpContext DOIVENT être typés
        // explicitement en Func<HttpContext, Task<IResult>> : sinon ils se lient à l'overload
        // RequestDelegate (Func<HttpContext, Task>) et l'IResult est IGNORÉ (réponse 200 vide).
        // Changement de langue : pose le cookie de culture puis revient à la page d'origine.
        // Anonyme (utilisable avant login). @return validé chemin local (anti open-redirect).
        app.MapGet("/set-lang", (HttpContext ctx, string lang, string? @return) =>
        {
            var c = Kpi.Localization.Loc.Normalize(lang); // valide contre la liste des langues supportées
            ctx.Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(c)),
                new CookieOptions { HttpOnly = false, SameSite = SameSiteMode.Lax, MaxAge = TimeSpan.FromDays(365), IsEssential = true });
            var back = SafeLocalReturn(@return);
            return Results.Redirect(back);
        }).AllowAnonymous();

        app.MapGet("/", (Func<HttpContext, Task<IResult>>)(ctx => self.ServeHtmlAsync(ctx)));
        app.MapGet("/index.html", (Func<HttpContext, Task<IResult>>)(ctx => self.ServeHtmlAsync(ctx)));
        // Banc d'isolation des composants — DEV uniquement, données fictives (fixtures), aucun secret.
        if (app.Environment.IsDevelopment())
            app.MapGet("/gallery", () => Results.Content(Kpi.Views.GalleryView.Page(), "text/html; charset=utf-8")).AllowAnonymous();
        app.MapGet("/api/status", () => Results.Json(self._state.Snapshot()));
        app.MapPost("/api/cancel", (HttpContext ctx) => self.CancelAsync(ctx));
        app.MapPost("/api/refresh", (Func<HttpContext, Task<IResult>>)(ctx => self.RefreshAsync(ctx)));
        // Édition de la config depuis le dashboard (ADMIN, cf. RequireAdmin) : listing live des projets/labels
        // (via le token de groupe STOCKÉ) + sauvegarde des sections Export.* (projets/phases/labels).
        app.MapGet("/api/options/projects", (Func<HttpContext, Task<IResult>>)(ctx => self.OptionsProjectsAsync(ctx)));
        app.MapGet("/api/options/labels", (Func<HttpContext, Task<IResult>>)(ctx => self.OptionsLabelsAsync(ctx)));
        app.MapGet("/api/options/milestones", (Func<HttpContext, Task<IResult>>)(ctx => self.OptionsMilestonesAsync(ctx)));
        app.MapPost("/api/options/worktime", (Func<HttpContext, Task<IResult>>)(ctx => self.SaveWorkTimeAsync(ctx)));
        app.MapPost("/api/options", (Func<HttpContext, Task<IResult>>)(ctx => self.SaveOptionsAsync(ctx)));
        // Identité résolue de l'utilisateur courant (rôle/périmètre/vue).
        app.MapGet("/api/me", (HttpContext ctx) => Results.Json(self.ResolveMe(ctx)));
        // Données du dashboard FILTRÉES selon le compte (cœur de la restriction côté serveur).
        app.MapGet("/api/data", (Func<HttpContext, Task<IResult>>)(ctx => self.ServeDataAsync(ctx)));

        // Assistant de mise en service. OUVERT (AllowAnonymous) tant que l'instance n'est PAS configurée
        // (1re mise en service après un clone : l'assistant capture l'admin + la config). Une fois
        // configuré → ADMIN-ONLY (la section Auth est alors verrouillée, plus d'escalade via l'app).
        app.MapGet("/setup", (HttpContext ctx) =>
        {
            if (!self.IsConfigured() || self.IsAdminLogin(ctx.User.Identity?.Name))
                return Results.Content(SetupView.Page(self._config.Auth, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName), "text/html; charset=utf-8");
            return (ctx.User.Identity?.IsAuthenticated ?? false) ? Results.Redirect("/") : Results.Redirect("/login");
        }).AllowAnonymous();
        // Endpoints de l'assistant : ouverts au bootstrap (non configuré), sinon admin-only (RequireSetupAccess).
        app.MapPost("/api/setup/test",   (Func<HttpContext, Task<IResult>>)(ctx => self.SetupTestAsync(ctx))).AllowAnonymous();
        app.MapPost("/api/setup/labels", (Func<HttpContext, Task<IResult>>)(ctx => self.SetupLabelsAsync(ctx))).AllowAnonymous();
        app.MapPost("/api/setup/oauth",  (Func<HttpContext, Task<IResult>>)(ctx => self.SetupOAuthSaveAsync(ctx))).AllowAnonymous();
        app.MapPost("/api/setup",        (Func<HttpContext, Task<IResult>>)(ctx => self.SetupSaveAsync(ctx))).AllowAnonymous();
        app.MapGet("/api/setup/progress", (HttpContext ctx) => self.SetupProgress(ctx)).AllowAnonymous();
        app.MapPost("/api/setup/cancel",  (HttpContext ctx) => self.CancelAsync(ctx));

        // --- Endpoints d'authentification ---
        // Page de connexion designée (OAuth + token) = page d'accueil. Déjà connecté → dashboard.
        app.MapGet("/login", (HttpContext ctx) =>
        {
            // Sinon on AFFICHE la page, y compris à la 1re mise en service (non configuré) : elle montre
            // alors un CTA « Commencer la configuration » → /setup (pas de connexion possible sans serveur).
            if (ctx.User.Identity?.IsAuthenticated ?? false) return Results.Redirect("/");
            return Results.Content(LoginView.Page(self._config.Auth, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, self.IsConfigured()), "text/html; charset=utf-8");
        }).AllowAnonymous().RequireRateLimiting("login");

        // Bouton « Se connecter avec GitLab » → challenge OAuth de l'instance configurée (Auth.Authority).
        app.MapGet("/auth/oauth", (HttpContext ctx, string? @return) =>
        {
            if (!self._config.Auth.OAuthConfigured) return Results.Redirect("/login");
            // @return validé chemin local (anti open-redirect) : permet à /setup de revenir au wizard après OAuth.
            var back = SafeLocalReturn(@return);
            return Results.Challenge(new AuthenticationProperties { RedirectUri = back }, new[] { "gitlab" });
        }).AllowAnonymous().RequireRateLimiting("login");

        app.MapGet("/logout", async (HttpContext ctx, string? @return) =>
        {
            await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            // @return validé chemin local (anti open-redirect) : permet au setup de revenir au wizard
            // après déconnexion (bouton « Modifier » l'admin → réaffiche l'écran de connexion).
            var back = SafeLocalReturn(@return);
            return Results.Redirect(back);
        });

        // Cible de retour du SSO en POPUP (cf. setup). Après le callback OAuth, le navigateur (dans la popup)
        // atterrit ici : on prévient la fenêtre parente (postMessage same-origin) puis on ferme la popup. Si la
        // page n'est PAS une popup (ouverture plein écran / repli), on revient au /setup au bout d'un court délai.
        // Page volontairement MUETTE (fond sombre, aucun texte visible) : elle ne vit que quelques
        // dizaines de ms avant fermeture — un message affiché d'emblée donnait un rendu peu
        // professionnel. Le texte n'apparaît qu'en dernier recours, si la fenêtre n'a pas pu se
        // fermer après ~900 ms (fermeture bloquée par le navigateur).
        app.MapGet("/auth/popup-done", () => Results.Content(
            "<!doctype html><html><head><meta charset=\"utf-8\"><title>GitLab</title></head>" +
            "<body style=\"margin:0;background:#0a0e13;color:#9aa6b6;font:14px system-ui,'Segoe UI',sans-serif;display:flex;align-items:center;justify-content:center;height:100vh\">" +
            "<div id=\"m\" style=\"opacity:0;transition:opacity .25s\">Connexion GitLab terminée — vous pouvez fermer cette fenêtre.</div>" +
            "<script>(function(){try{if(window.opener&&!window.opener.closed){window.opener.postMessage({kpiOauth:'done'},window.location.origin);}}catch(e){}" +
            "try{window.close();}catch(e){}" +
            "setTimeout(function(){try{window.close();}catch(e){}try{if(!window.opener){window.location.replace('/setup');return;}}catch(e){window.location.replace('/setup');return;}" +
            "var m=document.getElementById('m');if(m)m.style.opacity='1';},900);})();</script>" +
            "</body></html>", "text/html; charset=utf-8")).AllowAnonymous();

        Console.WriteLine("=== Dashboard server (ASP.NET Core) prêt ===");
        Console.WriteLine($"Ouvrir : http://localhost:{port}/");
        if (authCfg.OAuthConfigured) Console.WriteLine($"Auth   : OAuth GitLab ({authCfg.Authority}) · callback {authCfg.CallbackPath}");
        else Console.WriteLine("Auth   : connexion par token GitLab (page /login) — OAuth non configuré.");
        Console.WriteLine($"Rôles  : accès réservé aux membres GitLab des projets configurés ({config.ResolveServers().Count} serveur(s)) · admins (fichier serveur) : {string.Join(", ", authCfg.AdminUsers)}");
        Console.WriteLine("Ctrl+C pour arrêter.");

        // Arrêt propre quand le token de Program.cs (Ctrl+C) est annulé.
        ct.Register(() => { try { app.Lifetime.StopApplication(); } catch { } });
        await app.RunAsync();
    }

    /// <summary>
    /// Chemin de retour LOCAL sûr (anti open-redirect). Accepte uniquement un chemin commençant par '/'
    /// qui n'est PAS protocol-relative : refuse « //host » ET « /\host » (les navigateurs normalisent '\'
    /// en '/', donc « /\host » devient « //host » → redirection externe). Sinon repli sur <paramref name="fallback"/>.
    /// </summary>
    private static string SafeLocalReturn(string? value, string fallback = "/")
    {
        if (string.IsNullOrEmpty(value) || value[0] != '/') return fallback;
        if (value.Length >= 2 && (value[1] == '/' || value[1] == '\\')) return fallback;
        return value;
    }

    /// <summary>Slug de projet (dernier segment du chemin) dérivé d'un webUrl d'issue GitLab
    /// (https://host/groupe/projet/-/issues/123 → « projet »). Repli pour nommer un projet sans nom persisté.</summary>
    private static string ProjectSlugFromWebUrl(string url)
    {
        var m = Regex.Match(url ?? "", @"^https?://[^/]+/(.+?)/-/issues/");
        if (!m.Success) return "";
        var segs = m.Groups[1].Value.Split('/');
        return segs.Length > 0 ? segs[^1] : "";
    }

    // --- Endpoints ------------------------------------------------------

    private async Task<IResult> ServeHtmlAsync(HttpContext ctx)
    {
        // Rôles GitLab : une session dont l'accès au projet a été révoqué est déconnectée.
        if (!await IsAllowedAsync(ctx))
        {
            await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Redirect("/login");
        }
        // Première mise en service : tant que la connexion GitLab n'est pas configurée, on ne sert
        // jamais un dashboard vide → l'admin (seul autorisé ici à ce stade) est envoyé vers /setup.
        if (!IsConfigured()) return Results.Redirect("/setup");
        // Dashboard = app de référence Claude Design ; le payload réel (filtré par compte) est
        // inliné et window.APP est construit par le mapper AVANT le rendu React.
        var json = await BuildScopedPayloadAsync(ctx);
        var lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName; // "fr"/"en" (posée par UseRequestLocalization)
        return Results.Content(DashboardView.BuildReferencePage(json, lang), "text/html; charset=utf-8");
    }

    private IResult CancelAsync(HttpContext ctx)
    {
        var deny = RequireAdmin(ctx); if (deny != null) return deny;
        CancellationTokenSource? cts;
        lock (_ctsLock) { cts = _refreshCts; }
        if (cts != null && !cts.IsCancellationRequested)
        {
            try { cts.Cancel(); } catch { }
            return Results.Text("Annulation demandée.", "text/plain; charset=utf-8");
        }
        return Results.Text("Aucune acquisition en cours.", "text/plain; charset=utf-8");
    }

    private async Task<IResult> RefreshAsync(HttpContext ctx)
    {
        var deny = RequireAdmin(ctx); if (deny != null) return deny;
        if (!await _refreshLock.WaitAsync(0))
            return Results.Text("Une acquisition est déjà en cours.", "text/plain; charset=utf-8", Encoding.UTF8, statusCode: 409);

        List<string> milestonesToRefresh = new();
        string? projectFilter = null;
        try
        {
            using var reader = new StreamReader(ctx.Request.Body);
            var body = await reader.ReadToEndAsync();
            if (!string.IsNullOrWhiteSpace(body))
            {
                var parsed = JsonSerializer.Deserialize<RefreshRequest>(body);
                if (parsed?.milestones != null && parsed.milestones.Count > 0)
                    milestonesToRefresh.AddRange(parsed.milestones.Where(m => !string.IsNullOrWhiteSpace(m)));
                if (!string.IsNullOrWhiteSpace(parsed?.project))
                    projectFilter = parsed!.project!.Trim();
            }
        }
        catch { /* body invalide → refresh complet */ }

        var appStopping = ctx.RequestServices.GetService(typeof(IHostApplicationLifetime)) as IHostApplicationLifetime;
        var serverCt = appStopping?.ApplicationStopping ?? CancellationToken.None;
        _ = Task.Run(() => RunRefreshAsync(milestonesToRefresh, projectFilter, serverCt));
        return Results.Text("Acquisition démarrée.", "text/plain; charset=utf-8", Encoding.UTF8, statusCode: 202);
    }

    private async Task RunRefreshAsync(List<string> milestonesToRefresh, string? projectFilter, CancellationToken serverCt)
    {
        CancellationTokenSource localCts;
        CancellationTokenSource linked;
        lock (_ctsLock)
        {
            localCts = new CancellationTokenSource();
            _refreshCts = localCts;
        }
        linked = CancellationTokenSource.CreateLinkedTokenSource(serverCt, localCts.Token);
        try
        {
            _state.Reset();
            _state.Running = true;
            _state.StartedAt = DateTime.UtcNow;

            if (_config.Servers is { Count: > 0 })
            {
                // v2 multi-serveurs : extraction cloisonnée + chiffrée, CIBLABLE par projet et/ou
                // milestone (merge du store : seule la portée demandée est remplacée).
                if (milestonesToRefresh.Count == 0)
                    await ExportPipeline.RunMultiServerExportAsync(_config, (i, t) => { _state.Current = i; _state.Total = t; }, linked.Token, projectFilter);
                else
                    for (int i = 0; i < milestonesToRefresh.Count; i++)
                    {
                        linked.Token.ThrowIfCancellationRequested();
                        Console.WriteLine($"[Refresh] Milestone {i + 1}/{milestonesToRefresh.Count} : {milestonesToRefresh[i]}");
                        await ExportPipeline.RunMultiServerExportAsync(_config, (cur, tot) => { _state.Current = cur; _state.Total = tot; }, linked.Token, projectFilter, milestonesToRefresh[i]);
                    }
            }
            else
            {
                // Aucun serveur v2 configuré : refus explicite. (L'ancien repli mono-serveur RunFullExportAsync
                // écrivait des exports EN CLAIR à la racine de output/ — retiré : le web ne déclenche plus
                // que le pipeline multi-serveurs chiffré. Les commandes CLI restent disponibles.)
                _state.LastError = "Aucun serveur GitLab configuré — terminez le /setup avant de rafraîchir.";
                Console.Error.WriteLine("[Refresh] refusé : aucun serveur configuré (Servers vide).");
            }

            _state.LastRefreshAt = DateTime.UtcNow;
            _state.LastError = null;
        }
        catch (OperationCanceledException)
        {
            _state.LastError = "Annulé par l'utilisateur.";
            Console.WriteLine("Refresh annulé.");
        }
        catch (Exception ex)
        {
            _state.LastError = ex.Message;
            Console.Error.WriteLine("Refresh KO : " + ex);
        }
        finally
        {
            // Données régénérées → invalider le cache des payloads.
            _payloadCache.Clear();
            _state.Running = false;
            _refreshLock.Release();
            linked.Dispose();
            lock (_ctsLock) { if (_refreshCts == localCts) _refreshCts = null; }
            localCts.Dispose();
        }
    }

    // --- Helpers (identiques à l'ancien serveur) ------------------------

    private static string RuntimeConfigPath() => Path.Combine(AppContext.BaseDirectory, "appsettings.json");

    /// <summary>Migrations de config au repos (appsettings.json) au démarrage :
    /// (1) chiffre les secrets restés en clair (enc:v1:…) ; (2) sème les labels transversaux historiques
    /// SI la clé est ABSENTE (rétro-compat — une liste vide EXPLICITE = choix admin, respectée).
    /// Idempotent, atomique, best-effort. Synchronise aussi la config EN MÉMOIRE pour le 1er payload.</summary>
    private static async Task MigrateConfigAtRestAsync(AppConfig config)
    {
        try
        {
            var path = RuntimeConfigPath();
            if (!File.Exists(path)) return; // 1re mise en service : rien à migrer
            if (JsonNode.Parse(await File.ReadAllTextAsync(path)) is not JsonObject root) return;
            var changed = false;
            void Enc(JsonObject o, string key)
            {
                var v = o[key]?.GetValue<string>() ?? "";
                var enc = SecureStore.ProtectSecret(v);
                if (enc != v) { o[key] = enc; changed = true; }
            }
            if (root["Servers"] is JsonArray servers)
                foreach (var s in servers)
                    if (s is JsonObject so && so["GroupToken"] != null) Enc(so, "GroupToken");
            if (root["Auth"] is JsonObject auth && auth["ClientSecret"] != null) Enc(auth, "ClientSecret");

            // Semis rétro-compat des labels transversaux : seulement si la CLÉ est absente. Une fois écrite
            // (même []), on ne re-sème plus → « tout retirer » reste stable au redémarrage.
            if (root["Export"] is JsonObject ex && ex["TransversalLabels"] == null)
            {
                var defaults = new[] { "CONTRACTUAL", "Unplanned", "Surcharge QA" };
                ex["TransversalLabels"] = new JsonArray(defaults.Select(s => JsonValue.Create(s)).ToArray());
                config.Export.TransversalLabels = defaults.ToList(); // synchro mémoire (1er payload sans redémarrage)
                changed = true;
                Console.WriteLine("[Migration] Labels transversaux : défauts historiques semés (rétro-compat, éditables dans Options).");
            }

            if (!changed) return;
            await WriteFileAtomicAsync(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine("[Config] appsettings.json migré au repos.");
        }
        catch (Exception ex) { Console.Error.WriteLine("[Config] Migration au repos impossible : " + ex.Message); }
    }

    private static string? SourceConfigPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Kpi.csproj")))
            {
                var p = Path.Combine(dir.FullName, "appsettings.json");
                return File.Exists(p) ? p : null;
            }
            dir = dir.Parent;
        }
        return null;
    }

    private string AccountsPath() => Path.Combine(_config.Export.OutputDirectory, "accounts.json");

    // Écriture atomique (tmp + rename) : aucun lecteur concurrent ne voit un fichier tronqué.
    private static async Task WriteFileAtomicAsync(string path, string content)
    {
        var tmp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        await File.WriteAllTextAsync(tmp, content);
        File.Move(tmp, path, overwrite: true);
    }

    // /api/data : payload du dashboard RESTREINT au périmètre du compte (filtrage côté serveur).
    private async Task<IResult> ServeDataAsync(HttpContext ctx)
    {
        // Rôles GitLab : un membre révoqué perd l'accès aux données (cache 5 min).
        if (!await IsAllowedAsync(ctx))
            return Results.Text("Accès au projet révoqué.", "text/plain; charset=utf-8", Encoding.UTF8, StatusCodes.Status403Forbidden);
        return Results.Content(await BuildScopedPayloadAsync(ctx), "application/json; charset=utf-8");
    }

    // Construit le payload JSON filtré par compte (réutilisé par /api/data ET le dashboard inliné).
    private async Task<string> BuildScopedPayloadAsync(HttpContext ctx)
    {
        var login = EffectiveLogin(ctx, out _);
        var r = ResolveAccount(login);
        var cfg = _config; // snapshot cohérent (config rechargeable à chaud)

        // Serveur courant = celui de la session (claim posé au login), repli sur le 1er configuré.
        var serverId = ServerById(ctx.User.FindFirst(ServerClaim)?.Value)?.Id ?? cfg.ResolveServers().FirstOrDefault()?.Id ?? "default";
        var (dataDir, encrypted) = ResolveServerDataDir(cfg.Export.OutputDirectory, serverId);

        // Cache du JSON produit : clé = serveur + périmètre du compte, signature = mtimes des fichiers source.
        string Mtime(string f) { var fp = Path.Combine(dataDir, f); return File.Exists(fp) ? File.GetLastWriteTimeUtc(fp).Ticks.ToString() : "0"; }
        var cacheKey = $"{serverId}|{r.ScopeType}|{r.ScopeValue}|{string.Join(',', r.Milestones)}|{string.Join(',', r.Labels)}|{r.Role}";
        var sig = $"{Mtime("issues.json")}.{Mtime("labels.json")}.{Mtime("milestones.json")}";
        if (_payloadCache.TryGetValue(cacheKey, out var cached) && cached.sig == sig) return cached.json;

        var (all, labels, milestones, lastExtracted) = await LoadServerDataAsync(serverId, dataDir, encrypted, ctx.RequestAborted);

        IEnumerable<IssueExport> filtered = all;
        if (r.ScopeType == "user" && !string.IsNullOrEmpty(r.ScopeValue))
            filtered = filtered.Where(e => e.Assignees != null && e.Assignees.Any(a => string.Equals(a, r.ScopeValue, StringComparison.OrdinalIgnoreCase)));
        else if (r.ScopeType == "team" && !string.IsNullOrEmpty(r.ScopeValue))
        {
            cfg.Export.Teams.TryGetValue(r.ScopeValue, out var members);
            var set = new HashSet<string>(members ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            filtered = filtered.Where(e => e.Assignees != null && e.Assignees.Any(a => set.Contains(a)));
        }
        if (r.Milestones.Count > 0)
        {
            var ms = new HashSet<string>(r.Milestones, StringComparer.OrdinalIgnoreCase);
            filtered = filtered.Where(e => e.Milestone != null && ms.Contains(e.Milestone));
        }
        if (r.Labels.Count > 0)
        {
            var lbl = new HashSet<string>(r.Labels, StringComparer.OrdinalIgnoreCase);
            filtered = filtered.Where(e => e.Labels != null && e.Labels.Any(l => lbl.Contains(l)));
        }

        Dictionary<string, List<string>> scopedTeams;
        if (r.Role == "admin") scopedTeams = cfg.Export.Teams;
        else if (r.ScopeType == "team" && !string.IsNullOrEmpty(r.ScopeValue) && cfg.Export.Teams.TryGetValue(r.ScopeValue, out var tm))
            scopedTeams = new Dictionary<string, List<string>> { [r.ScopeValue] = tm };
        else scopedTeams = new Dictionary<string, List<string>>();

        // --- Bloc « setup » (lecture seule) pour l'onglet Options : reflet de la config /setup ---
        // Noms de projets : priorité à Export.Projects (persisté) ; repli = slug dérivé du webUrl des issues.
        var nameById = new Dictionary<int, string>();
        foreach (var e in all)
        {
            var pid = (int)e.ProjectId;
            if (pid > 0 && !nameById.ContainsKey(pid) && !string.IsNullOrEmpty(e.WebUrl))
            {
                var nm = ProjectSlugFromWebUrl(e.WebUrl!);
                if (nm.Length > 0) nameById[pid] = nm;
            }
        }
        string ProjName(int id, string? saved) =>
            !string.IsNullOrWhiteSpace(saved) ? saved! : (nameById.TryGetValue(id, out var n) ? n : "#" + id);
        static bool RealPeriod(PeriodDefinition p) => !string.IsNullOrEmpty(p.Key) && !string.Equals(p.Key, "none", StringComparison.OrdinalIgnoreCase);
        // Migration Piste 2 : rôle depuis Role si présent, sinon dérivé de Timed + EffectivePhases (repli dev/review/qa/tofix).
        var effActive = (cfg.Export.EffectivePhases is { Count: > 0 }) ? cfg.Export.EffectivePhases : new List<string> { "dev", "review", "qa", "tofix" };
        var effSet = new HashSet<string>(effActive, StringComparer.OrdinalIgnoreCase);
        string RoleOf(PeriodDefinition p)
        {
            var r = (p.Role ?? "").Trim().ToLowerInvariant();
            if (r == "active" || r == "wait" || r == "nogc") return r;
            return !p.Timed ? "nogc" : (effSet.Contains(p.Key) ? "active" : "wait");
        }
        PeriodDefinition WithRole(PeriodDefinition p) { var role = RoleOf(p); return new PeriodDefinition { Key = p.Key, Name = p.Name, Color = p.Color, Role = role, Timed = role != "nogc" }; }
        object PeriodObj(PeriodDefinition p) { var role = RoleOf(p); return new { key = p.Key, name = string.IsNullOrWhiteSpace(p.Name) ? p.Key : p.Name, color = string.IsNullOrWhiteSpace(p.Color) ? "#cccccc" : p.Color, role, timed = role != "nogc" }; }
        var rolePeriods = (cfg.Export.Periods ?? new()).Select(WithRole).ToList();
        List<object> setupProjects = (cfg.Export.Projects is { Count: > 0 })
            ? cfg.Export.Projects.Select(p => (object)new { id = p.Id, name = ProjName(p.Id, p.Name), group = p.Group ?? "", imported = true }).ToList()
            : (cfg.Export.ProjectIds ?? new()).Select(id => (object)new { id, name = ProjName(id, null), group = "", imported = true }).ToList();
        var setup = new
        {
            isAdmin = string.Equals(r.Role, "admin", StringComparison.OrdinalIgnoreCase), // gate des actions admin (régénération, reconfigurer)
            projects = setupProjects,
            periods = (cfg.Export.Periods ?? new()).Where(RealPeriod).Select(PeriodObj).ToList(),
            periodsByProject = (cfg.Export.PeriodsByProject ?? new()).ToDictionary(kv => kv.Key.ToString(), kv => kv.Value.Where(RealPeriod).Select(PeriodObj).ToList()),
            labelPhases = cfg.Export.LabelPhases ?? new(),
            labelPhasesByProject = (cfg.Export.LabelPhasesByProject ?? new()).ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
            teams = scopedTeams.Select(kv => new { name = kv.Key, group = (cfg.Export.TeamGroups != null && cfg.Export.TeamGroups.TryGetValue(kv.Key, out var g)) ? g : "", members = kv.Value }).ToList(),
            // Équipes par projet — réservé à l'admin (rosters par projet ; les non-admins n'ont que leurs équipes scopées).
            teamsByProject = string.Equals(r.Role, "admin", StringComparison.OrdinalIgnoreCase)
                ? (cfg.Export.TeamsByProject ?? new()).ToDictionary(kv => kv.Key.ToString(), kv => (object)kv.Value.Select(t => new { name = t.Key, members = t.Value }).ToList())
                : new Dictionary<string, object>(),
            trackedLabels = cfg.Export.TrackedLabels ?? new(),
            transversalLabels = cfg.Export.TransversalLabels ?? new(),
        };

        // Dashboard modulaire → objet camelCase pour le payload (mapper : window.APP.pages). Vide ⇒ pages [].
        var dash = cfg.Dashboard ?? new DashboardConfig();
        var dashboard = new
        {
            schemaVersion = dash.SchemaVersion,
            defaultPageId = dash.DefaultPageId ?? "",
            pages = (dash.Pages ?? new()).Select(p => new
            {
                id = p.Id, kind = p.Kind,
                nav = new { label = p.Nav.Label, labelKey = p.Nav.LabelKey, icon = p.Nav.Icon, order = p.Nav.Order, showFilters = p.Nav.ShowFilters, badgeSource = p.Nav.BadgeSource },
                layout = new { cols = p.Layout.Cols, gap = p.Layout.Gap, rowUnit = p.Layout.RowUnit },
                widgets = (p.Widgets ?? new()).Select(w => new
                {
                    id = w.Id, type = w.Type, data = w.Data,
                    layout = new { w = w.Layout.W, h = w.Layout.H, x = w.Layout.X, y = w.Layout.Y },
                    @params = w.Params ?? new(),
                }).ToList(),
            }).ToList(),
        };

        var json = DashboardView.BuildPayloadJson(
            "", filtered.ToList(), // v2 : pas de milestone global (filtre UI) ; le payload couvre toutes les issues du périmètre
            // ?? new() : même garde que le bloc setup ci-dessus — un JSON édité à la main peut rendre
            // ces propriétés réellement nulles malgré l'annotation (et le ?? voisin fait considérer au
            // compilateur qu'elles peuvent l'être → CS8604 sans cette garde).
            scopedTeams, cfg.Export.LabelPhases ?? new(), rolePeriods,
            labels, milestones, lastExtracted, setup,
            new { startHour = cfg.Export.WorkStartHour, endHour = cfg.Export.WorkEndHour, workingDaysOnly = cfg.Export.WorkingDaysOnly, holidays = cfg.Export.Holidays ?? new(), minPhaseMinutes = cfg.Export.MinPhaseMinutes },
            cfg.Export.TransversalLabels ?? new(),
            dashboard);
        _payloadCache[cacheKey] = (sig, json);
        return json;
    }

    // Cache du payload JSON sérialisé par serveur+périmètre+signature de fichiers (cf. BuildScopedPayloadAsync).
    private static readonly ConcurrentDictionary<string, (string sig, string json)> _payloadCache = new();

    // Dossier de données d'un serveur : output/<serverId>/ s'il a des données (chiffrées), sinon repli
    // sur output/ (legacy, en clair) tant que la migration n'est pas faite → runtime jamais cassé.
    private static (string dir, bool encrypted) ResolveServerDataDir(string outputDirectory, string serverId)
    {
        var serverDir = Path.Combine(outputDirectory, serverId);
        if (File.Exists(Path.Combine(serverDir, "issues.json"))) return (serverDir, true);
        return (outputDirectory, false);
    }

    // Lit un fichier de données : déchiffré (SecureStore, sous-clé serveur) si dossier serveur, sinon en clair (legacy).
    private static string? ReadServerFile(string serverId, string dir, bool encrypted, string file)
    {
        var path = Path.Combine(dir, file);
        if (encrypted) return SecureStore.TryReadDecrypted(serverId, path);
        if (!File.Exists(path)) return null;
        try { using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite); using var sr = new StreamReader(fs); return sr.ReadToEnd(); }
        catch (Exception ex) { Console.Error.WriteLine($"Lecture {file} KO : {ex.Message}"); return null; }
    }

    private async Task<(List<IssueExport> issues, List<Kpi.GitLab.Models.GitLabLabel> labels, List<Kpi.GitLab.Models.GitLabMilestone> milestones, string lastExtracted)>
        LoadServerDataAsync(string serverId, string dir, bool encrypted, CancellationToken ct)
    {
        await Task.CompletedTask;
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        T? Parse<T>(string? txt) { if (txt == null) return default; try { return JsonSerializer.Deserialize<T>(txt, opts); } catch (Exception ex) { Console.Error.WriteLine($"JSON illisible : {ex.Message}"); return default; } }
        var issues = Parse<List<IssueExport>>(ReadServerFile(serverId, dir, encrypted, "issues.json")) ?? new();
        var labels = Parse<List<Kpi.GitLab.Models.GitLabLabel>>(ReadServerFile(serverId, dir, encrypted, "labels.json")) ?? new();
        var milestones = Parse<List<Kpi.GitLab.Models.GitLabMilestone>>(ReadServerFile(serverId, dir, encrypted, "milestones.json")) ?? new();
        var issuesPath = Path.Combine(dir, "issues.json");
        var lastExtracted = File.Exists(issuesPath) ? File.GetLastWriteTimeUtc(issuesPath).ToString("yyyy-MM-dd HH:mm") : "";
        return (issues, labels, milestones, lastExtracted);
    }

    private static AppConfig BuildConfig()
    {
        var b = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables(prefix: "KPI_");
        var cfg = new AppConfig();
        b.Build().Bind(cfg);
        // IConfiguration découpe les clés sur « : » → LabelPhases (clés « Prod::… ») est corrompu par Bind.
        // On relit ces maps telles quelles depuis le JSON, sinon le dashboard perd le mapping → cycle = 0 j.
        AppConfig.RepairColonKeyedMaps(cfg, AppContext.BaseDirectory);
        // Secrets au repos (enc:v1:…) → déchiffrés EN MÉMOIRE seulement (GroupToken, ClientSecret).
        Kpi.Export.SecureStore.UnprotectConfig(cfg);
        return cfg;
    }

    // --- État de refresh + payloads -------------------------------------

    private sealed class RefreshState
    {
        private readonly object _lock = new();
        private bool _running;
        private int _current;
        private int _total;
        private string? _lastError;
        private DateTime? _lastRefreshAt;
        private DateTime? _startedAt;

        public bool Running { get { lock (_lock) return _running; } set { lock (_lock) _running = value; } }
        public int Current { get { lock (_lock) return _current; } set { lock (_lock) _current = value; } }
        public int Total { get { lock (_lock) return _total; } set { lock (_lock) _total = value; } }
        public string? LastError { get { lock (_lock) return _lastError; } set { lock (_lock) _lastError = value; } }
        public DateTime? LastRefreshAt { get { lock (_lock) return _lastRefreshAt; } set { lock (_lock) _lastRefreshAt = value; } }
        public DateTime? StartedAt { get { lock (_lock) return _startedAt; } set { lock (_lock) _startedAt = value; } }

        public void Reset() { lock (_lock) { _current = 0; _total = 0; _lastError = null; } }

        public StateSnapshot Snapshot()
        {
            lock (_lock)
            {
                return new StateSnapshot
                {
                    running = _running,
                    current = _current,
                    total = _total,
                    lastError = _lastError,
                    lastRefreshAt = _lastRefreshAt?.ToString("yyyy-MM-dd HH:mm:ss"),
                    startedAt = _startedAt?.ToString("yyyy-MM-dd HH:mm:ss"),
                };
            }
        }
    }

    public sealed class StateSnapshot
    {
        public bool running { get; set; }
        public int current { get; set; }
        public int total { get; set; }
        public string? lastError { get; set; }
        public string? lastRefreshAt { get; set; }
        public string? startedAt { get; set; }
    }

    private sealed class RefreshRequest
    {
        public List<string>? milestones { get; set; }
        /// <summary>Id GitLab du projet à ré-extraire (chaîne). Vide/null = tous les projets configurés.</summary>
        public string? project { get; set; }
    }

}
