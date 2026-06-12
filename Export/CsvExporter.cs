using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using GitLabExporter.Config;
using GitLabExporter.Export.Models;

namespace GitLabExporter.Export;

public sealed class CsvExporter
{
    private readonly ExportConfig _config;
    private static readonly CsvConfiguration CsvConfig = new(CultureInfo.InvariantCulture)
    {
        // Excel FR ouvre mieux les CSV avec ';'. À ajuster si besoin.
        Delimiter = ";",
        Encoding = System.Text.Encoding.UTF8,
        HasHeaderRecord = true,
    };

    public CsvExporter(ExportConfig config) => _config = config;

    public async Task WriteAsync(List<IssueExport> exports, CancellationToken ct)
    {
        Directory.CreateDirectory(_config.OutputDirectory);
        await WriteIssuesMainAsync(exports, ct);
        await WriteAssigneesAsync(exports, ct);
        await WriteLabelsAsync(exports, ct);
        await WriteTrackedLabelEventsAsync(exports, ct);
        await WriteTransitionsAsync(exports, ct);
        await WriteMergeRequestsAsync(exports, ct);
        await WriteApproversAsync(exports, ct);
    }

    /// <summary>
    /// Vue principale : une ligne par issue, avec les dates/compteurs des labels et transitions tracés en colonnes.
    /// </summary>
    private async Task WriteIssuesMainAsync(List<IssueExport> exports, CancellationToken ct)
    {
        var path = Path.Combine(_config.OutputDirectory, "issues.csv");
        await using var writer = new StreamWriter(path, false, System.Text.Encoding.UTF8);
        await using var csv = new CsvWriter(writer, CsvConfig);

        // Header
        csv.WriteField("iid");
        csv.WriteField("title");
        csv.WriteField("state");
        csv.WriteField("weight");
        csv.WriteField("milestone");
        csv.WriteField("author");
        csv.WriteField("created_at");
        csv.WriteField("updated_at");
        csv.WriteField("closed_at");
        csv.WriteField("closed_by");
        csv.WriteField("assignees");
        csv.WriteField("labels");
        csv.WriteField("web_url");

        // Colonnes "first_added_<label>" + "last_added_<label>" pour chaque label tracé.
        foreach (var label in _config.TrackedLabels)
        {
            csv.WriteField($"first_added_{label}");
            csv.WriteField($"last_added_{label}");
        }

        // Colonnes "transitions_<From>_to_<To>" + "last_transition_<From>_to_<To>".
        foreach (var t in _config.TrackedTransitions)
        {
            csv.WriteField($"count_{t.From}_to_{t.To}");
            csv.WriteField($"last_{t.From}_to_{t.To}");
        }

        // Colonnes MR de clôture.
        csv.WriteField("closing_mr_iid");
        csv.WriteField("closing_mr_state");
        csv.WriteField("closing_mr_created_at");
        csv.WriteField("closing_mr_merged_at");
        csv.WriteField("closing_mr_closed_at");
        csv.WriteField("closing_mr_author");
        csv.WriteField("closing_mr_merged_by");
        csv.WriteField("closing_mr_approvers");
        csv.WriteField("closing_mr_url");

        // Colonnes agrégées sur toutes les MR liées.
        csv.WriteField("related_mrs_count");
        csv.WriteField("related_mrs_iids");
        csv.WriteField("all_approvers");
        csv.WriteField("all_approvers_count");

        await csv.NextRecordAsync();

        foreach (var e in exports)
        {
            csv.WriteField(e.Iid);
            csv.WriteField(e.Title);
            csv.WriteField(e.State);
            csv.WriteField(e.Weight);
            csv.WriteField(e.Milestone);
            csv.WriteField(e.AuthorUsername);
            csv.WriteField(Fmt(e.CreatedAt));
            csv.WriteField(Fmt(e.UpdatedAt));
            csv.WriteField(Fmt(e.ClosedAt));
            csv.WriteField(e.ClosedByUsername);
            csv.WriteField(string.Join(",", e.Assignees));
            csv.WriteField(string.Join(",", e.Labels));
            csv.WriteField(e.WebUrl);

            foreach (var label in _config.TrackedLabels)
            {
                e.FirstAddedAtPerTrackedLabel.TryGetValue(label, out var first);
                e.LastAddedAtPerTrackedLabel.TryGetValue(label, out var last);
                csv.WriteField(Fmt(first));
                csv.WriteField(Fmt(last));
            }

            foreach (var t in _config.TrackedTransitions)
            {
                var key = ExportService.TransitionKey(t.From, t.To);
                e.TransitionCounts.TryGetValue(key, out var count);
                e.TransitionDates.TryGetValue(key, out var dates);
                csv.WriteField(count);
                csv.WriteField(dates is { Count: > 0 } ? Fmt(dates.Max()) : "");
            }

            var mr = e.ClosingMergeRequest;
            csv.WriteField(mr?.Iid);
            csv.WriteField(mr?.State);
            csv.WriteField(Fmt(mr?.CreatedAt));
            csv.WriteField(Fmt(mr?.MergedAt));
            csv.WriteField(Fmt(mr?.ClosedAt));
            csv.WriteField(mr?.AuthorUsername);
            csv.WriteField(mr?.MergedByUsername);
            csv.WriteField(mr == null ? "" : string.Join(",", mr.Approvers));
            csv.WriteField(mr?.WebUrl);

            var allApprovers = e.MergeRequests
                .SelectMany(m => m.Approvers)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            csv.WriteField(e.MergeRequests.Count);
            csv.WriteField(string.Join(",", e.MergeRequests.Select(m => m.Iid)));
            csv.WriteField(string.Join(",", allApprovers));
            csv.WriteField(allApprovers.Count);

            await csv.NextRecordAsync();
        }
        Console.WriteLine($"  CSV écrit : {path}");
    }

    /// <summary>Une ligne par (issue, assignee) pour faciliter une vue par personne.</summary>
    private async Task WriteAssigneesAsync(List<IssueExport> exports, CancellationToken ct)
    {
        var path = Path.Combine(_config.OutputDirectory, "issues_assignees.csv");
        await using var writer = new StreamWriter(path, false, System.Text.Encoding.UTF8);
        await using var csv = new CsvWriter(writer, CsvConfig);

        csv.WriteField("iid"); csv.WriteField("title"); csv.WriteField("state");
        csv.WriteField("weight"); csv.WriteField("assignee");
        await csv.NextRecordAsync();

        foreach (var e in exports)
        {
            if (e.Assignees.Count == 0)
            {
                csv.WriteField(e.Iid); csv.WriteField(e.Title); csv.WriteField(e.State);
                csv.WriteField(e.Weight); csv.WriteField("");
                await csv.NextRecordAsync();
            }
            else
            {
                foreach (var a in e.Assignees)
                {
                    csv.WriteField(e.Iid); csv.WriteField(e.Title); csv.WriteField(e.State);
                    csv.WriteField(e.Weight); csv.WriteField(a);
                    await csv.NextRecordAsync();
                }
            }
        }
        Console.WriteLine($"  CSV écrit : {path}");
    }

    /// <summary>Une ligne par (issue, label) — utile pour pivot par label.</summary>
    private async Task WriteLabelsAsync(List<IssueExport> exports, CancellationToken ct)
    {
        var path = Path.Combine(_config.OutputDirectory, "issues_labels.csv");
        await using var writer = new StreamWriter(path, false, System.Text.Encoding.UTF8);
        await using var csv = new CsvWriter(writer, CsvConfig);

        csv.WriteField("iid"); csv.WriteField("title"); csv.WriteField("state");
        csv.WriteField("weight"); csv.WriteField("label");
        await csv.NextRecordAsync();

        foreach (var e in exports)
        {
            if (e.Labels.Count == 0) continue;
            foreach (var l in e.Labels)
            {
                csv.WriteField(e.Iid); csv.WriteField(e.Title); csv.WriteField(e.State);
                csv.WriteField(e.Weight); csv.WriteField(l);
                await csv.NextRecordAsync();
            }
        }
        Console.WriteLine($"  CSV écrit : {path}");
    }

    /// <summary>Une ligne par événement de label tracé (add/remove) avec sa date.</summary>
    private async Task WriteTrackedLabelEventsAsync(List<IssueExport> exports, CancellationToken ct)
    {
        var path = Path.Combine(_config.OutputDirectory, "label_events.csv");
        await using var writer = new StreamWriter(path, false, System.Text.Encoding.UTF8);
        await using var csv = new CsvWriter(writer, CsvConfig);

        csv.WriteField("iid"); csv.WriteField("created_at"); csv.WriteField("action");
        csv.WriteField("label"); csv.WriteField("user");
        await csv.NextRecordAsync();

        foreach (var e in exports)
        {
            foreach (var ev in e.TrackedLabelEvents)
            {
                csv.WriteField(e.Iid); csv.WriteField(Fmt(ev.CreatedAt));
                csv.WriteField(ev.Action); csv.WriteField(ev.Label); csv.WriteField(ev.UserUsername);
                await csv.NextRecordAsync();
            }
        }
        Console.WriteLine($"  CSV écrit : {path}");
    }

    /// <summary>Une ligne par occurrence d'une transition configurée (issue, paire, date).</summary>
    private async Task WriteTransitionsAsync(List<IssueExport> exports, CancellationToken ct)
    {
        var path = Path.Combine(_config.OutputDirectory, "transitions.csv");
        await using var writer = new StreamWriter(path, false, System.Text.Encoding.UTF8);
        await using var csv = new CsvWriter(writer, CsvConfig);

        csv.WriteField("iid"); csv.WriteField("from_label"); csv.WriteField("to_label");
        csv.WriteField("transition_at");
        await csv.NextRecordAsync();

        foreach (var e in exports)
        {
            foreach (var t in _config.TrackedTransitions)
            {
                var key = ExportService.TransitionKey(t.From, t.To);
                if (!e.TransitionDates.TryGetValue(key, out var dates)) continue;
                foreach (var d in dates)
                {
                    csv.WriteField(e.Iid); csv.WriteField(t.From); csv.WriteField(t.To);
                    csv.WriteField(Fmt(d));
                    await csv.NextRecordAsync();
                }
            }
        }
        Console.WriteLine($"  CSV écrit : {path}");
    }

    /// <summary>Une ligne par MR liée à l'issue (tous statuts), avec un flag is_closing.</summary>
    private async Task WriteMergeRequestsAsync(List<IssueExport> exports, CancellationToken ct)
    {
        var path = Path.Combine(_config.OutputDirectory, "merge_requests.csv");
        await using var writer = new StreamWriter(path, false, System.Text.Encoding.UTF8);
        await using var csv = new CsvWriter(writer, CsvConfig);

        csv.WriteField("issue_iid"); csv.WriteField("mr_iid"); csv.WriteField("title");
        csv.WriteField("state"); csv.WriteField("is_closing");
        csv.WriteField("author"); csv.WriteField("created_at"); csv.WriteField("merged_at");
        csv.WriteField("closed_at"); csv.WriteField("merged_by");
        csv.WriteField("reviewers"); csv.WriteField("approvers");
        csv.WriteField("approvers_count"); csv.WriteField("url");
        await csv.NextRecordAsync();

        foreach (var e in exports)
        {
            foreach (var mr in e.MergeRequests)
            {
                csv.WriteField(e.Iid); csv.WriteField(mr.Iid); csv.WriteField(mr.Title);
                csv.WriteField(mr.State); csv.WriteField(mr.IsClosing ? "true" : "false");
                csv.WriteField(mr.AuthorUsername); csv.WriteField(Fmt(mr.CreatedAt)); csv.WriteField(Fmt(mr.MergedAt));
                csv.WriteField(Fmt(mr.ClosedAt)); csv.WriteField(mr.MergedByUsername);
                csv.WriteField(string.Join(",", mr.Reviewers)); csv.WriteField(string.Join(",", mr.Approvers));
                csv.WriteField(mr.Approvers.Count); csv.WriteField(mr.WebUrl);
                await csv.NextRecordAsync();
            }
        }
        Console.WriteLine($"  CSV écrit : {path}");
    }

    /// <summary>Une ligne par (issue, MR, approuveur) — pivot facile par personne ou MR.</summary>
    private async Task WriteApproversAsync(List<IssueExport> exports, CancellationToken ct)
    {
        var path = Path.Combine(_config.OutputDirectory, "approvers.csv");
        await using var writer = new StreamWriter(path, false, System.Text.Encoding.UTF8);
        await using var csv = new CsvWriter(writer, CsvConfig);

        csv.WriteField("issue_iid"); csv.WriteField("mr_iid"); csv.WriteField("mr_state");
        csv.WriteField("is_closing"); csv.WriteField("approver");
        await csv.NextRecordAsync();

        foreach (var e in exports)
        {
            foreach (var mr in e.MergeRequests)
            {
                foreach (var a in mr.Approvers)
                {
                    csv.WriteField(e.Iid); csv.WriteField(mr.Iid); csv.WriteField(mr.State);
                    csv.WriteField(mr.IsClosing ? "true" : "false"); csv.WriteField(a);
                    await csv.NextRecordAsync();
                }
            }
        }
        Console.WriteLine($"  CSV écrit : {path}");
    }

    private static string Fmt(DateTimeOffset? dt) =>
        dt?.ToString("yyyy-MM-dd'T'HH:mm:ssK", CultureInfo.InvariantCulture) ?? "";
}
