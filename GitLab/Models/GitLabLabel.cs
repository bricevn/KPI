using System.Text.Json.Serialization;

namespace GitLabExporter.GitLab.Models;

public sealed class GitLabLabel
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("color")] public string? Color { get; set; }       // hex ex: "#fc9c2b"
    [JsonPropertyName("text_color")] public string? TextColor { get; set; } // hex
    [JsonPropertyName("description")] public string? Description { get; set; }
}
