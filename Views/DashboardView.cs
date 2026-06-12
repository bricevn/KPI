using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Kpi.Config;
using Kpi.Export;
using Kpi.Export.Models;

namespace Kpi.Views;

/// <summary>
/// Dashboard interactif (auto-contenu) :
/// - Embarque toutes les issues + métadonnées en JSON dans la page.
/// - L'utilisateur choisit dynamiquement le label primaire et les utilisateurs filtrés.
/// - Le rendu (tableau principal + sections "Issues sans poids" / "sans approval")
///   est recalculé côté JS à chaque changement de filtre.
/// </summary>
public sealed class DashboardView
{
    private const string DefaultPrimaryLabel = "";

    public async Task GenerateAsync(
        string outputDirectory,
        string milestone,
        List<IssueExport> exports,
        IReadOnlyList<LabelTransitionConfig> trackedTransitions,
        IReadOnlyDictionary<string, List<string>> teams,
        IReadOnlyDictionary<string, string> labelPhases,
        IReadOnlyList<PeriodDefinition> periods,
        CancellationToken ct)
    {
        var viewsDir = Path.Combine(outputDirectory, "views");
        Directory.CreateDirectory(viewsDir);

        // Export statique AUTONOME : payload réel inliné + page nouveau design (même rendu que le live).
        var aux = LoadAuxFromDisk(outputDirectory);
        var payloadJson = BuildPayloadJson(milestone, exports, trackedTransitions, teams, labelPhases, periods, aux.labels, aux.milestones, aux.lastExtracted);
        var html = BuildReferencePage(payloadJson);
        var path = Path.Combine(viewsDir, SafeFileName($"release_{(string.IsNullOrEmpty(milestone) ? "all" : milestone)}.html"));
        await File.WriteAllTextAsync(path, html, new UTF8Encoding(false), ct);
        Console.WriteLine($"  HTML écrit : {path}");
    }

    /// <summary>
    /// Construit le JSON du payload dashboard (window.__DATA__). Les données auxiliaires (couleurs de
    /// labels, dates de milestones, date d'extraction) sont fournies par l'APPELANT, qui maîtrise le
    /// stockage : déchiffrement multi-serveurs côté serveur, ou lecture disque pour l'export statique.
    /// </summary>
    public static string BuildPayloadJson(
        string milestone,
        List<IssueExport> exports,
        IReadOnlyList<LabelTransitionConfig> trackedTransitions,
        IReadOnlyDictionary<string, List<string>> teams,
        IReadOnlyDictionary<string, string> labelPhases,
        IReadOnlyList<PeriodDefinition> periods,
        IReadOnlyList<Kpi.GitLab.Models.GitLabLabel> labels,
        IReadOnlyList<Kpi.GitLab.Models.GitLabMilestone> milestones,
        string? lastExtractedAt)
    {
        var payload = BuildPayload(milestone, exports, trackedTransitions, teams, labelPhases, periods);
        payload.lastExtractedAt = lastExtractedAt ?? "";
        foreach (var lab in labels)
            if (!string.IsNullOrEmpty(lab.Name))
                payload.labelColors[lab.Name] = new LabelColorPayload { color = lab.Color ?? "", textColor = lab.TextColor ?? "" };
        foreach (var m in milestones)
            if (!string.IsNullOrEmpty(m.Title))
                payload.milestoneDates[m.Title] = new MilestoneDatesPayload { startDate = m.StartDate, dueDate = m.DueDate };
        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    /// <summary>Lit labels.json + milestones.json EN CLAIR + la date d'extraction depuis un dossier
    /// (chemin legacy / export statique CLI). Côté serveur multi-serveurs, on déchiffre via SecureStore.</summary>
    public static (List<Kpi.GitLab.Models.GitLabLabel> labels, List<Kpi.GitLab.Models.GitLabMilestone> milestones, string lastExtracted) LoadAuxFromDisk(string outputDirectory)
    {
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var labels = new List<Kpi.GitLab.Models.GitLabLabel>();
        var milestones = new List<Kpi.GitLab.Models.GitLabMilestone>();
        var lastExtracted = "";
        var issuesPath = Path.Combine(outputDirectory, "issues.json");
        if (File.Exists(issuesPath)) lastExtracted = File.GetLastWriteTimeUtc(issuesPath).ToString("yyyy-MM-dd HH:mm");
        var lp = Path.Combine(outputDirectory, "labels.json");
        if (File.Exists(lp)) { try { labels = JsonSerializer.Deserialize<List<Kpi.GitLab.Models.GitLabLabel>>(File.ReadAllText(lp), opts) ?? new(); } catch (Exception ex) { Console.WriteLine($"  [warn] labels.json : {ex.Message}"); } }
        var mp = Path.Combine(outputDirectory, "milestones.json");
        if (File.Exists(mp)) { try { milestones = JsonSerializer.Deserialize<List<Kpi.GitLab.Models.GitLabMilestone>>(File.ReadAllText(mp), opts) ?? new(); } catch (Exception ex) { Console.WriteLine($"  [warn] milestones.json : {ex.Message}"); } }
        return (labels, milestones, lastExtracted);
    }

    // --- Payload construction -------------------------------------------------

    private static DashboardPayload BuildPayload(
        string milestone,
        List<IssueExport> exports,
        IReadOnlyList<LabelTransitionConfig> trackedTransitions,
        IReadOnlyDictionary<string, List<string>> teams,
        IReadOnlyDictionary<string, string> labelPhases,
        IReadOnlyList<PeriodDefinition> periods)
    {
        var availableLabels = exports
            .SelectMany(e => e.Labels)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(l => l, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Les usernames qui peuvent compter pour "traité par".
        var users = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in exports)
        {
            foreach (var a in e.Assignees) users.Add(a);
            if (!string.IsNullOrEmpty(e.ClosedByUsername)) users.Add(e.ClosedByUsername!);
            if (!string.IsNullOrEmpty(e.AuthorUsername)) users.Add(e.AuthorUsername!);
            foreach (var ev in e.TrackedLabelEvents)
                if (!string.IsNullOrEmpty(ev.UserUsername)) users.Add(ev.UserUsername!);
            foreach (var mr in e.MergeRequests)
                foreach (var u in mr.Approvers) users.Add(u);
        }
        var availableUsers = users.OrderBy(u => u, StringComparer.OrdinalIgnoreCase).ToList();

        return new DashboardPayload
        {
            milestone = milestone,
            generatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm"),
            defaultPrimaryLabel = availableLabels.FirstOrDefault(l => string.Equals(l, DefaultPrimaryLabel, StringComparison.OrdinalIgnoreCase))
                                  ?? DefaultPrimaryLabel,
            availableLabels = availableLabels,
            availableUsers = availableUsers,
            transitionsConfig = trackedTransitions
                .Select(t => new TransitionConfigPayload { from = t.From, to = t.To })
                .ToList(),
            availableMilestones = exports
                .Select(e => e.Milestone)
                .Where(m => !string.IsNullOrEmpty(m))
                .Select(m => m!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(m => m, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            teams = teams?.ToDictionary(kv => kv.Key, kv => kv.Value ?? new List<string>())
                    ?? new Dictionary<string, List<string>>(),
            labelPhases = labelPhases?.ToDictionary(kv => kv.Key, kv => kv.Value)
                    ?? new Dictionary<string, string>(),
            // Catalogue dynamique des périodes (source de vérité keys/libellés/couleurs côté UI).
            // « none » est exclu : ce n'est pas une période mais le marqueur « non suivi » de labelPhases.
            periods = (periods ?? Array.Empty<PeriodDefinition>())
                    .Where(p => !string.IsNullOrEmpty(p.Key) && !string.Equals(p.Key, "none", StringComparison.OrdinalIgnoreCase))
                    .Select(p => new PeriodPayload
                    {
                        key = p.Key,
                        name = string.IsNullOrWhiteSpace(p.Name) ? p.Key : p.Name,        // jamais de libellé vide
                        color = string.IsNullOrWhiteSpace(p.Color) ? "#cccccc" : p.Color, // jamais de couleur vide
                        timed = p.Timed,
                    })
                    .ToList(),
            issues = exports.Select(ToPayload).ToList(),
        };
    }

    private static IssuePayload ToPayload(IssueExport e) => new()
    {
        iid = e.Iid,
        title = e.Title,
        state = e.State,
        weight = e.Weight,
        webUrl = e.WebUrl,
        authorUsername = e.AuthorUsername,
        closedByUsername = e.ClosedByUsername,
        createdAt = e.CreatedAt?.ToString("o"),
        closedAt = e.ClosedAt?.ToString("o"),
        milestone = e.Milestone,
        assignees = e.Assignees,
        labels = e.Labels,
        transitionCounts = e.TransitionCounts,
        transitionDurations = e.TransitionDurations.ToDictionary(
            kv => kv.Key,
            kv => kv.Value.Select(ts => ts.TotalSeconds).ToArray()),
        labelEvents = e.TrackedLabelEvents.Select(ev => new LabelEventPayload
        {
            user = ev.UserUsername,
            action = ev.Action,
            label = ev.Label,
            at = ev.CreatedAt.ToString("o"),
        }).ToList(),
        commentsCount = e.Comments.Count,
        commentsByAuthor = e.Comments.ByAuthor,
        mergeRequests = e.MergeRequests.Select(mr => new MergeRequestPayload
        {
            iid = mr.Iid,
            state = mr.State,
            approvers = mr.Approvers.ToArray(),
            reviewers = mr.Reviewers.ToArray(),
            isClosing = mr.IsClosing,
        }).ToList(),
    };

    public static string BuildReferencePage(string? payloadJson = null, string lang = "en")
    {
        var baseDir = AppContext.BaseDirectory;
        string A(string sub, string f) => File.ReadAllText(Path.Combine(baseDir, "Assets", sub, f));
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        var lc = Kpi.Localization.Loc.Normalize(lang);
        sb.AppendLine("<html lang=\"" + lc + "\"" + (Kpi.Localization.Loc.IsRtl(lc) ? " dir=\"rtl\"" : "") + ">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"utf-8\">");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        sb.AppendLine("  <title>Release 2026-R2 — Dashboard</title>");
        sb.AppendLine("  <link rel=\"preconnect\" href=\"https://fonts.googleapis.com\">");
        sb.AppendLine("  <link rel=\"preconnect\" href=\"https://fonts.gstatic.com\" crossorigin>");
        sb.AppendLine("  <link href=\"https://fonts.googleapis.com/css2?family=Space+Grotesk:wght@400;500;600;700&family=IBM+Plex+Mono:wght@400;500;600&display=swap\" rel=\"stylesheet\">");
        sb.AppendLine("  <style>");
        sb.AppendLine(A("design", "shared.css"));
        sb.AppendLine(A("design", "studio.css"));
        sb.AppendLine("  html, body { margin: 0; } body { background: #0a0e13; }");
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("  <div id=\"root\"></div>");
        sb.AppendLine("  <script>" + A("vendor", "react.js") + "</script>");
        sb.AppendLine("  <script>" + A("vendor", "react-dom.js") + "</script>");
        sb.AppendLine("  <script>" + A("vendor", "babel.js") + "</script>");
        // Données : si payload réel fourni → window.__DATA__ + mapper, exécutés SYNCHRONEMENT
        // (window.APP doit exister AVANT l'éval des .jsx qui font `const A = window.APP`).
        // Sinon → faux data.js (démo, route /ref).
        if (payloadJson != null)
        {
            // Défense XSS : le payload est inliné dans un <script>. Neutraliser toute séquence pouvant
            // clore la balise / casser le parseur (titres d'issues contrôlés par les membres du projet).
            // Ces caractères n'apparaissent que dans des littéraux de chaîne JSON → \uXXXX y est décodé à l'identique.
            var safeJson = payloadJson
                .Replace("<", "\\u003C").Replace(">", "\\u003E").Replace("&", "\\u0026")
                .Replace("\u2028", "\\u2028").Replace("\u2029", "\\u2029");
            sb.AppendLine("  <script>window.__DATA__ = " + safeJson + ";\n" + A("app", "mapper.js") + "\nwindow.APP = window.buildAPP(window.__DATA__);</script>");
        }
        else
            sb.AppendLine("  <script>" + A("app", "data.js") + "</script>");
        // i18n CLIENT : window.__LANG__ (langue serveur) PUIS i18n.js (définit window.t) — AVANT les .jsx,
        // qui appellent window.t() au render. Script sync = exécuté avant les <script type="text/babel">.
        sb.AppendLine("  <script>window.__LANG__ = " + JsonSerializer.Serialize(lc)
            + "; window.__LANGS__ = " + JsonSerializer.Serialize(Kpi.Localization.Loc.List()) + ";</script>");
        sb.AppendLine("  <script>" + A("app", "i18n.js") + "</script>");
        foreach (var f in new[] { "ui.jsx", "tab-dashboard.jsx", "tab-charts.jsx", "tab-anomalies.jsx", "tab-issues.jsx", "tab-calendar.jsx", "tab-velocity.jsx", "tab-options.jsx", "tweaks-panel.jsx", "shell.jsx" })
            sb.AppendLine("  <script type=\"text/babel\" data-presets=\"react\">" + A("app", f) + "</script>");
        sb.AppendLine("  <script type=\"text/babel\" data-presets=\"react\">ReactDOM.createRoot(document.getElementById('root')).render(<window.Shell />);</script>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }


    private static string SafeFileName(string s)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        // JsonNamingPolicy.CamelCase serait redondant : on a déjà des champs en lowerCamel.
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // --- DTOs (champs en lowerCamelCase pour aller direct en JS) --------------

    private sealed class DashboardPayload
    {
        public string milestone { get; set; } = "";
        public string generatedAt { get; set; } = "";
        public string? lastExtractedAt { get; set; }
        public string defaultPrimaryLabel { get; set; } = "";
        public List<string> availableLabels { get; set; } = new();
        public List<string> availableUsers { get; set; } = new();
        public List<TransitionConfigPayload> transitionsConfig { get; set; } = new();
        public List<string> availableMilestones { get; set; } = new();
        public Dictionary<string, List<string>> teams { get; set; } = new();
        public Dictionary<string, string> labelPhases { get; set; } = new();
        public List<PeriodPayload> periods { get; set; } = new();
        public Dictionary<string, LabelColorPayload> labelColors { get; set; } = new();
        public Dictionary<string, MilestoneDatesPayload> milestoneDates { get; set; } = new();
        public List<IssuePayload> issues { get; set; } = new();
    }

    private sealed class TransitionConfigPayload
    {
        public string from { get; set; } = "";
        public string to { get; set; } = "";
    }

    private sealed class PeriodPayload
    {
        public string key { get; set; } = "";
        public string name { get; set; } = "";
        public string color { get; set; } = "";
        public bool timed { get; set; }
    }

    private sealed class LabelColorPayload
    {
        public string color { get; set; } = "";
        public string textColor { get; set; } = "";
    }

    private sealed class MilestoneDatesPayload
    {
        public string? startDate { get; set; }
        public string? dueDate { get; set; }
    }

    private sealed class IssuePayload
    {
        public long iid { get; set; }
        public string title { get; set; } = "";
        public string state { get; set; } = "";
        public int? weight { get; set; }
        public string? webUrl { get; set; }
        public string? authorUsername { get; set; }
        public string? closedByUsername { get; set; }
        public string? createdAt { get; set; } // ISO 8601
        public string? closedAt { get; set; }  // ISO 8601, null si encore opened
        public string? milestone { get; set; }
        public List<string> assignees { get; set; } = new();
        public List<string> labels { get; set; } = new();
        public Dictionary<string, int> transitionCounts { get; set; } = new();
        public Dictionary<string, double[]> transitionDurations { get; set; } = new();
        public int commentsCount { get; set; }
        public Dictionary<string, int> commentsByAuthor { get; set; } = new();
        public List<LabelEventPayload> labelEvents { get; set; } = new();
        public List<MergeRequestPayload> mergeRequests { get; set; } = new();
    }

    private sealed class LabelEventPayload
    {
        public string? user { get; set; }
        public string action { get; set; } = "";
        public string label { get; set; } = "";
        public string at { get; set; } = ""; // ISO 8601
    }

    private sealed class MergeRequestPayload
    {
        public long iid { get; set; }
        public string state { get; set; } = "";
        public string[] approvers { get; set; } = Array.Empty<string>();
        public string[] reviewers { get; set; } = Array.Empty<string>();
        public bool isClosing { get; set; }
    }

}
