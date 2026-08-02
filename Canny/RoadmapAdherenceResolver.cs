using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Kpi.Config;
using Kpi.GitLab;
using Kpi.GitLab.Models;

namespace Kpi.Canny;

/// <summary>
/// Résout l'adhérence roadmap (KPI « Roadmap Adherence ») : pour chaque sujet Canny « [N] … » lié à des
/// épics/issues GitLab, récupère via l'API l'état des issues (celles de l'épic, et/ou les issues directes)
/// et calcule <c>adherent = statut Canny « complete » ET toutes les issues liées fermées</c>.
/// <para>Utilise le GroupToken (read_api) : épics au niveau groupe + issues de projets divers. Tolérant aux
/// erreurs par cible (épic supprimé, plan sans épics) — une cible non résolue reste « non fermée ».</para>
/// Produit un JSON injecté au client en <c>window.__ROADMAP__</c> (même mécanisme que <c>__CANNY__</c>).
/// </summary>
public static class RoadmapAdherenceResolver
{
    private static readonly JsonSerializerOptions OutJson = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static async Task<string> ResolveAsync(AppConfig cfg, List<RoadmapTopicRefs> topics, CancellationToken ct)
    {
        // Cibles UNIQUES (un même épic/issue peut être lié par plusieurs sujets → une seule requête).
        var epicKeys = topics.SelectMany(t => t.Epics)
            .GroupBy(e => e.Group + "!" + e.Iid).Select(g => g.First()).ToList();
        var issueKeys = topics.SelectMany(t => t.Issues)
            .GroupBy(i => i.Project + "!" + i.Iid).Select(g => g.First()).ToList();

        var epicData = new ConcurrentDictionary<string, ResolvedEpic>();
        var issueData = new ConcurrentDictionary<string, ResolvedIssue>();

        if (epicKeys.Count > 0 || issueKeys.Count > 0)
        {
            using var client = new GitLabClient(cfg.PrimaryGitLab());
            using var gate = new SemaphoreSlim(8); // borne le fan-out de requêtes GitLab

            var epicTasks = epicKeys.Select(async e =>
            {
                await gate.WaitAsync(ct);
                try
                {
                    var epic = await client.GetGroupEpicAsync(e.Group, e.Iid, ct);
                    var issues = await client.GetGroupEpicIssuesAsync(e.Group, e.Iid, ct);
                    epicData[e.Group + "!" + e.Iid] = new ResolvedEpic
                    {
                        Title = epic?.Title ?? $"Epic &{e.Iid}",
                        State = epic?.State ?? "unknown",
                        WebUrl = epic?.WebUrl,
                        Issues = issues,
                    };
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { Console.Error.WriteLine($"  [warn] épic {e.Group}&{e.Iid} : {ex.Message}"); }
                finally { gate.Release(); }
            });

            var issueTasks = issueKeys.Select(async i =>
            {
                await gate.WaitAsync(ct);
                try
                {
                    var iss = await client.GetIssueByRefAsync(i.Project, i.Iid, ct);
                    issueData[i.Project + "!" + i.Iid] = iss != null
                        ? new ResolvedIssue { Title = iss.Title, State = iss.State, WebUrl = iss.WebUrl }
                        : new ResolvedIssue { Title = $"#{i.Iid}", State = "unknown", WebUrl = null };
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { Console.Error.WriteLine($"  [warn] issue {i.Project}#{i.Iid} : {ex.Message}"); }
                finally { gate.Release(); }
            });

            await Task.WhenAll(epicTasks.Concat(issueTasks));
        }

        var outTopics = topics.Select(t =>
        {
            var epics = t.Epics.Select(e =>
            {
                epicData.TryGetValue(e.Group + "!" + e.Iid, out var re);
                var iss = re?.Issues ?? new List<GitLabIssue>();
                int total = iss.Count, closed = iss.Count(x => x.State == "closed");
                return new
                {
                    group = e.Group,
                    iid = e.Iid,
                    title = re?.Title ?? $"Epic &{e.Iid}",
                    state = re?.State ?? "unknown",
                    webUrl = re?.WebUrl,
                    total,
                    closed,
                    allClosed = total > 0 && closed == total,
                    issues = iss.OrderByDescending(x => x.Iid)
                        .Select(x => new { iid = x.Iid, title = x.Title, state = x.State, webUrl = x.WebUrl }).ToList(),
                };
            }).ToList();

            var directIssues = t.Issues.Select(i =>
            {
                issueData.TryGetValue(i.Project + "!" + i.Iid, out var ri);
                var st = ri?.State ?? "unknown";
                return new { project = i.Project, iid = i.Iid, title = ri?.Title ?? $"#{i.Iid}", state = st, webUrl = ri?.WebUrl, closed = st == "closed" };
            }).ToList();

            int tTotal = epics.Sum(e => e.total) + directIssues.Count;
            int tClosed = epics.Sum(e => e.closed) + directIssues.Count(d => d.closed);
            bool hasTarget = epics.Count > 0 || directIssues.Count > 0;
            // Côté GitLab « fait » : chaque épic a toutes ses issues fermées ET chaque issue directe est fermée.
            bool gitlabDone = hasTarget && epics.All(e => e.allClosed) && directIssues.All(d => d.closed);
            bool adherent = t.Complete && gitlabDone;

            return new
            {
                postId = t.PostId,
                title = t.Title,
                url = t.Url,
                status = t.Status,
                complete = t.Complete,
                roadmaps = t.Roadmaps,
                epics,
                issues = directIssues,
                targetTotal = tTotal,
                targetClosed = tClosed,
                gitlabDone,
                adherent,
            };
        })
        .OrderByDescending(x => x.adherent)
        .ThenBy(x => x.title, StringComparer.Ordinal)
        .ToList();

        var payload = new
        {
            generatedAt = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            summary = new { total = outTopics.Count, adherent = outTopics.Count(x => x.adherent) },
            topics = outTopics,
        };
        return JsonSerializer.Serialize(payload, OutJson);
    }

    private sealed class ResolvedEpic
    {
        public string Title { get; set; } = "";
        public string State { get; set; } = "";
        public string? WebUrl { get; set; }
        public List<GitLabIssue> Issues { get; set; } = new();
    }

    private sealed class ResolvedIssue
    {
        public string Title { get; set; } = "";
        public string State { get; set; } = "";
        public string? WebUrl { get; set; }
    }
}
