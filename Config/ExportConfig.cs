using System.Linq;
using System.Text.Json;

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

    /// <summary>
    /// Répare les dictionnaires dont les CLÉS contiennent « : » (labels GitLab type « Prod::Code In Progress »).
    /// <para>
    /// IConfiguration emploie « : » comme séparateur de SECTION : un <c>.Bind()</c> découpe donc ces clés et
    /// corrompt silencieusement le mapping — <see cref="ExportConfig.LabelPhases"/> se retrouve vidé (seules
    /// les clés sans « : » survivent), d'où « le cycle moyen affiche 0 j » et « les associations label→phase
    /// ne sont pas conservées entre /setup et le dashboard ». On relit donc ces maps directement dans les
    /// fichiers JSON (System.Text.Json préserve les clés telles quelles), en superposant <c>appsettings.json</c>
    /// puis <c>appsettings.Development.json</c> comme le fait IConfiguration.
    /// </para>
    /// À appeler IMMÉDIATEMENT après <c>configRoot.Bind(appConfig)</c>, avec <c>AppContext.BaseDirectory</c>.
    /// </summary>
    public static void RepairColonKeyedMaps(AppConfig cfg, string baseDir)
    {
        if (cfg?.Export == null || string.IsNullOrEmpty(baseDir)) return;
        var lp = new Dictionary<string, string>();
        var lbp = new Dictionary<int, Dictionary<string, string>>();
        var sm = new Dictionary<int, string>();
        bool sawLp = false, sawLbp = false, sawSm = false;

        foreach (var file in new[] { "appsettings.json", "appsettings.Development.json" })
        {
            var path = Path.Combine(baseDir, file);
            if (!File.Exists(path)) continue;
            JsonDocument? doc;
            try { doc = JsonDocument.Parse(File.ReadAllText(path)); }
            catch { continue; } // fichier illisible/invalide : on garde ce qu'a produit Bind
            using (doc)
            {
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object
                    || !root.TryGetProperty("Export", out var ex) || ex.ValueKind != JsonValueKind.Object)
                    continue;

                // LabelPhases : { "label": "phaseKey" }
                if (ex.TryGetProperty("LabelPhases", out var lpEl) && lpEl.ValueKind == JsonValueKind.Object)
                {
                    sawLp = true;
                    foreach (var kv in lpEl.EnumerateObject())
                        if (kv.Value.ValueKind == JsonValueKind.String)
                            lp[kv.Name] = kv.Value.GetString() ?? "none";
                }

                // LabelPhasesByProject : { "projectId": { "label": "phaseKey" } }
                if (ex.TryGetProperty("LabelPhasesByProject", out var lbpEl) && lbpEl.ValueKind == JsonValueKind.Object)
                {
                    sawLbp = true;
                    foreach (var proj in lbpEl.EnumerateObject())
                    {
                        if (!int.TryParse(proj.Name, out var pid) || proj.Value.ValueKind != JsonValueKind.Object) continue;
                        if (!lbp.TryGetValue(pid, out var m)) { m = new(); lbp[pid] = m; }
                        foreach (var kv in proj.Value.EnumerateObject())
                            if (kv.Value.ValueKind == JsonValueKind.String)
                                m[kv.Name] = kv.Value.GetString() ?? "none";
                    }
                }

                // StartMilestones : { "projectId": "titre" } — clés numériques (pas de « : ») mais le
                // binder IConfiguration est capricieux avec les Dictionary à clé non-string selon les
                // versions (échec SILENCIEUX) → on la relit ici aussi, elle pilote l'extraction.
                if (ex.TryGetProperty("StartMilestones", out var smEl) && smEl.ValueKind == JsonValueKind.Object)
                {
                    sawSm = true;
                    foreach (var kv in smEl.EnumerateObject())
                        if (kv.Value.ValueKind == JsonValueKind.String && int.TryParse(kv.Name, out var smPid))
                            sm[smPid] = kv.Value.GetString() ?? "";
                }
            }
        }

        // On ne remplace que si un fichier portait réellement la map (sinon on conserve la sortie de Bind,
        // p. ex. un override par variable d'environnement KPI_).
        if (sawLp) cfg.Export.LabelPhases = lp;
        if (sawLbp) cfg.Export.LabelPhasesByProject = lbp;
        if (sawSm) cfg.Export.StartMilestones = sm;
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
    /// <summary>URL de l'instance GitLab (autorité OAuth). Ex : https://gitlab.example.com</summary>
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
    /// <summary>Instance GitLab à certificat TLS auto-signé / CA interne : relâche la validation TLS du
    /// BACKCHANNEL OAuth (échange code→token + /api/v4/user). Indispensable pour le SSO sur self-hosted self-signed.</summary>
    public bool AllowSelfSignedCertificates { get; set; }

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

    /// <summary>Labels « transversaux » (noms exacts) recoupant plusieurs types, affichés à part dans la
    /// section « Labels transversaux » du dashboard. Configurable dans Options → Configuration.
    /// Vide ⇒ repli mapper sur les labels historiques (CONTRACTUAL / Unplanned / Surcharge QA).
    /// Valeurs (pas des clés) ⇒ pas concerné par la corruption « : » de <see cref="RepairColonKeyedMaps"/>.</summary>
    public List<string> TransversalLabels { get; set; } = new();

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

    /// <summary>
    /// Mapping label → phase PAR PROJET (clé = id de projet GitLab). Écrit par /setup en mode « Par projet ».
    /// Vide pour un projet ⇒ repli sur le mapping global <see cref="LabelPhases"/>.
    /// ⚠ Stage 1 : persisté par /setup mais PAS encore consommé par la logique de phases du dashboard
    /// (câblage du dashboard prévu en Stage 2). Voir export_setup/README.md.
    /// </summary>
    public Dictionary<int, Dictionary<string, string>> LabelPhasesByProject { get; set; } = new();

    /// <summary>
    /// Catalogue des périodes PAR PROJET (clé = id de projet GitLab). Écrit par /setup en mode « Par projet ».
    /// Vide pour un projet ⇒ repli sur <see cref="Periods"/> global. Même réserve Stage 1/Stage 2 que
    /// <see cref="LabelPhasesByProject"/>.
    /// </summary>
    public Dictionary<int, List<PeriodDefinition>> PeriodsByProject { get; set; } = new();

    /// <summary>
    /// Projets importés à l'assistant, avec leur NOM et leur namespace (full_path), persistés par /setup.
    /// Permet à l'onglet Options du dashboard d'afficher la vraie liste de projets (les IDs seuls ne suffisent
    /// pas — GitLab ne renvoie pas le nom dans les issues). Vide pour une config ancienne ⇒ repli sur les IDs.
    /// </summary>
    public List<ProjectRef> Projects { get; set; } = new();

    /// <summary>
    /// Groupe GitLab (full_path) de chaque équipe (clé = nom d'équipe). Écrit par /setup. Permet d'associer
    /// une équipe à un projet (l'équipe couvre le projet si son groupe est le namespace du projet ou un ancêtre).
    /// <see cref="Teams"/> (membres) reste la source pour le filtrage ; ceci n'ajoute que le rattachement projet.
    /// </summary>
    public Dictionary<string, string> TeamGroups { get; set; } = new();

    /// <summary>
    /// Équipes PAR PROJET (clé = id de projet GitLab ; valeur = nom d'équipe → membres). Écrit par l'onglet
    /// Options en portée « par projet ». Vide pour un projet ⇒ repli sur <see cref="Teams"/> global.
    /// </summary>
    public Dictionary<int, Dictionary<string, List<string>>> TeamsByProject { get; set; } = new();

    /// <summary>Valeur sentinelle de <see cref="StartMilestones"/> : « Aucune » — le projet est SAUTÉ
    /// lors des extractions globales (données existantes préservées). Une régénération CIBLÉE sur ce
    /// projet (Options → projet précis) l'extrait quand même : la demande explicite prime.</summary>
    public const string SkipExtractionSentinel = "__skip__";

    /// <summary>
    /// Milestone d'IMPORT INITIAL par projet (clé = id de projet GitLab ; valeur = titre de milestone).
    /// Écrit par /setup : la 1re extraction importe UNIQUEMENT cette milestone — ce n'est PAS une borne :
    /// les rafraîchissements globaux mettent ensuite à jour les milestones déjà importées (store ∪ celle-ci),
    /// et une régénération ciblée (Options) peut importer n'importe quelle autre milestone à tout moment.
    /// Vide/absent ⇒ tout l'historique. Valeur <see cref="SkipExtractionSentinel"/> ⇒ projet sauté (pas
    /// d'extraction ; import ultérieur via régénération ciblée).
    /// NB : clés numériques ⇒ pas concerné par la corruption « : » de IConfiguration (RepairColonKeyedMaps).
    /// </summary>
    public Dictionary<int, string> StartMilestones { get; set; } = new();

    // ---- Calcul du temps (Options → « Calcul du temps ») : fenêtre de temps ouvré + anti-bruit
    //      des durées de phase (cycle). Consommé par le mapper client via payload.workTime. ----
    /// <summary>Heure de début de la journée travaillée (0-23, heure locale du serveur). Défaut 9 h.</summary>
    public int WorkStartHour { get; set; } = 9;
    /// <summary>Heure de fin de la journée travaillée (1-24, exclusive). Défaut 19 h (maximum légal cadre : 10 h/j).</summary>
    public int WorkEndHour { get; set; } = 19;
    /// <summary>true = seuls les jours ouvrés (lun-ven) comptent dans les durées de phase.</summary>
    public bool WorkingDaysOnly { get; set; } = true;
    /// <summary>Jours fériés (aaaa-mm-jj) exclus du temps ouvré.</summary>
    public List<string> Holidays { get; set; } = new();
    /// <summary>Anti-bruit : les segments de phase plus courts que ce seuil (minutes, temps réel) sont
    /// ignorés dans les durées — élimine les poses/retraits de label accidentels. 0 = désactivé.</summary>
    public int MinPhaseMinutes { get; set; } = 0;
    /// <summary>OBSOLÈTE (Piste 2) : l'appartenance au « travail actif » est désormais portée par
    /// <see cref="PeriodDefinition.Role"/> (= "active"). Conservé UNIQUEMENT en lecture pour migrer les
    /// vieilles configs (période sans Role → Role dérivé de Timed + cette liste). Plus jamais écrit.
    /// Repli si vide : dev/review/qa/tofix. Pas de « : » ⇒ Bind sûr.</summary>
    public List<string> EffectivePhases { get; set; } = new();
}

/// <summary>Référence d'un projet importé : id GitLab + nom affichable + namespace (full_path du groupe).</summary>
public sealed class ProjectRef
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    /// <summary>full_path du namespace (ex. « groupe » ou « parent/sous-groupe »), pour l'association aux équipes.</summary>
    public string Group { get; set; } = "";
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
    /// <summary>Si true, la période est chronométrée (compte dans les durées). Ex. uiux=false (segment Gantt seul).
    /// DÉRIVÉ de <see cref="Role"/> à l'écriture (<c>Role != "nogc"</c>) ; conservé en lecture pour la migration.</summary>
    public bool Timed { get; set; } = true;
    /// <summary>Rôle de la phase (Piste 2, remplace Timed + EffectivePhases) :
    /// <c>"active"</c> = chronométrée ET comptée dans le temps effectif ; <c>"wait"</c> = chronométrée mais
    /// exclue (attente) ; <c>"nogc"</c> = non chronométrée (segment Gantt seul).
    /// Vide ⇒ à migrer depuis Timed + <see cref="ExportConfig.EffectivePhases"/> au chargement.</summary>
    public string Role { get; set; } = "";
}

public sealed class LabelTransitionConfig
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
}
