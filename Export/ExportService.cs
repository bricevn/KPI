using Kpi.Config;
using Kpi.Export.Models;
using Kpi.GitLab;
using Kpi.GitLab.Models;

namespace Kpi.Export;

public sealed class ExportService
{
    private readonly GitLabClient _client;
    private readonly ExportConfig _exportConfig;
    private readonly string _milestone;
    private readonly HashSet<string> _trackedLabels;
    private readonly List<LabelTransitionConfig> _trackedTransitions;

    public ExportService(GitLabClient client, GitLabConfig gitLabConfig, ExportConfig exportConfig)
    {
        _client = client;
        _exportConfig = exportConfig;
        _milestone = gitLabConfig.Milestone;
        _trackedLabels = new HashSet<string>(exportConfig.TrackedLabels, StringComparer.OrdinalIgnoreCase);
        _trackedTransitions = exportConfig.TrackedTransitions;
    }

    public async Task<List<IssueExport>> BuildIssueExportsAsync(
        CancellationToken ct,
        Action<int, int>? onProgress = null,
        string? milestoneOverride = null)
    {
        // milestoneOverride : null = utiliser _milestone (config), "" = TOUTES les issues du projet, sinon nom précis.
        var effective = milestoneOverride ?? _milestone;
        Console.WriteLine(string.IsNullOrWhiteSpace(effective)
            ? "Récupération de TOUTES les issues du projet (toutes milestones)..."
            : $"Récupération des issues (milestone={effective})...");
        var issues = await _client.GetIssuesByMilestoneAsync(effective, ct);
        Console.WriteLine($"  -> {issues.Count} issues récupérées.");
        return await BuildExportsFromIssuesAsync(issues, ct, onProgress);
    }

    /// <summary>Construit les IssueExport à partir d'une liste d'issues DÉJÀ récupérées (ex. extraction scopée
    /// par assignee, où l'appelant a fait l'union multi-assignee). Traitement PARALLÈLE à concurrence bornée :
    /// chaque issue déclenche ~4-5 appels API (events, notes, MRs, approvals) — l'enchaînement séquentiel était
    /// le goulot. BuildSingleAsync est sans état partagé mutable → sûr en concurrent ; ordre préservé.</summary>
    public async Task<List<IssueExport>> BuildExportsFromIssuesAsync(
        List<GitLabIssue> issues, CancellationToken ct, Action<int, int>? onProgress = null)
    {
        onProgress?.Invoke(0, issues.Count);
        const int MaxConcurrency = 6; // prudent vs rate-limit GitLab
        var exports = new IssueExport[issues.Count];
        var done = 0;
        using var gate = new SemaphoreSlim(MaxConcurrency);
        async Task ProcessAsync(int i)
        {
            await gate.WaitAsync(ct);
            try
            {
                exports[i] = await BuildSingleAsync(issues[i], ct);
                var n = Interlocked.Increment(ref done);
                Console.WriteLine($"  [{n}/{issues.Count}] #{issues[i].Iid} — {Truncate(issues[i].Title, 60)}");
                onProgress?.Invoke(n, issues.Count);
            }
            finally { gate.Release(); }
        }
        var tasks = new List<Task>(issues.Count);
        for (var i = 0; i < issues.Count; i++) tasks.Add(ProcessAsync(i));
        await Task.WhenAll(tasks);
        return exports.ToList();
    }

    private async Task<IssueExport> BuildSingleAsync(GitLabIssue issue, CancellationToken ct)
    {
        var export = new IssueExport
        {
            Id = issue.Id,
            Iid = issue.Iid,
            ProjectId = issue.ProjectId,
            Title = issue.Title,
            State = issue.State,
            Weight = issue.Weight,
            Milestone = issue.Milestone?.Title,
            WebUrl = issue.WebUrl,
            CreatedAt = issue.CreatedAt,
            UpdatedAt = issue.UpdatedAt,
            ClosedAt = issue.ClosedAt,
            ClosedByUsername = issue.ClosedBy?.Username,
            AuthorUsername = issue.Author?.Username,
            Labels = issue.Labels.ToList(),
            Assignees = issue.Assignees.Select(a => a.Username).ToList(),
        };

        // Évènements de labels.
        var allEvents = await _client.GetLabelEventsAsync(issue.Iid, ct);
        var ordered = allEvents
            .Where(e => e.Label != null)
            .OrderBy(e => e.CreatedAt)
            .ToList();

        // Filtrage sur les labels tracés.
        foreach (var ev in ordered)
        {
            if (ev.Label == null) continue;
            if (!_trackedLabels.Contains(ev.Label.Name)) continue;
            export.TrackedLabelEvents.Add(new LabelTransitionEvent
            {
                CreatedAt = ev.CreatedAt,
                Action = ev.Action,
                Label = ev.Label.Name,
                UserUsername = ev.User?.Username,
            });
        }

        // Première / dernière date d'ajout par label tracé.
        foreach (var label in _trackedLabels)
        {
            var adds = ordered
                .Where(e => e.Action == "add" && string.Equals(e.Label?.Name, label, StringComparison.OrdinalIgnoreCase))
                .Select(e => e.CreatedAt)
                .ToList();
            export.FirstAddedAtPerTrackedLabel[label] = adds.Count > 0 ? adds.Min() : null;
            export.LastAddedAtPerTrackedLabel[label] = adds.Count > 0 ? adds.Max() : null;
        }

        // Transitions configurées : on parcourt les événements et on compte les passages From -> To.
        // Définition d'une transition : un "add" du label To précédé (chronologiquement) du label From actif.
        ComputeTransitions(ordered, export);

        // Commentaires non-system : seul le compte est consommé (dashboard « Commentaires »).
        var notes = await _client.GetIssueNotesAsync(issue.Iid, ct);
        var realNotes = notes.Where(n => !n.System).ToList();
        export.Comments = new CommentsSummary
        {
            Count = realNotes.Count,
        };

        // Toutes les MR liées à l'issue, peu importe le statut.
        var related = await _client.GetRelatedMergeRequestsAsync(issue.Iid, ct);
        // MR identifiées par GitLab comme ayant clos l'issue.
        var closing = await _client.GetClosingMergeRequestsAsync(issue.Iid, ct);
        var closingIid = ChooseClosingMr(closing)?.Iid;

        // S'assure qu'on a bien la closing dans la liste (au cas où related ne la retournerait pas).
        var combined = new Dictionary<long, GitLabMergeRequest>();
        foreach (var mr in related) combined[mr.Iid] = mr;
        foreach (var mr in closing) combined.TryAdd(mr.Iid, mr);

        foreach (var mr in combined.Values.OrderBy(m => m.CreatedAt ?? DateTimeOffset.MinValue))
        {
            var approvers = await FetchApproversAsync(mr.Iid, ct);
            export.MergeRequests.Add(new MergeRequestSummary
            {
                Iid = mr.Iid,
                Title = mr.Title,
                State = mr.State,
                WebUrl = mr.WebUrl,
                AuthorUsername = mr.Author?.Username,
                CreatedAt = mr.CreatedAt,
                MergedAt = mr.MergedAt,
                ClosedAt = mr.ClosedAt,
                MergedByUsername = mr.MergedBy?.Username,
                Approvers = approvers,
                Reviewers = mr.Reviewers.Select(r => r.Username).ToList(),
                IsClosing = closingIid.HasValue && mr.Iid == closingIid.Value,
            });
        }

        return export;
    }

    private async Task<List<string>> FetchApproversAsync(long mrIid, CancellationToken ct)
    {
        try
        {
            var approvals = await _client.GetMergeRequestApprovalsAsync(mrIid, ct);
            if (approvals?.ApprovedBy == null) return new List<string>();
            return approvals.ApprovedBy
                .Select(a => a.User?.Username)
                .Where(u => !string.IsNullOrEmpty(u))
                .Select(u => u!)
                .ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    [warn] approvals MR !{mrIid} indisponibles : {ex.Message}");
            return new List<string>();
        }
    }

    private void ComputeTransitions(List<ResourceLabelEvent> ordered, IssueExport export)
    {
        // active : ensemble des labels actuellement positionnés sur l'issue à l'instant ev.
        // activeSince : date à laquelle chaque label actif a été ajouté pour la dernière fois.
        // Pour chaque add d'un label "To" d'une transition configurée, si "From" est actif :
        //   - on compte la transition
        //   - on enregistre la durée passée dans "From" (ev.CreatedAt - activeSince[From])
        var active = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var activeSince = new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);

        // Pré-init des compteurs/listes pour toutes les paires configurées.
        foreach (var t in _trackedTransitions)
        {
            var key = TransitionKey(t.From, t.To);
            export.TransitionCounts[key] = 0;
            export.TransitionDates[key] = new List<DateTimeOffset>();
            export.TransitionDurations[key] = new List<TimeSpan>();
        }

        foreach (var ev in ordered)
        {
            var name = ev.Label?.Name;
            if (name == null) continue;

            if (ev.Action == "add")
            {
                foreach (var t in _trackedTransitions)
                {
                    if (string.Equals(t.To, name, StringComparison.OrdinalIgnoreCase) && active.Contains(t.From))
                    {
                        var key = TransitionKey(t.From, t.To);
                        export.TransitionCounts[key]++;
                        export.TransitionDates[key].Add(ev.CreatedAt);

                        if (activeSince.TryGetValue(t.From, out var since))
                        {
                            var duration = ev.CreatedAt - since;
                            if (duration >= TimeSpan.Zero)
                                export.TransitionDurations[key].Add(duration);
                        }
                    }
                }
                active.Add(name);
                activeSince[name] = ev.CreatedAt;
            }
            else if (ev.Action == "remove")
            {
                active.Remove(name);
                activeSince.Remove(name);
            }
        }
    }

    public static string TransitionKey(string from, string to) => $"{from} -> {to}";

    private static GitLabMergeRequest? ChooseClosingMr(List<GitLabMergeRequest> closingMrs)
    {
        if (closingMrs.Count == 0) return null;

        // Priorité : MR mergée la plus récente. Sinon la plus récemment fermée. Sinon la plus récemment créée.
        var merged = closingMrs
            .Where(m => m.MergedAt.HasValue)
            .OrderByDescending(m => m.MergedAt!.Value)
            .FirstOrDefault();
        if (merged != null) return merged;

        var closed = closingMrs
            .Where(m => m.ClosedAt.HasValue)
            .OrderByDescending(m => m.ClosedAt!.Value)
            .FirstOrDefault();
        if (closed != null) return closed;

        return closingMrs.OrderByDescending(m => m.CreatedAt ?? DateTimeOffset.MinValue).First();
    }

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) || s.Length <= max ? s : s.Substring(0, max) + "...";
}
