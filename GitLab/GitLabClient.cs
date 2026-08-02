using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Web;
using Kpi.Config;
using Kpi.GitLab.Models;

namespace Kpi.GitLab;

public sealed class GitLabClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _projectIdEncoded;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public GitLabClient(GitLabConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.BaseUrl))
            throw new InvalidOperationException("GitLab:BaseUrl est vide. Renseignez appsettings.json.");
        if (string.IsNullOrWhiteSpace(config.PrivateToken) || config.PrivateToken.StartsWith("REMPLACEZ"))
            throw new InvalidOperationException("GitLab:PrivateToken est vide ou non configuré.");
        if (string.IsNullOrWhiteSpace(config.ProjectId))
            throw new InvalidOperationException("GitLab:ProjectId est vide.");

        var handler = new HttpClientHandler();
        if (config.AllowSelfSignedCertificates)
        {
            handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
        }

        _http = new HttpClient(handler)
        {
            BaseAddress = new Uri(config.BaseUrl.TrimEnd('/') + "/api/v4/"),
            Timeout = TimeSpan.FromSeconds(config.RequestTimeoutSeconds),
        };
        _http.DefaultRequestHeaders.Add("PRIVATE-TOKEN", config.PrivateToken);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        // Pour l'API GitLab, on peut passer l'ID numérique ou le chemin URL-encoded (namespace%2Fprojet).
        _projectIdEncoded = HttpUtility.UrlEncode(config.ProjectId);
    }

    public async Task<List<GitLabIssue>> GetIssuesByMilestoneAsync(string? milestone, CancellationToken ct, string? assigneeUsername = null)
    {
        // milestone null/vide = TOUTES les issues du projet. assigneeUsername renseigné = extraction SCOPÉE
        // (issues assignées à cet utilisateur) — utilisé pour le refresh par rôle (membre/lead).
        var path = $"projects/{_projectIdEncoded}/issues?scope=all&per_page=100";
        if (!string.IsNullOrWhiteSpace(milestone)) path += "&milestone=" + HttpUtility.UrlEncode(milestone);
        if (!string.IsNullOrWhiteSpace(assigneeUsername)) path += "&assignee_username=" + HttpUtility.UrlEncode(assigneeUsername);
        return await GetAllPagesAsync<GitLabIssue>(path, ct);
    }

    public async Task<List<ResourceLabelEvent>> GetLabelEventsAsync(long issueIid, CancellationToken ct)
    {
        var path = $"projects/{_projectIdEncoded}/issues/{issueIid}/resource_label_events?per_page=100";
        return await GetAllPagesAsync<ResourceLabelEvent>(path, ct);
    }

    public async Task<List<GitLabNote>> GetIssueNotesAsync(long issueIid, CancellationToken ct)
    {
        var path = $"projects/{_projectIdEncoded}/issues/{issueIid}/notes?per_page=100";
        return await GetAllPagesAsync<GitLabNote>(path, ct);
    }

    public async Task<List<GitLabMergeRequest>> GetClosingMergeRequestsAsync(long issueIid, CancellationToken ct)
    {
        var path = $"projects/{_projectIdEncoded}/issues/{issueIid}/closed_by?per_page=100";
        return await GetAllPagesAsync<GitLabMergeRequest>(path, ct);
    }

    public async Task<List<GitLabMergeRequest>> GetRelatedMergeRequestsAsync(long issueIid, CancellationToken ct)
    {
        var path = $"projects/{_projectIdEncoded}/issues/{issueIid}/related_merge_requests?per_page=100";
        return await GetAllPagesAsync<GitLabMergeRequest>(path, ct);
    }

    public async Task<GitLabApprovals?> GetMergeRequestApprovalsAsync(long mrIid, CancellationToken ct)
    {
        var path = $"projects/{_projectIdEncoded}/merge_requests/{mrIid}/approvals";
        using var resp = await _http.GetAsync(path, ct);
        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        if (resp.StatusCode == HttpStatusCode.Forbidden) return null; // CE sans l'EE feature
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        return await JsonSerializer.DeserializeAsync<GitLabApprovals>(stream, _jsonOptions, ct);
    }

    // ---- Adhérence roadmap : épics de GROUPE + issues par chemin ARBITRAIRE ----------------------
    // Les liens Canny→GitLab pointent des épics (/groups/<grp>/-/epics/N) et des issues de projets
    // divers (/<projet>/-/issues/N), hors du projet configuré. On cible donc le chemin explicitement.
    // Le GroupToken (read_api) couvre le groupe et ses projets. Tolérant : 404/403 → null/vide (épic
    // supprimé, ou plan sans épics) plutôt qu'une exception qui ferait tomber toute l'extraction.

    /// <summary>Épic de groupe (état + titre). null si absent/inaccessible.</summary>
    public async Task<GitLabEpic?> GetGroupEpicAsync(string groupPath, long epicIid, CancellationToken ct)
    {
        var path = $"groups/{HttpUtility.UrlEncode(groupPath)}/epics/{epicIid}";
        using var resp = await _http.GetAsync(path, ct);
        if (resp.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden) return null;
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        return await JsonSerializer.DeserializeAsync<GitLabEpic>(stream, _jsonOptions, ct);
    }

    /// <summary>Issues rattachées à un épic de groupe (tous projets confondus). Vide si inaccessible.</summary>
    public async Task<List<GitLabIssue>> GetGroupEpicIssuesAsync(string groupPath, long epicIid, CancellationToken ct)
    {
        var path = $"groups/{HttpUtility.UrlEncode(groupPath)}/epics/{epicIid}/issues?per_page=100";
        try { return await GetAllPagesAsync<GitLabIssue>(path, ct); }
        catch (HttpRequestException ex) { Console.Error.WriteLine($"  [warn] issues de l'épic {groupPath}&{epicIid} indisponibles : {ex.Message}"); return new(); }
    }

    /// <summary>Une issue d'un projet ciblé par son CHEMIN (namespace/projet). null si absente/inaccessible.</summary>
    public async Task<GitLabIssue?> GetIssueByRefAsync(string projectPath, long iid, CancellationToken ct)
    {
        var path = $"projects/{HttpUtility.UrlEncode(projectPath)}/issues/{iid}";
        using var resp = await _http.GetAsync(path, ct);
        if (resp.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden) return null;
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        return await JsonSerializer.DeserializeAsync<GitLabIssue>(stream, _jsonOptions, ct);
    }

    public async Task<List<GitLabLabel>> GetProjectLabelsAsync(CancellationToken ct)
    {
        // GitLab : on inclut les labels héritage d'ancêtres (groupe) avec include_ancestor_groups=true.
        var path = $"projects/{_projectIdEncoded}/labels?with_counts=false&include_ancestor_groups=true&per_page=100";
        return await GetAllPagesAsync<GitLabLabel>(path, ct);
    }

    public async Task<List<GitLabMilestone>> GetProjectMilestonesAsync(CancellationToken ct)
    {
        // include_parent_milestones=true → récupère aussi les milestones héritées du groupe parent (depuis GitLab 16.5).
        var path = $"projects/{_projectIdEncoded}/milestones?include_parent_milestones=true&per_page=100";
        return await GetAllPagesAsync<GitLabMilestone>(path, ct);
    }

    private async Task<List<T>> GetAllPagesAsync<T>(string startingPath, CancellationToken ct)
    {
        var results = new List<T>();
        string? nextPath = startingPath;
        while (!string.IsNullOrEmpty(nextPath))
        {
            using var resp = await _http.GetAsync(nextPath, ct);
            resp.EnsureSuccessStatusCode();

            await using var stream = await resp.Content.ReadAsStreamAsync(ct);
            var page = await JsonSerializer.DeserializeAsync<List<T>>(stream, _jsonOptions, ct);
            if (page != null) results.AddRange(page);

            nextPath = ExtractNextLink(resp, nextPath);
        }
        return results;
    }

    private string? ExtractNextLink(HttpResponseMessage resp, string currentPath)
    {
        // GitLab utilise le header Link RFC 5988 pour la pagination.
        if (resp.Headers.TryGetValues("Link", out var linkValues))
        {
            foreach (var headerValue in linkValues)
            {
                foreach (var part in headerValue.Split(','))
                {
                    var trimmed = part.Trim();
                    if (!trimmed.Contains("rel=\"next\"", StringComparison.OrdinalIgnoreCase)) continue;
                    var start = trimmed.IndexOf('<');
                    var end = trimmed.IndexOf('>');
                    if (start >= 0 && end > start)
                    {
                        var absoluteUrl = trimmed.Substring(start + 1, end - start - 1);
                        // On garde l'URL absolue car la BaseAddress est déjà gérée par HttpClient pour
                        // les chemins relatifs ; on veut donc la transformer pour qu'elle reste dans le scope.
                        if (Uri.TryCreate(absoluteUrl, UriKind.Absolute, out var uri))
                        {
                            return uri.PathAndQuery.StartsWith("/api/v4/")
                                ? uri.PathAndQuery.Substring("/api/v4/".Length)
                                : uri.PathAndQuery;
                        }
                    }
                }
            }
        }

        // Fallback : on regarde X-Next-Page.
        if (resp.Headers.TryGetValues("X-Next-Page", out var nextPageValues))
        {
            var nextPage = nextPageValues.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(nextPage) && int.TryParse(nextPage, out var n) && n > 0)
            {
                var sep = currentPath.Contains('?') ? '&' : '?';
                // Retire éventuellement le paramètre page existant.
                var cleaned = System.Text.RegularExpressions.Regex.Replace(currentPath, @"([?&])page=\d+&?", "$1");
                cleaned = cleaned.TrimEnd('&', '?');
                return $"{cleaned}{sep}page={n}";
            }
        }
        return null;
    }

    public void Dispose() => _http.Dispose();
}
