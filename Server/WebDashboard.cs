using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading.RateLimiting;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kpi.Config;
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
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kpi.Server;

/// <summary>
/// Serveur du dashboard sur ASP.NET Core (Kestrel). Remplace l'ancien HttpListener.
/// Étape 1 de la sécurisation : même comportement/endpoints qu'avant, sans authentification.
/// Conçu pour tourner derrière un reverse proxy (TLS géré en amont).
/// Les étapes suivantes ajouteront : OAuth GitLab, résolution de compte/rôles, /api/data filtré.
/// </summary>
public sealed class WebDashboard
{
    private volatile AppConfig _config; // rechargé à chaud via /api/config (volatile : visibilité multi-thread)
    private const string TokenSentinel = "********";
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
    private HttpClient SharedHttp => _config.GitLab.AllowSelfSignedCertificates ? _sharedHttpRelaxed : _sharedHttp;

    private WebDashboard(AppConfig config) => _config = config;

    public static async Task RunAsync(AppConfig config, int port, CancellationToken ct)
    {
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
        builder.Services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "dp-keys")))
            .SetApplicationName("Kpi");

        if (authCfg.OAuthConfigured)
        {
            var gl = authCfg.Authority.TrimEnd('/');
            authBuilder.AddOAuth("gitlab", o =>
            {
                o.ClientId = authCfg.ClientId;
                o.ClientSecret = authCfg.ClientSecret;
                o.CallbackPath = authCfg.CallbackPath;
                o.AuthorizationEndpoint = gl + "/oauth/authorize";
                o.TokenEndpoint = gl + "/oauth/token";
                o.UserInformationEndpoint = gl + "/api/v4/user";
                o.Scope.Add("read_user");
                o.SaveTokens = false;
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
                    // Rôles GitLab : refuser le ticket si l'utilisateur n'est ni membre du projet ni admin.
                    var uname = ctx.Identity?.Name ?? "";
                    if (!self.IsAdminLogin(uname) && await self.GetProjectAccessLevelAsync(uname, ctx.HttpContext.RequestAborted) == null)
                        ctx.Fail("Compte GitLab non membre du projet.");
                };
                // Échec du flux OAuth (dont refus ci-dessus) → retour propre à la page de connexion.
                o.Events.OnRemoteFailure = ctx =>
                {
                    ctx.Response.Redirect("/login");
                    ctx.HandleResponse();
                    return Task.CompletedTask;
                };
            });
        }
        builder.Services.AddAuthorization(o =>
        {
            // Tout exige une authentification, sauf endpoints marqués AllowAnonymous (/login, /auth/oauth, /api/auth/token).
            o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
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
        app.MapGet("/", (Func<HttpContext, Task<IResult>>)(ctx => self.ServeHtmlAsync(ctx)));
        app.MapGet("/index.html", (Func<HttpContext, Task<IResult>>)(ctx => self.ServeHtmlAsync(ctx)));
        // App de référence (Claude Design) — DONNÉES DE DÉMO. Exposée uniquement en développement.
        if (app.Environment.IsDevelopment())
            app.MapGet("/ref", () => Results.Content(Kpi.Views.HypervisorReleaseView.BuildReferencePage(), "text/html; charset=utf-8")).AllowAnonymous();
        app.MapGet("/api/status", () => Results.Json(self._state.Snapshot()));
        app.MapGet("/api/config", (Func<HttpContext, Task<IResult>>)(ctx => self.ServeConfigAsync(ctx)));
        app.MapGet("/api/config/token", (Func<HttpContext, Task<IResult>>)(ctx => self.ServeTokenAsync(ctx)));
        app.MapPost("/api/config", (Func<HttpContext, Task<IResult>>)(ctx => self.SaveConfigAsync(ctx)));
        // Comptes & vues : ADMIN-ONLY (étape 3).
        app.MapGet("/api/accounts", (Func<HttpContext, Task<IResult>>)(ctx => self.ServeAccountsAsync(ctx)));
        app.MapPost("/api/accounts", (Func<HttpContext, Task<IResult>>)(ctx => self.SaveAccountsAsync(ctx)));
        app.MapPost("/api/cancel", (HttpContext ctx) => self.CancelAsync(ctx));
        app.MapPost("/api/refresh", (Func<HttpContext, Task<IResult>>)(ctx => self.RefreshAsync(ctx)));
        // Identité résolue de l'utilisateur courant (rôle/périmètre/vue).
        app.MapGet("/api/me", (HttpContext ctx) => Results.Json(self.ResolveMe(ctx)));
        // Données du dashboard FILTRÉES selon le compte (cœur de la restriction côté serveur).
        app.MapGet("/api/data", (Func<HttpContext, Task<IResult>>)(ctx => self.ServeDataAsync(ctx)));

        // Assistant de première mise en service (admin-only, tant que la connexion GitLab est vide).
        app.MapGet("/setup", (HttpContext ctx) =>
        {
            if (self.IsConfigured()) return Results.Redirect("/");
            if (!self.IsAdminLogin(ctx.User.Identity?.Name)) return Results.Redirect("/login");
            return Results.Content(SetupView.Page(authCfg), "text/html; charset=utf-8");
        });
        app.MapPost("/api/setup/test",   (Func<HttpContext, Task<IResult>>)(ctx => self.SetupTestAsync(ctx)));
        app.MapPost("/api/setup/labels", (Func<HttpContext, Task<IResult>>)(ctx => self.SetupLabelsAsync(ctx)));
        app.MapPost("/api/setup",        (Func<HttpContext, Task<IResult>>)(ctx => self.SetupSaveAsync(ctx)));

        // --- Endpoints d'authentification ---
        // Page de connexion designée (OAuth + token). Déjà connecté → dashboard.
        app.MapGet("/login", (HttpContext ctx) =>
        {
            if (ctx.User.Identity?.IsAuthenticated ?? false) return Results.Redirect("/");
            return Results.Content(LoginView.Page(authCfg), "text/html; charset=utf-8");
        }).AllowAnonymous().RequireRateLimiting("login");

        // Bouton « Se connecter avec GitLab » → challenge OAuth de l'instance configurée (Auth.Authority).
        app.MapGet("/auth/oauth", (HttpContext ctx) =>
        {
            if (!authCfg.OAuthConfigured) return Results.Redirect("/login");
            return Results.Challenge(new AuthenticationProperties { RedirectUri = "/" }, new[] { "gitlab" });
        }).AllowAnonymous().RequireRateLimiting("login");

        // Connexion par Personal Access Token : validée CÔTÉ SERVEUR contre {instance}/api/v4/user.
        // Le token N'EST PAS stocké : on ne garde que le username dans le cookie de session.
        app.MapPost("/api/auth/token", (Func<HttpContext, Task<IResult>>)(ctx => self.LoginWithTokenAsync(ctx)))
           .AllowAnonymous().RequireRateLimiting("login");

        app.MapGet("/logout", async (HttpContext ctx) =>
        {
            await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Redirect("/");
        });

        Console.WriteLine("=== Dashboard server (ASP.NET Core) prêt ===");
        Console.WriteLine($"Ouvrir : http://localhost:{port}/");
        if (authCfg.OAuthConfigured) Console.WriteLine($"Auth   : OAuth GitLab ({authCfg.Authority}) · callback {authCfg.CallbackPath}");
        else Console.WriteLine("Auth   : connexion par token GitLab (page /login) — OAuth non configuré.");
        Console.WriteLine($"Rôles  : accès réservé aux membres GitLab du projet {config.GitLab.ProjectId} · admins (fichier serveur) : {string.Join(", ", authCfg.AdminUsers)}");
        Console.WriteLine("Ctrl+C pour arrêter.");

        // Arrêt propre quand le token de Program.cs (Ctrl+C) est annulé.
        ct.Register(() => { try { app.Lifetime.StopApplication(); } catch { } });
        await app.RunAsync();
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
        return Results.Content(HypervisorReleaseView.BuildReferencePage(json), "text/html; charset=utf-8");
    }

    private async Task<IResult> ServeConfigAsync(HttpContext ctx)
    {
        var deny = RequireAdmin(ctx); if (deny != null) return deny;
        try
        {
            var node = JsonNode.Parse(await File.ReadAllTextAsync(RuntimeConfigPath()));
            var gl = node?["GitLab"];
            if (gl?["PrivateToken"] != null) gl["PrivateToken"] = TokenSentinel;
            var au = node?["Auth"];
            if (!string.IsNullOrEmpty(au?["ClientSecret"]?.GetValue<string>())) au!["ClientSecret"] = TokenSentinel;
            var masked = node!.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            return Results.Json(new ConfigPayload { content = masked });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("ServeConfig KO : " + ex);
            return Results.Text("Lecture de la configuration impossible.", "text/plain; charset=utf-8", Encoding.UTF8, statusCode: 500);
        }
    }

    private async Task<IResult> SaveConfigAsync(HttpContext ctx)
    {
        var deny = RequireAdmin(ctx); if (deny != null) return deny;
        string content;
        try
        {
            using var reader = new StreamReader(ctx.Request.Body);
            var body = await reader.ReadToEndAsync();
            content = JsonNode.Parse(body)?["content"]?.GetValue<string>() ?? "";
        }
        catch { return Results.Text("Requête invalide.", "text/plain; charset=utf-8", Encoding.UTF8, statusCode: 400); }

        JsonNode? incoming;
        try { incoming = JsonNode.Parse(content); }
        catch (Exception ex) { return Results.Text("JSON invalide : " + ex.Message, "text/plain; charset=utf-8", Encoding.UTF8, statusCode: 400); }
        if (incoming == null) return Results.Text("JSON vide.", "text/plain; charset=utf-8", Encoding.UTF8, statusCode: 400);

        var glNode = incoming["GitLab"];
        if (glNode != null)
        {
            var inTok = glNode["PrivateToken"]?.GetValue<string>();
            if (string.IsNullOrEmpty(inTok) || inTok == TokenSentinel)
                glNode["PrivateToken"] = ReadCurrentToken() ?? "";
        }

        // La section Auth (admins, OAuth) n'est PAS modifiable via l'app : on la préserve depuis
        // le disque, quel que soit le contenu envoyé. Seul un accès au serveur peut la changer.
        try
        {
            var current = JsonNode.Parse(await File.ReadAllTextAsync(RuntimeConfigPath()));
            var curAuth = current?["Auth"];
            if (curAuth != null) incoming["Auth"] = curAuth.DeepClone();
            else incoming.AsObject().Remove("Auth");
        }
        catch { incoming.AsObject().Remove("Auth"); }

        var outText = incoming.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        try
        {
            await WriteFileAtomicAsync(RuntimeConfigPath(), outText);
            var src = SourceConfigPath();
            if (src != null && !string.Equals(src, RuntimeConfigPath(), StringComparison.OrdinalIgnoreCase))
                await WriteFileAtomicAsync(src, outText);
        }
        catch (Exception ex) { Console.Error.WriteLine("SaveConfig write KO : " + ex); return Results.Text("Écriture de la configuration impossible.", "text/plain; charset=utf-8", Encoding.UTF8, statusCode: 500); }

        try { _config = BuildConfig(); _memberCache.Clear(); _payloadCache.Clear(); /* le projet a pu changer → re-résoudre accès + payloads */ }
        catch (Exception ex) { Console.Error.WriteLine("SaveConfig reload KO : " + ex); return Results.Text("Configuration enregistrée, mais rechargement à chaud échoué (redémarrez le serveur).", "text/plain; charset=utf-8"); }

        return Results.Text("Configuration enregistrée et rechargée. (Régénérez les vues / relancez un Rafraîchir si Milestone ou TrackedLabels ont changé.)", "text/plain; charset=utf-8");
    }

    private async Task<IResult> ServeAccountsAsync(HttpContext ctx)
    {
        var deny = RequireAdmin(ctx); if (deny != null) return deny;
        try
        {
            var p = AccountsPath();
            var content = File.Exists(p) ? await File.ReadAllTextAsync(p) : "{\n  \"views\": [],\n  \"accounts\": []\n}";
            JsonNode.Parse(content);
            return Results.Json(new ConfigPayload { content = content });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("ServeAccounts KO : " + ex);
            return Results.Text("Lecture des comptes impossible.", "text/plain; charset=utf-8", Encoding.UTF8, statusCode: 500);
        }
    }

    private async Task<IResult> SaveAccountsAsync(HttpContext ctx)
    {
        var deny = RequireAdmin(ctx); if (deny != null) return deny;
        string content;
        try
        {
            using var reader = new StreamReader(ctx.Request.Body);
            var body = await reader.ReadToEndAsync();
            content = JsonNode.Parse(body)?["content"]?.GetValue<string>() ?? "";
        }
        catch { return Results.Text("Requête invalide.", "text/plain; charset=utf-8", Encoding.UTF8, statusCode: 400); }

        JsonNode? incoming;
        try { incoming = JsonNode.Parse(content); }
        catch (Exception ex) { return Results.Text("JSON invalide : " + ex.Message, "text/plain; charset=utf-8", Encoding.UTF8, statusCode: 400); }
        if (incoming == null) return Results.Text("JSON vide.", "text/plain; charset=utf-8", Encoding.UTF8, statusCode: 400);

        var outText = incoming.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        try
        {
            var p = AccountsPath();
            var dir = Path.GetDirectoryName(p);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            await WriteFileAtomicAsync(p, outText);
        }
        catch (Exception ex) { Console.Error.WriteLine("SaveAccounts KO : " + ex); return Results.Text("Écriture des comptes impossible.", "text/plain; charset=utf-8", Encoding.UTF8, statusCode: 500); }

        return Results.Text("Comptes & vues enregistrés.", "text/plain; charset=utf-8");
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
        try
        {
            using var reader = new StreamReader(ctx.Request.Body);
            var body = await reader.ReadToEndAsync();
            if (!string.IsNullOrWhiteSpace(body))
            {
                var parsed = JsonSerializer.Deserialize<RefreshRequest>(body);
                if (parsed?.milestones != null && parsed.milestones.Count > 0)
                    milestonesToRefresh.AddRange(parsed.milestones.Where(m => !string.IsNullOrWhiteSpace(m)));
                else if (!string.IsNullOrWhiteSpace(parsed?.milestone))
                    milestonesToRefresh.Add(parsed.milestone!);
            }
        }
        catch { /* body invalide → refresh complet */ }

        var appStopping = ctx.RequestServices.GetService(typeof(IHostApplicationLifetime)) as IHostApplicationLifetime;
        var serverCt = appStopping?.ApplicationStopping ?? CancellationToken.None;
        _ = Task.Run(() => RunRefreshAsync(milestonesToRefresh, serverCt));
        return Results.Text("Acquisition démarrée.", "text/plain; charset=utf-8", Encoding.UTF8, statusCode: 202);
    }

    private async Task RunRefreshAsync(List<string> milestonesToRefresh, CancellationToken serverCt)
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

            if (milestonesToRefresh.Count == 0)
            {
                await ExportPipeline.RunFullExportAsync(_config, (i, t) => { _state.Current = i; _state.Total = t; }, linked.Token, "");
            }
            else
            {
                for (int i = 0; i < milestonesToRefresh.Count; i++)
                {
                    linked.Token.ThrowIfCancellationRequested();
                    Console.WriteLine($"[Refresh] Milestone {i + 1}/{milestonesToRefresh.Count} : {milestonesToRefresh[i]}");
                    await ExportPipeline.RunFullExportAsync(_config, (cur, tot) => { _state.Current = cur; _state.Total = tot; }, linked.Token, milestonesToRefresh[i]);
                }
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
            // Données régénérées → invalider les caches mémoire.
            _payloadCache.Clear();
            lock (_issuesCacheLock) { _issuesCache = null; }
            _state.Running = false;
            _refreshLock.Release();
            linked.Dispose();
            lock (_ctsLock) { if (_refreshCts == localCts) _refreshCts = null; }
            localCts.Dispose();
        }
    }

    // --- Helpers (identiques à l'ancien serveur) ------------------------

    private static string RuntimeConfigPath() => Path.Combine(AppContext.BaseDirectory, "appsettings.json");

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

    // POST /api/auth/token  body: { "instance": "https://gitlab.com", "token": "glpat-..." }
    // Valide le PAT en interrogeant {instance}/api/v4/user, puis ouvre une session cookie
    // contenant UNIQUEMENT le username. Le token n'est jamais conservé ni loggé.
    private async Task<IResult> LoginWithTokenAsync(HttpContext ctx)
    {
        string instance, token;
        try
        {
            using var reader = new StreamReader(ctx.Request.Body);
            var body = await reader.ReadToEndAsync();
            var node = JsonNode.Parse(body);
            instance = (node?["instance"]?.GetValue<string>() ?? "").Trim().TrimEnd('/');
            token    = (node?["token"]?.GetValue<string>() ?? "").Trim();
        }
        catch { return Results.Json(new { ok = false, error = "Requête invalide." }, statusCode: 400); }

        if (string.IsNullOrWhiteSpace(instance) || string.IsNullOrWhiteSpace(token))
            return Results.Json(new { ok = false, error = "Instance et token requis." }, statusCode: 400);

        // Garde anti-SSRF : URL absolue http/https uniquement.
        if (!Uri.TryCreate(instance, UriKind.Absolute, out var baseUri)
            || (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
            return Results.Json(new { ok = false, error = "Adresse GitLab invalide." }, statusCode: 400);

        // Anti-SSRF : l'instance DOIT correspondre à Auth.Authority. Authority absent → fail-closed
        // (sinon un POST anonyme pourrait sonder le réseau interne / les métadonnées cloud).
        if (string.IsNullOrWhiteSpace(_config.Auth.Authority)
            || !Uri.TryCreate(_config.Auth.Authority, UriKind.Absolute, out var allowedUri))
            return Results.Json(new { ok = false, error = "Connexion par token non configurée sur ce serveur." }, statusCode: 503);
        if (!string.Equals(baseUri.Host, allowedUri.Host, StringComparison.OrdinalIgnoreCase))
            return Results.Json(new { ok = false, error = "Instance non autorisée." }, statusCode: 400);

        var http = SharedHttp;
        using var req = new HttpRequestMessage(HttpMethod.Get, new Uri(baseUri, "/api/v4/user"));
        req.Headers.Add("PRIVATE-TOKEN", token);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        HttpResponseMessage resp;
        try { resp = await http.SendAsync(req, ctx.RequestAborted); }
        catch (TaskCanceledException) { return Results.Json(new { ok = false, error = "Délai dépassé en joignant l’instance GitLab." }, statusCode: 504); }
        catch { return Results.Json(new { ok = false, error = "Instance GitLab injoignable." }, statusCode: 502); }

        using (resp)
        {
            if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized || resp.StatusCode == System.Net.HttpStatusCode.Forbidden)
                return Results.Json(new { ok = false, error = "Token d’accès invalide ou expiré. Vérifiez vos identifiants GitLab." });
            if (!resp.IsSuccessStatusCode)
                return Results.Json(new { ok = false, error = $"Réponse inattendue de GitLab ({(int)resp.StatusCode})." });

            string username; bool isBot;
            try
            {
                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ctx.RequestAborted));
                username = doc.RootElement.TryGetProperty("username", out var u) ? (u.GetString() ?? "") : "";
                isBot = doc.RootElement.TryGetProperty("bot", out var b) && b.ValueKind == JsonValueKind.True;
            }
            catch { return Results.Json(new { ok = false, error = "Réponse GitLab illisible." }); }

            if (string.IsNullOrWhiteSpace(username))
                return Results.Json(new { ok = false, error = "Compte GitLab sans username." });

            // Comptes techniques (project/group access tokens) : refusés — ce sont des identités de
            // service, pas des personnes. Le flag `bot` de /api/v4/user fait foi, le pattern en filet.
            if (isBot || IsBotUsername(username))
                return Results.Json(new { ok = false, error = "Les comptes de service (bot) ne peuvent pas ouvrir de session — utilisez votre token personnel." });

            // Rôles GitLab : seuls les MEMBRES du projet (ou les admins du fichier serveur) entrent.
            if (!IsAdminLogin(username) && await GetProjectAccessLevelAsync(username, ctx.RequestAborted) == null)
                return Results.Json(new { ok = false, error = "Votre compte GitLab n’est pas membre du projet — accès refusé." });

            // Session cookie — même schéma que l'OAuth (claim Name = username). AUCUN token stocké.
            var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, username) },
                CookieAuthenticationDefaults.AuthenticationScheme);
            await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

            return Results.Json(new { ok = true, user = new { username } });
        }
    }

    // --- Assistant de première mise en service (/setup) -----------------

    // Configuré ? = connexion GitLab de base renseignée. Sinon `/` redirige vers /setup.
    private bool IsConfigured() =>
        !string.IsNullOrWhiteSpace(_config.GitLab.BaseUrl)
        && !string.IsNullOrWhiteSpace(_config.GitLab.PrivateToken)
        && !string.IsNullOrWhiteSpace(_config.GitLab.ProjectId);

    private static async Task<JsonNode?> ReadJsonBody(HttpContext ctx)
    {
        using var r = new StreamReader(ctx.Request.Body);
        return JsonNode.Parse(await r.ReadToEndAsync());
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
        using var req = new HttpRequestMessage(HttpMethod.Get, new Uri(baseUri, path));
        req.Headers.Add("PRIVATE-TOKEN", token);
        using var resp = await http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode) return null;
        return JsonNode.Parse(await resp.Content.ReadAsStringAsync(ct));
    }

    // POST /api/setup/test → { ok, projects:[{id,name,group}], groups:[{name,members:[{username,name,role}]}] }
    private async Task<IResult> SetupTestAsync(HttpContext ctx)
    {
        var deny = RequireAdmin(ctx); if (deny != null) return deny;
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
            projects.Add(new { id = p!["id"]!.GetValue<int>(), name = p["name"]!.GetValue<string>(),
                group = p["namespace"]?["path"]?.GetValue<string>() ?? "" });

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

    // POST /api/setup/labels { baseUrl, token, selfSigned, projectIds:[] } → { ok, labels:[...] }
    private async Task<IResult> SetupLabelsAsync(HttpContext ctx)
    {
        var deny = RequireAdmin(ctx); if (deny != null) return deny;
        var b = await ReadJsonBody(ctx);
        var baseUrl = (b?["baseUrl"]?.GetValue<string>() ?? "").Trim().TrimEnd('/');
        var token   = (b?["token"]?.GetValue<string>() ?? "").Trim();
        var selfS   = b?["selfSigned"]?.GetValue<bool>() ?? false;
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) || !SetupHostAllowed(baseUri))
            return Results.Json(new { ok = false });

        var http = selfS ? _sharedHttpRelaxed : _sharedHttp;
        var set = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pid in b?["projectIds"]?.AsArray() ?? new JsonArray())
        {
            var lj = await GlGet(http, baseUri, $"/api/v4/projects/{pid!.GetValue<int>()}/labels?per_page=100", token, ctx.RequestAborted);
            foreach (var l in lj?.AsArray() ?? new JsonArray())
                set.Add(l!["name"]!.GetValue<string>());
        }
        return Results.Json(new { ok = true, labels = set });
    }

    // POST /api/setup { baseUrl, token, selfSigned, timeout, projectIds, labelPhases, teams } → écrit appsettings.json
    private async Task<IResult> SetupSaveAsync(HttpContext ctx)
    {
        var deny = RequireAdmin(ctx); if (deny != null) return deny;
        var b = await ReadJsonBody(ctx);
        var baseUrl = (b?["baseUrl"]?.GetValue<string>() ?? "").Trim().TrimEnd('/');
        var token   = (b?["token"]?.GetValue<string>() ?? "").Trim();
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(token))
            return Results.Json(new { ok = false, error = "Connexion manquante." });
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) || !SetupHostAllowed(baseUri))
            return Results.Json(new { ok = false, error = "Instance non autorisée." });

        var projectIds = (b?["projectIds"]?.AsArray() ?? new JsonArray()).Select(n => n!.GetValue<int>()).ToList();
        if (projectIds.Count == 0) return Results.Json(new { ok = false, error = "Sélectionnez au moins un projet." });

        var trackedLabels = new List<string>();
        var labelPhases = new Dictionary<string, string>();
        foreach (var kv in (b?["labelPhases"]?.AsObject() ?? new JsonObject()))
        {
            var ph = kv.Value?.GetValue<string>() ?? "none";
            labelPhases[kv.Key] = ph;
            if (ph != "none") trackedLabels.Add(kv.Key);
        }

        var teams = new JsonObject();
        foreach (var t in b?["teams"]?.AsArray() ?? new JsonArray())
        {
            var name = t!["name"]!.GetValue<string>();
            var arr = new JsonArray();
            foreach (var m in t["members"]?.AsArray() ?? new JsonArray())
                arr.Add(m!["username"]!.GetValue<string>());
            teams[name] = arr;
        }

        // Merge non destructif : la section Auth (admins, OAuth) est PRÉSERVÉE telle quelle (non modifiable via l'app).
        JsonObject root;
        try { root = (JsonNode.Parse(await File.ReadAllTextAsync(RuntimeConfigPath())) as JsonObject) ?? new JsonObject(); }
        catch { root = new JsonObject(); }

        var gl = root["GitLab"] as JsonObject ?? new JsonObject(); root["GitLab"] = gl;
        gl["BaseUrl"] = baseUrl;
        gl["PrivateToken"] = token;
        gl["ProjectId"] = projectIds[0].ToString();   // modèle mono-projet : projet principal = 1er sélectionné
        gl["AllowSelfSignedCertificates"] = b?["selfSigned"]?.GetValue<bool>() ?? false;
        gl["RequestTimeoutSeconds"] = b?["timeout"]?.GetValue<int>() ?? 60;

        var ex = root["Export"] as JsonObject ?? new JsonObject(); root["Export"] = ex;
        ex["TrackedLabels"] = new JsonArray(trackedLabels.Select(s => JsonValue.Create(s)).ToArray());
        ex["LabelPhases"] = JsonSerializer.SerializeToNode(labelPhases);
        ex["Teams"] = teams;
        ex["ProjectIds"] = new JsonArray(projectIds.Select(i => JsonValue.Create(i)).ToArray());

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

        return Results.Json(new { ok = true });
    }

    // --- Résolution de compte / rôles (étape 3) -------------------------

    private Task<IResult> ServeTokenAsync(HttpContext ctx)
    {
        var deny = RequireAdmin(ctx);
        if (deny != null) return Task.FromResult(deny);
        return Task.FromResult(Results.Json(new TokenPayload { token = ReadCurrentToken() ?? "" }));
    }

    private IResult? RequireAdmin(HttpContext ctx)
        => IsAdminLogin(ctx.User.Identity?.Name)
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

    private async Task<int?> GetProjectAccessLevelAsync(string username, CancellationToken ct)
    {
        // Les bots sont membres du projet (ils portent les tokens) mais ne sont PAS des utilisateurs :
        // traités comme non-membres partout (login, OAuth, revalidation continue).
        if (string.IsNullOrWhiteSpace(username) || IsBotUsername(username)) return null;
        if (_memberCache.TryGetValue(username, out var hit) && hit.until > DateTime.UtcNow) return hit.level;
        try
        {
            var gl = _config.GitLab;
            var http = SharedHttp;
            var url = $"{gl.BaseUrl.TrimEnd('/')}/api/v4/projects/{Uri.EscapeDataString(gl.ProjectId)}/members/all?query={Uri.EscapeDataString(username)}&per_page=100";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("PRIVATE-TOKEN", gl.PrivateToken);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) throw new HttpRequestException($"members/all -> {(int)resp.StatusCode}");
            int? level = null;
            using (var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct)))
                foreach (var m in doc.RootElement.EnumerateArray())
                    if (m.TryGetProperty("username", out var u)
                        && string.Equals(u.GetString(), username, StringComparison.OrdinalIgnoreCase)
                        && m.TryGetProperty("access_level", out var al))
                    { level = al.GetInt32(); break; }
            _memberCache[username] = (level, DateTime.UtcNow.Add(MemberCacheTtl));
            return level;
        }
        catch
        {
            if (_memberCache.TryGetValue(username, out var stale))
            {
                _memberCache[username] = (stale.level, DateTime.UtcNow.Add(MemberCacheTtl));
                return stale.level;
            }
            return null;
        }
    }

    /// <summary>Session autorisée ? (admin configuré, ou membre GitLab du projet — révocable à chaud.)</summary>
    private async Task<bool> IsAllowedAsync(HttpContext ctx)
    {
        var login = ctx.User.Identity?.Name ?? "";
        if (IsAdminLogin(login)) return true;
        return await GetProjectAccessLevelAsync(login, ctx.RequestAborted) != null;
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

        // Cache du JSON produit : clé = périmètre du compte, signature = mtimes des fichiers source.
        // Évite de relire/recalculer/resérialiser tout le périmètre à chaque requête (/ et /api/data).
        var dir = cfg.Export.OutputDirectory;
        string Mtime(string f) { var fp = Path.Combine(dir, f); return File.Exists(fp) ? File.GetLastWriteTimeUtc(fp).Ticks.ToString() : "0"; }
        var cacheKey = $"{r.ScopeType}|{r.ScopeValue}|{string.Join(',', r.Milestones)}|{string.Join(',', r.Labels)}|{r.Role}";
        var sig = $"{Mtime("issues.json")}.{Mtime("labels.json")}.{Mtime("milestones.json")}";
        if (_payloadCache.TryGetValue(cacheKey, out var cached) && cached.sig == sig) return cached.json;

        var all = await LoadIssuesAsync(ctx.RequestAborted);

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

        var json = await HypervisorReleaseView.BuildPayloadJsonAsync(
            cfg.Export.OutputDirectory, cfg.GitLab.Milestone, filtered.ToList(),
            cfg.Export.TrackedTransitions, scopedTeams, cfg.Export.LabelPhases, ctx.RequestAborted);
        _payloadCache[cacheKey] = (sig, json);
        return json;
    }

    private static (DateTime mtime, List<IssueExport> data)? _issuesCache;
    private static readonly object _issuesCacheLock = new();
    // Cache du payload JSON sérialisé par périmètre+signature de fichiers (cf. BuildScopedPayloadAsync).
    private static readonly ConcurrentDictionary<string, (string sig, string json)> _payloadCache = new();
    private async Task<List<IssueExport>> LoadIssuesAsync(CancellationToken ct)
    {
        var p = Path.Combine(_config.Export.OutputDirectory, "issues.json");
        if (!File.Exists(p)) return new List<IssueExport>();
        var mtime = File.GetLastWriteTimeUtc(p);
        lock (_issuesCacheLock) { if (_issuesCache is { } c && c.mtime == mtime) return c.data; }
        List<IssueExport> data;
        try
        {
            using var fs = new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            data = await JsonSerializer.DeserializeAsync<List<IssueExport>>(fs, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, ct) ?? new List<IssueExport>();
        }
        catch (Exception ex)
        {
            // issues.json corrompu/partiel (ex. pendant un refresh) : ne pas planter en 500.
            Console.Error.WriteLine("issues.json illisible : " + ex.Message);
            lock (_issuesCacheLock) { if (_issuesCache is { } c) return c.data; }
            return new List<IssueExport>();
        }
        lock (_issuesCacheLock) { _issuesCache = (mtime, data); }
        return data;
    }

    private static string? ReadCurrentToken()
    {
        try
        {
            var node = JsonNode.Parse(File.ReadAllText(RuntimeConfigPath()));
            return node?["GitLab"]?["PrivateToken"]?.GetValue<string>();
        }
        catch { return null; }
    }

    private static AppConfig BuildConfig()
    {
        var b = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables(prefix: "KPI_");
        var cfg = new AppConfig();
        b.Build().Bind(cfg);
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
        public string? milestone { get; set; }
        public List<string>? milestones { get; set; }
    }

    private sealed class ConfigPayload { public string content { get; set; } = ""; }
    private sealed class TokenPayload { public string token { get; set; } = ""; }
}
