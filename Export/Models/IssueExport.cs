using System.Text.Json.Serialization;
using Kpi.GitLab.Models;

namespace Kpi.Export.Models;

/// <summary>
/// Vue enrichie d'une issue, prête à être sérialisée en JSON ou aplatie en CSV.
/// </summary>
public sealed class IssueExport
{
    public long Id { get; set; }
    public long Iid { get; set; }
    public long ProjectId { get; set; }
    public string Title { get; set; } = "";
    public string State { get; set; } = "";
    public int? Weight { get; set; }
    public string? Milestone { get; set; }
    public string? WebUrl { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public string? ClosedByUsername { get; set; }
    public string? AuthorUsername { get; set; }
    public List<string> Labels { get; set; } = new();
    public List<string> Assignees { get; set; } = new();

    /// <summary>Toutes les transitions tracées (labels configurés) pour cette issue.</summary>
    public List<LabelTransitionEvent> TrackedLabelEvents { get; set; } = new();

    /// <summary>Dates "première fois où le label a été ajouté" pour chaque label tracé.</summary>
    public Dictionary<string, DateTimeOffset?> FirstAddedAtPerTrackedLabel { get; set; } = new();

    /// <summary>Dates "dernière fois où le label a été ajouté" pour chaque label tracé.</summary>
    public Dictionary<string, DateTimeOffset?> LastAddedAtPerTrackedLabel { get; set; } = new();

    /// <summary>Compteur de transitions From -> To (clé "From->To") sur les paires configurées.</summary>
    public Dictionary<string, int> TransitionCounts { get; set; } = new();

    /// <summary>Dates des transitions From -> To (clé "From->To") sur les paires configurées.</summary>
    public Dictionary<string, List<DateTimeOffset>> TransitionDates { get; set; } = new();

    /// <summary>
    /// Durées passées dans le label "From" avant chaque transition vers "To".
    /// Une entrée par occurrence de transition détectée.
    /// </summary>
    public Dictionary<string, List<TimeSpan>> TransitionDurations { get; set; } = new();

    /// <summary>Toutes les MR liées à l'issue (tous statuts), avec leurs approbateurs.</summary>
    public List<MergeRequestSummary> MergeRequests { get; set; } = new();

    /// <summary>Synthèse des commentaires non-system (auteurs et compteurs).</summary>
    public CommentsSummary Comments { get; set; } = new();

    /// <summary>Raccourci : la MR identifiée comme ayant clos l'issue (si présente).</summary>
    [JsonIgnore]
    public MergeRequestSummary? ClosingMergeRequest =>
        MergeRequests.FirstOrDefault(m => m.IsClosing);
}

public sealed class LabelTransitionEvent
{
    public DateTimeOffset CreatedAt { get; set; }
    public string Action { get; set; } = ""; // add / remove
    public string Label { get; set; } = "";
    public string? UserUsername { get; set; }
}

public sealed class CommentsSummary
{
    /// <summary>Nombre total de commentaires non-system.</summary>
    public int Count { get; set; }
}

public sealed class MergeRequestSummary
{
    public long Iid { get; set; }
    public string Title { get; set; } = "";
    public string State { get; set; } = ""; // opened / closed / merged / locked
    public string? WebUrl { get; set; }
    public string? AuthorUsername { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
    public DateTimeOffset? MergedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public string? MergedByUsername { get; set; }
    public List<string> Approvers { get; set; } = new();
    public List<string> Reviewers { get; set; } = new();
    /// <summary>Vrai si cette MR est celle qui a (ou aurait) clos l'issue.</summary>
    public bool IsClosing { get; set; }
}
