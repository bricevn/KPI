using System.Text.Json.Serialization;

namespace Kpi.GitLab.Models;

/// <summary>Épic GitLab (niveau GROUPE, feature Premium/Ultimate). Réduit à ce que consomme
/// l'adhérence roadmap (état + titre + URL). Les issues de l'épic sont récupérées à part.</summary>
public sealed class GitLabEpic
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("iid")] public long Iid { get; set; }
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("state")] public string State { get; set; } = ""; // opened | closed
    [JsonPropertyName("web_url")] public string? WebUrl { get; set; }
}
