using System.Text.Json.Serialization;

namespace Kpi.GitLab.Models;

public sealed class GitLabIssue
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("iid")] public long Iid { get; set; }
    [JsonPropertyName("project_id")] public long ProjectId { get; set; }
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("state")] public string State { get; set; } = "";
    [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; set; }
    [JsonPropertyName("updated_at")] public DateTimeOffset? UpdatedAt { get; set; }
    [JsonPropertyName("closed_at")] public DateTimeOffset? ClosedAt { get; set; }
    [JsonPropertyName("closed_by")] public GitLabUser? ClosedBy { get; set; }
    [JsonPropertyName("labels")] public List<string> Labels { get; set; } = new();
    [JsonPropertyName("weight")] public int? Weight { get; set; }
    [JsonPropertyName("assignees")] public List<GitLabUser> Assignees { get; set; } = new();
    [JsonPropertyName("author")] public GitLabUser? Author { get; set; }
    [JsonPropertyName("milestone")] public GitLabMilestone? Milestone { get; set; }
    [JsonPropertyName("web_url")] public string? WebUrl { get; set; }
    [JsonPropertyName("due_date")] public string? DueDate { get; set; }
}
