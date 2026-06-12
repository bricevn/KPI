using System.Text.Json.Serialization;

namespace Kpi.GitLab.Models;

public sealed class GitLabMergeRequest
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("iid")] public long Iid { get; set; }
    [JsonPropertyName("project_id")] public long ProjectId { get; set; }
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("state")] public string State { get; set; } = "";
    [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; set; }
    [JsonPropertyName("updated_at")] public DateTimeOffset? UpdatedAt { get; set; }
    [JsonPropertyName("merged_at")] public DateTimeOffset? MergedAt { get; set; }
    [JsonPropertyName("closed_at")] public DateTimeOffset? ClosedAt { get; set; }
    [JsonPropertyName("author")] public GitLabUser? Author { get; set; }
    [JsonPropertyName("assignees")] public List<GitLabUser> Assignees { get; set; } = new();
    [JsonPropertyName("reviewers")] public List<GitLabUser> Reviewers { get; set; } = new();
    [JsonPropertyName("merged_by")] public GitLabUser? MergedBy { get; set; }
    [JsonPropertyName("labels")] public List<string> Labels { get; set; } = new();
    [JsonPropertyName("web_url")] public string? WebUrl { get; set; }
    [JsonPropertyName("source_branch")] public string? SourceBranch { get; set; }
    [JsonPropertyName("target_branch")] public string? TargetBranch { get; set; }
}
