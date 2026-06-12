using System.Text.Json.Serialization;

namespace GitLabExporter.GitLab.Models;

public sealed class ResourceLabelEvent
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("user")] public GitLabUser? User { get; set; }
    [JsonPropertyName("created_at")] public DateTimeOffset CreatedAt { get; set; }
    [JsonPropertyName("resource_type")] public string? ResourceType { get; set; }
    [JsonPropertyName("resource_id")] public long ResourceId { get; set; }
    [JsonPropertyName("label")] public LabelRef? Label { get; set; }
    [JsonPropertyName("action")] public string Action { get; set; } = ""; // "add" or "remove"
}

public sealed class LabelRef
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("color")] public string? Color { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
}
