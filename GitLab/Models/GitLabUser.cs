using System.Text.Json.Serialization;

namespace GitLabExporter.GitLab.Models;

public sealed class GitLabUser
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("username")] public string Username { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("state")] public string? State { get; set; }
    [JsonPropertyName("email")] public string? Email { get; set; }
}
