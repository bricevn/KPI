using System.Text.Json;
using GitLabExporter.Config;
using GitLabExporter.Export;
using GitLabExporter.Export.Models;
using GitLabExporter.GitLab;
using GitLabExporter.Views;

namespace GitLabExporter.Pipeline;

/// <summary>
/// Pipeline d'export complet (API → JSON → CSV → vues HTML).
/// Centralise la logique pour qu'elle soit appelable depuis Program.cs ou depuis le serveur HTTP.
/// </summary>
public static class ExportPipeline
{
    public static async Task RunFullExportAsync(
        AppConfig config,
        Action<int, int>? onProgress,
        CancellationToken ct,
        string? milestoneOverride = null)
    {
        // milestoneOverride : null = utilise la config (Milestone d'appsettings),
        //                     "" = TOUTES les milestones, "X" = nom précis.
        using var client = new GitLabClient(config.GitLab);
        var service = new ExportService(client, config.GitLab, config.Export);
        var exports = await service.BuildIssueExportsAsync(ct, onProgress, milestoneOverride);

        // Récupération des labels du projet (avec couleurs) — sauvegardé dans labels.json
        // pour être réutilisable par --views-only sans refaire d'appel API.
        try
        {
            var labels = await client.GetProjectLabelsAsync(ct);
            var labelsPath = Path.Combine(config.Export.OutputDirectory, "labels.json");
            await JsonExporter.WriteJsonAtomicAsync(labelsPath, labels, new JsonSerializerOptions { WriteIndented = true }, ct);
            Console.WriteLine($"  -> {labels.Count} labels (avec couleurs) sauvegardés dans labels.json");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [warn] Récupération des labels impossible : {ex.Message}");
        }

        // Récupération des milestones du projet (avec start_date / due_date) — sauvegardé dans milestones.json.
        try
        {
            var milestones = await client.GetProjectMilestonesAsync(ct);
            var msPath = Path.Combine(config.Export.OutputDirectory, "milestones.json");
            await JsonExporter.WriteJsonAtomicAsync(msPath, milestones, new JsonSerializerOptions { WriteIndented = true }, ct);
            Console.WriteLine($"  -> {milestones.Count} milestones (avec dates) sauvegardés dans milestones.json");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [warn] Récupération des milestones impossible : {ex.Message}");
        }

        // Phase 2 — merge : si on a refetché uniquement une milestone précise (override non-vide),
        // on conserve les issues des autres milestones de l'ancien issues.json et on remplace
        // uniquement celles de la milestone refetchée.
        if (!string.IsNullOrWhiteSpace(milestoneOverride))
        {
            var jsonPath = Path.Combine(config.Export.OutputDirectory, "issues.json");
            if (File.Exists(jsonPath))
            {
                try
                {
                    List<IssueExport>? existing;
                    await using (var fs = File.OpenRead(jsonPath))
                    {
                        existing = await JsonSerializer.DeserializeAsync<List<IssueExport>>(
                            fs,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                            ct);
                    }
                    if (existing != null && existing.Count > 0)
                    {
                        var preserved = existing
                            .Where(e => !string.Equals(e.Milestone, milestoneOverride, StringComparison.OrdinalIgnoreCase))
                            .ToList();
                        var newIids = new HashSet<long>(exports.Select(e => e.Iid));
                        // Filtrer aussi pour ne pas avoir de doublons par IID (sécurité).
                        preserved = preserved.Where(e => !newIids.Contains(e.Iid)).ToList();
                        var merged = preserved.Concat(exports).OrderBy(e => e.Iid).ToList();
                        Console.WriteLine($"  Merge : {preserved.Count} issues conservées (autres milestones) + {exports.Count} fraîchement extraites = {merged.Count}.");
                        exports = merged;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  [warn] Merge impossible ({ex.Message}). issues.json sera écrasé avec les issues de la milestone seulement.");
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine("Écriture des fichiers de sortie...");
        await JsonExporter.WriteAsync(config.Export.OutputDirectory, exports, ct);

        var csvExporter = new CsvExporter(config.Export);
        await csvExporter.WriteAsync(exports, ct);

        Console.WriteLine();
        Console.WriteLine("Génération des vues HTML...");
        await new HypervisorReleaseView()
            .GenerateAsync(
                config.Export.OutputDirectory,
                config.GitLab.Milestone,
                exports,
                config.Export.TrackedTransitions,
                config.Export.Teams,
                config.Export.LabelPhases,
                ct);

        Console.WriteLine();
        Console.WriteLine($"Terminé : {exports.Count} issues traitées.");
    }
}
