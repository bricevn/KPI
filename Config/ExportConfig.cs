namespace Kpi.Config;

public sealed class AppConfig
{
    public GitLabConfig GitLab { get; set; } = new();
    public ExportConfig Export { get; set; } = new();
    public AuthConfig Auth { get; set; } = new();
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
}

public sealed class LabelTransitionConfig
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
}
