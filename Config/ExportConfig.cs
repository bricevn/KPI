using System.Linq;

namespace Kpi.Config;

public sealed class AppConfig
{
    // 1c-D : le bloc mono-serveur `GitLab` a été RETIRÉ — la config est 100% multi-serveur (`Servers`).
    // Migration d'une ancienne config : voir docs/MIGRATION.md (convertir GitLab → une entrée Servers).
    /// <summary>Serveurs GitLab à analyser (v2 multi-serveurs). Chacun cloisonné sous output/&lt;Id&gt;/.</summary>
    public List<ServerConfig> Servers { get; set; } = new();
    public ExportConfig Export { get; set; } = new();
    public AuthConfig Auth { get; set; } = new();

    /// <summary>Serveurs configurés. Plus de repli legacy : <see cref="Servers"/> est l'unique source.</summary>
    public List<ServerConfig> ResolveServers() => Servers ?? new();

    /// <summary>
    /// Config client GitLab dérivée du PREMIER serveur, pour les chemins mono-serveur encore en place
    /// (export complet CLI, fetch labels/milestones, vues statiques). Milestone vide = toutes les issues.
    /// Le multi-serveur passe lui par <see cref="ServerConfig"/> directement (RunMultiServerExportAsync).
    /// </summary>
    public GitLabConfig PrimaryGitLab()
    {
        var s = ResolveServers().FirstOrDefault();
        if (s == null) return new GitLabConfig();
        return new GitLabConfig
        {
            BaseUrl = s.BaseUrl,
            PrivateToken = s.GroupToken,
            ProjectId = s.ProjectIds is { Count: > 0 } ? s.ProjectIds[0] : "",
            Milestone = "",
            AllowSelfSignedCertificates = s.AllowSelfSignedCertificates,
            RequestTimeoutSeconds = s.RequestTimeoutSeconds,
        };
    }
}

/// <summary>
/// Un serveur GitLab à analyser. Le <see cref="GroupToken"/> (token de GROUPE, scope read_api) couvre
/// les projets du groupe. Les données extraites sont cloisonnées sous <c>output/&lt;Id&gt;/</c> et
/// chiffrées au repos (cf. SecureStore, sous-clé dérivée de l'Id).
/// </summary>
public sealed class ServerConfig
{
    /// <summary>Identifiant court, stable et UNIQUE (segment de dossier, ex. « interne »). [A-Za-z0-9_-].</summary>
    public string Id { get; set; } = "";
    /// <summary>URL racine de l'instance (sans /api/v4). Ex : https://gitlab.exemple.com</summary>
    public string BaseUrl { get; set; } = "";
    /// <summary>Group Access Token (scope read_api). JAMAIS exposé au client ni loggé.</summary>
    public string GroupToken { get; set; } = "";
    /// <summary>Projets à extraire (« groupe/projet » ou IDs). Vide = tous les projets accessibles au token.</summary>
    public List<string> ProjectIds { get; set; } = new();
    public bool AllowSelfSignedCertificates { get; set; }
    public int RequestTimeoutSeconds { get; set; } = 60;
}

/// <summary>
/// Authentification. OAuth GitLab + connexion par Personal Access Token (page /login).
/// </summary>
public sealed class AuthConfig
{
    /// <summary>URL de l'instance GitLab (autorité OAuth). Ex : https://gitlab.obvious.tech</summary>
    public string Authority { get; set; } = "";
    /// <summary>Application ID de l'app OAuth GitLab.</summary>
    public string ClientId { get; set; } = "";
    /// <summary>Secret de l'app OAuth GitLab (gitignoré : appsettings.json).</summary>
    public string ClientSecret { get; set; } = "";
    /// <summary>Chemin de callback OAuth. Redirect URI à enregistrer = &lt;domaine&gt; + ce chemin.</summary>
    public string CallbackPath { get; set; } = "/signin-gitlab";
    /// <summary>Usernames GitLab considérés admin (en plus des comptes type=admin de accounts.json via leur liste leads).</summary>
    public List<string> AdminUsers { get; set; } = new();
    /// <summary>Id de la vue attribuée aux salariés connectés non listés (auto-provision « individuel »). Vide = tous les onglets.</summary>
    public string DefaultViewId { get; set; } = "";

    /// <summary>True si l'OAuth GitLab est exploitable (autorité + client id + secret renseignés).</summary>
    public bool OAuthConfigured =>
        !string.IsNullOrWhiteSpace(Authority)
        && !string.IsNullOrWhiteSpace(ClientId)
        && !string.IsNullOrWhiteSpace(ClientSecret);
}

public sealed class GitLabConfig
{
    public string BaseUrl { get; set; } = "";
    public string PrivateToken { get; set; } = "";
    public string ProjectId { get; set; } = "";
    public string Milestone { get; set; } = "";
    public bool AllowSelfSignedCertificates { get; set; }
    public int RequestTimeoutSeconds { get; set; } = 60;
}

public sealed class ExportConfig
{
    public string OutputDirectory { get; set; } = "output";
    public List<string> TrackedLabels { get; set; } = new();
    public List<LabelTransitionConfig> TrackedTransitions { get; set; } = new();

    /// <summary>
    /// Groupes d'utilisateurs (équipes). Clé = nom d'équipe, valeur = liste de usernames GitLab.
    /// Utilisé par le filtre "Équipe" du dashboard pour cocher/décocher en bloc.
    /// </summary>
    public Dictionary<string, List<string>> Teams { get; set; } = new();

    /// <summary>
    /// Mapping label → phase écrit par l'assistant de mise en service (/setup).
    /// ⚠ Pas encore consommé par la logique de phases (le mapper utilise des noms de labels en dur) :
    /// persisté pour un câblage ultérieur. Voir setup_dotnet/README.md.
    /// </summary>
    public Dictionary<string, string> LabelPhases { get; set; } = new();

    /// <summary>
    /// Projets sélectionnés à l'assistant. `GitLab.ProjectId` reste le projet principal (mono-projet) ;
    /// l'agrégation multi-projets reste à étendre côté extraction. Persisté pour un usage ultérieur.
    /// </summary>
    public List<int> ProjectIds { get; set; } = new();

    /// <summary>
    /// Catalogue des périodes (phases) dynamiques défini par l'admin via /setup : SOURCE DE VÉRITÉ
    /// unique des keys/libellés/couleurs. <see cref="LabelPhases"/> ne fait que pointer vers ces keys.
    /// VIDE ⇒ aucune phase calculée ni affichée (PAS de repli Prod::* historique — c'est le contrat v2).
    /// L'ordre de la liste = ordre des colonnes/légende côté UI.
    /// </summary>
    public List<PeriodDefinition> Periods { get; set; } = new();
}

/// <summary>
/// Une période de temps (phase) du cycle de production. Renommable / couleur ajustable par l'admin.
/// La <see cref="Key"/> est l'identifiant stable référencé par <see cref="ExportConfig.LabelPhases"/>
/// et par le payload <c>window.__DATA__.periods</c>.
/// </summary>
public sealed class PeriodDefinition
{
    /// <summary>Clé stable, minuscule [a-z0-9]+ (ex. « dev »). Référencée par LabelPhases et le payload.</summary>
    public string Key { get; set; } = "";
    /// <summary>Libellé affiché (ex. « Développement »). Renommable par l'admin.</summary>
    public string Name { get; set; } = "";
    /// <summary>Couleur hex (#RRGGBB).</summary>
    public string Color { get; set; } = "";
    /// <summary>Si true, la période est chronométrée (compte dans les durées). Ex. uiux=false (segment Gantt seul).</summary>
    public bool Timed { get; set; } = true;
}

public sealed class LabelTransitionConfig
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
}
