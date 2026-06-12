using System.Text.Json.Serialization;

namespace GitLabExporter.GitLab.Models;

public sealed class GitLabNote
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("author")] public GitLabUser? Author { get; set; }
    [JsonPropertyName("created_at")] public DateTimeOffset CreatedAt { get; set; }
    [JsonPropertyName("system")] public bool System { get; set; }
    [JsonPropertyName("body")] public string? Body { get; set; }
}
