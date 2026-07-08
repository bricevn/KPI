using System.Net.Http.Headers;
using System.Text.Json;
using Kpi.Config;
using Kpi.Export;
using Kpi.Export.Models;
using Kpi.GitLab;
using Kpi.GitLab.Models;
using Kpi.Views;

namespace Kpi.Pipeline;

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
        var gl = config.PrimaryGitLab(); // 1c-D : 1er serveur (le bloc GitLab legacy a été retiré)
        using var client = new GitLabClient(gl);
        var service = new ExportService(client, gl, config.Export);
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
        await new DashboardView()
            .GenerateAsync(
                config.Export.OutputDirectory,
                gl.Milestone,
                exports,
                config.Export.TrackedTransitions,
                config.Export.Teams,
                config.Export.LabelPhases,
                config.Export.Periods,
                ct);

        Console.WriteLine();
        Console.WriteLine($"Terminé : {exports.Count} issues traitées.");
    }

    // ====================================================================
    // v2 — Extraction MULTI-SERVEURS, cloisonnée par serveur et CHIFFRÉE au repos.
    // Chemin additif : n'altère pas RunFullExportAsync (legacy) tant que le dashboard
    // n'a pas basculé (1c). Réutilise GitLabClient/ExportService par (serveur, projet).
    // ====================================================================
    private static readonly JsonSerializerOptions _storeJson = new() { WriteIndented = false };
    private static readonly JsonSerializerOptions _readJson = new() { PropertyNameCaseInsensitive = true };

    /// <param name="projectFilter">Id GitLab d'un projet (chaîne) pour une acquisition CIBLÉE ; null = tous les projets configurés.</param>
    /// <param name="milestoneFilter">Titre d'une milestone pour une acquisition CIBLÉE ; null = toutes les issues.
    /// En mode ciblé, le store existant est MERGÉ : seules les issues de la portée sont remplacées.</param>
    public static async Task RunMultiServerExportAsync(AppConfig config, Action<int, int>? onProgress, CancellationToken ct,
        string? projectFilter = null, string? milestoneFilter = null)
    {
        projectFilter = string.IsNullOrWhiteSpace(projectFilter) ? null : projectFilter.Trim();
        milestoneFilter = string.IsNullOrWhiteSpace(milestoneFilter) ? null : milestoneFilter.Trim();
        var scoped = projectFilter != null || milestoneFilter != null;

        var servers = config.ResolveServers();
        if (servers.Count == 0) { Console.WriteLine("Aucun serveur GitLab configuré (Servers vide)."); return; }

        foreach (var server in servers)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(server.Id) || string.IsNullOrWhiteSpace(server.BaseUrl) || string.IsNullOrWhiteSpace(server.GroupToken))
            {
                Console.WriteLine($"  [skip] serveur '{server.Id}' incomplet (Id/BaseUrl/GroupToken requis).");
                continue;
            }

            var serverDir = Path.Combine(config.Export.OutputDirectory, SafeSegment(server.Id));
            var projectIds = server.ProjectIds is { Count: > 0 }
                ? server.ProjectIds
                : await ListProjectsAsync(server, ct);
            if (projectFilter != null)
            {
                projectIds = projectIds.Where(p => p == projectFilter).ToList();
                if (projectIds.Count == 0) { Console.WriteLine($"  [skip] serveur '{server.Id}' : projet {projectFilter} absent de sa configuration."); continue; }
            }
            Console.WriteLine($"== Serveur '{server.Id}' ({server.BaseUrl}) : {projectIds.Count} projet(s){(scoped ? " · acquisition ciblée" : "")} ==");

            var allIssues = new List<IssueExport>();
            var labels = new Dictionary<string, GitLabLabel>(StringComparer.OrdinalIgnoreCase);
            var milestones = new Dictionary<string, GitLabMilestone>(StringComparer.OrdinalIgnoreCase);
            var skippedPids = new HashSet<string>(); // projets « Aucune » sautés CE run (données existantes à préserver)

            foreach (var pid in projectIds)
            {
                ct.ThrowIfCancellationRequested();
                // « Aucune » (sentinelle __skip__) : le projet est SAUTÉ lors des runs globaux — sauf si
                // la régénération le CIBLE explicitement (projectFilter) : la demande explicite prime,
                // il est alors extrait sans borne de départ.
                var skipCfg = int.TryParse(pid, out var pidPre)
                    && (config.Export.StartMilestones ?? new()).TryGetValue(pidPre, out var preTitle)
                    && preTitle == ExportConfig.SkipExtractionSentinel;
                if (skipCfg && projectFilter == null)
                {
                    skippedPids.Add(pid);
                    Console.WriteLine($"  -- projet {pid} : extraction SAUTÉE (« Aucune » au setup) — données existantes conservées --");
                    continue;
                }
                var glCfg = new GitLabConfig
                {
                    BaseUrl = server.BaseUrl,
                    PrivateToken = server.GroupToken,
                    ProjectId = pid,
                    Milestone = "",  // pas de prérequis milestone : on extrait TOUTES les issues du projet
                    AllowSelfSignedCertificates = server.AllowSelfSignedCertificates,
                    RequestTimeoutSeconds = server.RequestTimeoutSeconds,
                };
                using var client = new GitLabClient(glCfg);
                var service = new ExportService(client, glCfg, config.Export);
                Console.WriteLine($"  -- projet {pid} --");
                // Catalogue des milestones AVANT les issues : alimente milestones.json ET sert de référence
                // de dates au filtre « milestone de départ » ci-dessous.
                var projMs = new List<GitLabMilestone>();
                try { projMs = await client.GetProjectMilestonesAsync(ct); foreach (var m in projMs) if (!string.IsNullOrEmpty(m.Title)) milestones[m.Title] = m; }
                catch (Exception ex) { Console.WriteLine($"     [warn] milestones projet {pid} : {ex.Message}"); }
                // Milestone de DÉPART (config /setup) : les issues des milestones ANTÉRIEURES (date due/start
                // strictement plus ancienne) sont exclues. Sans milestone, ou milestone inconnue du catalogue
                // (pas de date comparable) → conservées.
                Func<GitLabIssue, bool>? startFilter = null;
                if (int.TryParse(pid, out var pidInt)
                    && (config.Export.StartMilestones ?? new()).TryGetValue(pidInt, out var startMsTitle)
                    && !string.IsNullOrWhiteSpace(startMsTitle)
                    && startMsTitle != ExportConfig.SkipExtractionSentinel) // ciblage explicite d'un projet « Aucune » → sans borne
                {
                    var boundary = projMs.FirstOrDefault(m => string.Equals(m.Title, startMsTitle, StringComparison.OrdinalIgnoreCase));
                    var boundaryDate = boundary?.DueDate ?? boundary?.StartDate; // ISO yyyy-MM-dd → comparaison ordinale
                    if (!string.IsNullOrEmpty(boundaryDate))
                    {
                        var msDates = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                        foreach (var m in projMs) if (!string.IsNullOrEmpty(m.Title) && !msDates.ContainsKey(m.Title)) msDates[m.Title] = m.DueDate ?? m.StartDate;
                        Console.WriteLine($"     départ de l'export : milestone « {startMsTitle} » (≥ {boundaryDate})");
                        startFilter = iss =>
                        {
                            var t = iss.Milestone?.Title;
                            if (string.IsNullOrEmpty(t) || string.Equals(t, startMsTitle, StringComparison.OrdinalIgnoreCase)) return true;
                            return !(msDates.TryGetValue(t, out var d) && !string.IsNullOrEmpty(d) && string.CompareOrdinal(d, boundaryDate) < 0);
                        };
                    }
                    else Console.WriteLine($"     [warn] milestone de départ « {startMsTitle} » introuvable/sans date → historique complet.");
                }
                var issues = await service.BuildIssueExportsAsync(ct, onProgress, milestoneFilter ?? "", startFilter);
                allIssues.AddRange(issues);  // chaque IssueExport porte déjà son ProjectId (groupement)
                try { foreach (var l in await client.GetProjectLabelsAsync(ct)) if (!string.IsNullOrEmpty(l.Name)) labels[l.Name] = l; }
                catch (Exception ex) { Console.WriteLine($"     [warn] labels projet {pid} : {ex.Message}"); }
            }

            if (scoped || skippedPids.Count > 0)
            {
                // Acquisition CIBLÉE et/ou projets SAUTÉS (« Aucune ») → merge avec le store existant :
                // on ne remplace que la portée réellement extraite ; le reste (autres projets/milestones,
                // projets sautés) est conservé — un run global réécrivant tout les effacerait sinon.
                var prevTxt = SecureStore.TryReadDecrypted(server.Id, Path.Combine(serverDir, "issues.json"));
                if (prevTxt != null)
                {
                    var existing = JsonSerializer.Deserialize<List<IssueExport>>(prevTxt, _readJson) ?? new();
                    bool InScope(IssueExport e) =>
                        (projectFilter == null || e.ProjectId.ToString() == projectFilter)
                        && (milestoneFilter == null || string.Equals(e.Milestone, milestoneFilter, StringComparison.OrdinalIgnoreCase));
                    bool Keep(IssueExport e) => skippedPids.Contains(e.ProjectId.ToString()) || (scoped && !InScope(e));
                    var newKeys = new HashSet<(long, long)>(allIssues.Select(e => (e.ProjectId, e.Iid)));
                    var preserved = existing.Where(e => Keep(e) && !newKeys.Contains((e.ProjectId, e.Iid))).ToList();
                    Console.WriteLine($"  Merge : {preserved.Count} issues conservées (hors portée / projets sautés) + {allIssues.Count} fraîchement extraites.");
                    allIssues = preserved.Concat(allIssues).OrderBy(e => e.ProjectId).ThenBy(e => e.Iid).ToList();
                }
                // Catalogues labels/milestones : réinjecter l'existant (autres projets) — le frais gagne.
                var prevLb = SecureStore.TryReadDecrypted(server.Id, Path.Combine(serverDir, "labels.json"));
                if (prevLb != null)
                    foreach (var l in JsonSerializer.Deserialize<List<GitLabLabel>>(prevLb, _readJson) ?? new())
                        if (!string.IsNullOrEmpty(l.Name) && !labels.ContainsKey(l.Name)) labels[l.Name] = l;
                var prevMs = SecureStore.TryReadDecrypted(server.Id, Path.Combine(serverDir, "milestones.json"));
                if (prevMs != null)
                    foreach (var m in JsonSerializer.Deserialize<List<GitLabMilestone>>(prevMs, _readJson) ?? new())
                        if (!string.IsNullOrEmpty(m.Title) && !milestones.ContainsKey(m.Title)) milestones[m.Title] = m;
            }

            // Écriture CHIFFRÉE (sous-clé du serveur) — cloisonnée sous output/<serverId>/.
            await SecureStore.WriteEncryptedAsync(server.Id, Path.Combine(serverDir, "issues.json"),
                JsonSerializer.Serialize(allIssues, _storeJson), ct);
            await SecureStore.WriteEncryptedAsync(server.Id, Path.Combine(serverDir, "labels.json"),
                JsonSerializer.Serialize(labels.Values.ToList(), _storeJson), ct);
            await SecureStore.WriteEncryptedAsync(server.Id, Path.Combine(serverDir, "milestones.json"),
                JsonSerializer.Serialize(milestones.Values.ToList(), _storeJson), ct);
            Console.WriteLine($"  -> {allIssues.Count} issues chiffrées dans {serverDir}/ ({projectIds.Count} projets, {labels.Count} labels, {milestones.Count} milestones)");

            // Auto-contrôle d'intégrité : on relit + déchiffre ce qu'on vient d'écrire (détecte une clé KO).
            var back = SecureStore.TryReadDecrypted(server.Id, Path.Combine(serverDir, "issues.json"));
            var backCount = back == null ? -1 : (JsonSerializer.Deserialize<List<IssueExport>>(back, _storeJson)?.Count ?? -1);
            Console.WriteLine(backCount == allIssues.Count
                ? $"  -> vérif chiffrement OK : {backCount} issues relues/déchiffrées."
                : $"  [warn] vérif chiffrement : {backCount} relues vs {allIssues.Count} attendues.");
        }
        Console.WriteLine("Extraction multi-serveurs terminée.");
        // NB (Phase 3) : CSV par serveur/projet + vues HTML d'export restent à brancher sur ce chemin.
    }

    /// <summary>Liste les projets accessibles au token de groupe d'un serveur (id numériques, en chaîne).</summary>
    private static async Task<List<string>> ListProjectsAsync(ServerConfig server, CancellationToken ct)
    {
        var handler = new HttpClientHandler();
        if (server.AllowSelfSignedCertificates) handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(server.RequestTimeoutSeconds <= 0 ? 60 : server.RequestTimeoutSeconds) };
        var ids = new List<string>();
        var url = server.BaseUrl.TrimEnd('/') + "/api/v4/projects?membership=true&simple=true&per_page=100&order_by=id&sort=asc";
        while (!string.IsNullOrEmpty(url))
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("PRIVATE-TOKEN", server.GroupToken);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) { Console.WriteLine($"  [warn] liste projets serveur '{server.Id}' : HTTP {(int)resp.StatusCode}"); break; }
            using (var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct)))
                foreach (var p in doc.RootElement.EnumerateArray())
                    if (p.TryGetProperty("id", out var id)) ids.Add(id.GetInt64().ToString());
            url = NextLink(resp);
        }
        return ids;
    }

    private static string? NextLink(HttpResponseMessage resp)
    {
        if (!resp.Headers.TryGetValues("Link", out var links)) return null;
        foreach (var link in links.SelectMany(l => l.Split(',')))
        {
            var parts = link.Split(';');
            if (parts.Length >= 2 && parts[1].Contains("rel=\"next\""))
                return parts[0].Trim().TrimStart('<').TrimEnd('>');
        }
        return null;
    }

    private static string SafeSegment(string s)
    {
        var arr = (s ?? "").Trim().Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_').ToArray();
        var r = new string(arr);
        return string.IsNullOrEmpty(r) ? "_" : r;
    }
}
