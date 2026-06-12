using System.Text.Json.Serialization;

namespace GitLabExporter.GitLab.Models;

public sealed class GitLabMilestone
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("iid")] public long Iid { get; set; }
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("state")] public string? State { get; set; }
    [JsonPropertyName("due_date")] public string? DueDate { get; set; }
    [JsonPropertyName("start_date")] public string? StartDate { get; set; }
}
