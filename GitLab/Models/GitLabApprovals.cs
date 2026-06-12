using System.Text.Json.Serialization;

namespace GitLabExporter.GitLab.Models;

public sealed class GitLabApprovals
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("iid")] public long Iid { get; set; }
    [JsonPropertyName("project_id")] public long ProjectId { get; set; }
    [JsonPropertyName("approvals_required")] public int? ApprovalsRequired { get; set; }
    [JsonPropertyName("approvals_left")] public int? ApprovalsLeft { get; set; }
    [JsonPropertyName("approved")] public bool? Approved { get; set; }
    [JsonPropertyName("approved_by")] public List<ApprovalEntry> ApprovedBy { get; set; } = new();
}

public sealed class ApprovalEntry
{
    [JsonPropertyName("user")] public GitLabUser? User { get; set; }
}
