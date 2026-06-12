using System.Text.Encodings.Web;
using System.Text.Json;
using Kpi.Export.Models;

namespace Kpi.Export;

public static class JsonExporter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static async Task WriteAsync(string outputDirectory, List<IssueExport> exports, CancellationToken ct)
    {
        var path = Path.Combine(outputDirectory, "issues.json");
        await WriteJsonAtomicAsync(path, exports, Options, ct);
        Console.WriteLine($"  JSON écrit : {path}");
    }

    /// <summary>
    /// Écriture atomique (fichier temporaire + rename) : aucun lecteur concurrent (ex. /api/data
    /// qui relit issues.json) ne peut tomber sur un fichier tronqué pendant la sérialisation.
    /// </summary>
    public static async Task WriteJsonAtomicAsync<T>(string path, T value, JsonSerializerOptions options, CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var tmp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var fs = File.Create(tmp))
                await JsonSerializer.SerializeAsync(fs, value, options, ct);
            File.Move(tmp, path, overwrite: true);
        }
        catch
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { /* best effort */ }
            throw;
        }
    }
}
