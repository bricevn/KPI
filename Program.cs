using System.Text.Json;
using GitLabExporter.Config;
using GitLabExporter.Export;
using GitLabExporter.Export.Models;
using GitLabExporter.Pipeline;
using GitLabExporter.Server;
using GitLabExporter.Views;
using Microsoft.Extensions.Configuration;

var builder = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables(prefix: "GITLAB_EXPORTER_");

var configRoot = builder.Build();
var appConfig = new AppConfig();
configRoot.Bind(appConfig);

bool viewsOnly = args.Any(a => a.Equals("--views-only", StringComparison.OrdinalIgnoreCase));
bool serve = args.Any(a => a.Equals("--serve", StringComparison.OrdinalIgnoreCase));
bool fetchLabels = args.Any(a => a.Equals("--fetch-labels", StringComparison.OrdinalIgnoreCase));
bool fetchMilestones = args.Any(a => a.Equals("--fetch-milestones", StringComparison.OrdinalIgnoreCase));
bool fetchAll = args.Any(a => a.Equals("--fetch-all", StringComparison.OrdinalIgnoreCase));
int port = ParsePort(args, defaultPort: 5050);

Console.WriteLine("=== GitLab Exporter ===");
Console.WriteLine($"Instance : {appConfig.GitLab.BaseUrl}");
Console.WriteLine($"Projet   : {appConfig.GitLab.ProjectId}");
Console.WriteLine($"Milestone: {appConfig.GitLab.Milestone}");
Console.WriteLine($"Labels   : {string.Join(", ", appConfig.Export.TrackedLabels)}");
Console.WriteLine($"Sortie   : {Path.GetFullPath(appConfig.Export.OutputDirectory)}");
if (viewsOnly) Console.WriteLine("Mode     : --views-only (lecture de issues.json, pas d'appel API)");
if (serve) Console.WriteLine($"Mode     : --serve (HTTP localhost:{port}, refresh à la demande)");
Console.WriteLine();

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

try
{
    if (serve)
    {
        // Vérifie qu'on a au moins un HTML à servir au démarrage.
        var htmlPath = Path.Combine(appConfig.Export.OutputDirectory, "views",
            $"release_{appConfig.GitLab.Milestone}_hypervisor.html");
        if (!File.Exists(htmlPath))
        {
            Console.WriteLine($"HTML absent ({htmlPath}). Lancement d'un export initial...");
            await ExportPipeline.RunFullExportAsync(appConfig, null, cts.Token);
        }

        await WebDashboard.RunAsync(appConfig, port, cts.Token);
        return 0;
    }

    if (fetchLabels)
    {
        Console.WriteLine("Récupération des labels du projet (couleurs incluses)...");
        using var client = new GitLabExporter.GitLab.GitLabClient(appConfig.GitLab);
        var labels = await client.GetProjectLabelsAsync(cts.Token);
        var labelsPath = Path.Combine(appConfig.Export.OutputDirectory, "labels.json");
        await GitLabExporter.Export.JsonExporter.WriteJsonAtomicAsync(labelsPath, labels, new JsonSerializerOptions { WriteIndented = true }, cts.Token);
        Console.WriteLine($"  -> {labels.Count} labels écrits dans {labelsPath}");
        return 0;
    }

    if (fetchMilestones)
    {
        Console.WriteLine("Récupération des milestones du projet (start_date / due_date)...");
        using var client = new GitLabExporter.GitLab.GitLabClient(appConfig.GitLab);
        var milestones = await client.GetProjectMilestonesAsync(cts.Token);
        var msPath = Path.Combine(appConfig.Export.OutputDirectory, "milestones.json");
        await GitLabExporter.Export.JsonExporter.WriteJsonAtomicAsync(msPath, milestones, new JsonSerializerOptions { WriteIndented = true }, cts.Token);
        Console.WriteLine($"  -> {milestones.Count} milestones écrites dans {msPath}");
        return 0;
    }

    if (fetchAll)
    {
        // Re-fetch de TOUT le projet (toutes milestones) → écrase issues.json avec l'ensemble.
        // Override = "" : effective vide = toutes les issues ; pas de merge (réécriture complète).
        Console.WriteLine("Mode     : --fetch-all (re-fetch complet de tout le projet)");
        await ExportPipeline.RunFullExportAsync(appConfig, null, cts.Token, "");
        return 0;
    }

    if (viewsOnly)
    {
        var jsonPath = Path.Combine(appConfig.Export.OutputDirectory, "issues.json");
        if (!File.Exists(jsonPath))
        {
            Console.Error.WriteLine($"Erreur : {jsonPath} introuvable. Lancez d'abord un export complet (sans --views-only).");
            return 1;
        }
        Console.WriteLine($"Lecture de {jsonPath}...");
        await using var fs = File.OpenRead(jsonPath);
        var exports = await JsonSerializer.DeserializeAsync<List<IssueExport>>(
            fs,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            cts.Token) ?? new();
        Console.WriteLine($"  -> {exports.Count} issues lues.");

        Console.WriteLine();
        Console.WriteLine("Génération des vues HTML...");
        await new HypervisorReleaseView()
            .GenerateAsync(
                appConfig.Export.OutputDirectory,
                appConfig.GitLab.Milestone,
                exports,
                appConfig.Export.TrackedTransitions,
                appConfig.Export.Teams,
                appConfig.Export.LabelPhases,
                cts.Token);

        Console.WriteLine();
        Console.WriteLine($"Terminé : {exports.Count} issues traitées.");
        return 0;
    }

    // Export complet par défaut.
    await ExportPipeline.RunFullExportAsync(appConfig, null, cts.Token);
    return 0;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Export annulé.");
    return 130;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Erreur : {ex.Message}");
    Console.Error.WriteLine(ex);
    return 1;
}

static int ParsePort(string[] args, int defaultPort)
{
    for (int i = 0; i < args.Length - 1; i++)
    {
        if (args[i].Equals("--port", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(args[i + 1], out var p) && p > 0)
            return p;
    }
    return defaultPort;
}
