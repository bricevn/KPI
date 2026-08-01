using System.Text.Json;
using Kpi.Config;
using Kpi.Export;

namespace Kpi.Canny;

/// <summary>
/// Orchestration Canny côté KPI : extraction API → consolidation → stockage CHIFFRÉ sous
/// <c>output/canny/</c> (même mécanisme que les données GitLab, sous-clé SecureStore « canny »).
/// Le dashboard lit le dataset déchiffré via <see cref="TryReadDatasetJson"/>.
/// </summary>
public static class CannyService
{
    /// <summary>Sous-clé de chiffrement + segment de dossier (une seule connexion Canny pour l'instant).</summary>
    public const string PartitionId = "canny";

    private static string CannyDir(AppConfig cfg) => Path.Combine(cfg.Export.OutputDirectory, PartitionId);
    private static string DatasetPath(AppConfig cfg) => Path.Combine(CannyDir(cfg), "dataset.json");
    private static string CommentsPath(AppConfig cfg) => Path.Combine(CannyDir(cfg), "comments.json");

    private static readonly JsonSerializerOptions RawJson = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Extrait depuis l'API Canny, consolide, et écrit le dataset chiffré. Retourne les comptes.</summary>
    public static async Task<CannyExtractResult> ExtractAndStoreAsync(AppConfig cfg, CancellationToken ct)
    {
        var canny = cfg.ExternalConnections.Canny;
        if (!canny.Configured)
            throw new InvalidOperationException("Canny non configuré (ApiKey vide) — passez par le setup ou les options.");

        using var client = new CannyClient(canny);
        Console.WriteLine("== Canny : extraction API v1 ==");
        var boards = await client.ListSimpleAsync<CannyBoardRaw>("boards/list", "boards", ct);
        var categories = await client.ListSimpleAsync<CannyCategoryRaw>("categories/list", "categories", ct);
        var tags = await client.ListSimpleAsync<CannyTagRaw>("tags/list", "tags", ct);
        var posts = await client.ListPagedAsync<CannyPostRaw>("posts/list", "posts", ct);
        var comments = await client.ListPagedAsync<CannyCommentRaw>("comments/list", "comments", ct);
        var votesCount = await client.CountPagedAsync("votes/list", "votes", ct);
        var users = await client.ListPagedAsync<CannyUserRaw>("users/list", "users", ct);
        var statusChanges = await client.ListPagedAsync<CannyStatusChangeRaw>("status_changes/list", "status_changes", ct);

        var built = CannyDatasetBuilder.Build(canny, boards, categories, tags, posts, comments, votesCount, users, statusChanges);
        await StoreAsync(cfg, built, ct);

        Console.WriteLine($"  -> Canny chiffré dans {CannyDir(cfg)}/ : {built.Result.Posts} posts, {built.Result.Comments} commentaires, {built.Result.Roadmaps} roadmaps.");
        // Auto-contrôle : on relit ce qu'on vient d'écrire.
        var back = TryReadDatasetJson(cfg);
        Console.WriteLine(back != null ? "  -> vérif chiffrement Canny OK." : "  [warn] relecture du dataset Canny impossible.");
        return built.Result;
    }

    private static async Task StoreAsync(AppConfig cfg, CannyBuildOutput built, CancellationToken ct)
    {
        await SecureStore.WriteEncryptedAsync(PartitionId, DatasetPath(cfg), built.DatasetJson, ct);
        await SecureStore.WriteEncryptedAsync(PartitionId, CommentsPath(cfg), built.CommentsJson, ct);
    }

    /// <summary>Dataset analytique Canny déchiffré (JSON) ; <c>null</c> si absent/indéchiffrable.</summary>
    public static string? TryReadDatasetJson(AppConfig cfg) => SecureStore.TryReadDecrypted(PartitionId, DatasetPath(cfg));

    /// <summary>Commentaires détaillés Canny déchiffrés (JSON) ; <c>null</c> si absent.</summary>
    public static string? TryReadCommentsJson(AppConfig cfg) => SecureStore.TryReadDecrypted(PartitionId, CommentsPath(cfg));

    /// <summary>True si un dataset Canny a déjà été extrait et stocké.</summary>
    public static bool HasData(AppConfig cfg) => File.Exists(DatasetPath(cfg));

    /// <summary>Date (UTC) de la dernière extraction Canny = mtime du dataset chiffré ; null si jamais extrait.</summary>
    public static DateTime? LastExtractedUtc(AppConfig cfg)
    {
        var p = DatasetPath(cfg);
        return File.Exists(p) ? File.GetLastWriteTimeUtc(p) : null;
    }

    /// <summary>Date de dernière extraction formatée « yyyy-MM-dd HH:mm » (UTC), ou "" si jamais extrait.</summary>
    public static string LastExtractedString(AppConfig cfg) =>
        LastExtractedUtc(cfg)?.ToString("yyyy-MM-dd HH:mm") ?? "";

    // ------------------------------------------------------------------------
    // VÉRIFICATION HORS-LIGNE : reconstruit le dataset depuis les JSON BRUTS d'un
    // dossier (les data/*.json du projet Canny) SANS appel API ni stockage — pour
    // comparer le port C# à la sortie du pipeline Node.
    // ------------------------------------------------------------------------
    public static CannyBuildOutput BuildFromRawDir(CannyConfig cfg, string rawDir)
    {
        var boards = ReadArray<CannyBoardRaw>(Path.Combine(rawDir, "boards.json"), "boards");
        var categories = ReadArray<CannyCategoryRaw>(Path.Combine(rawDir, "categories.json"), "categories");
        var tags = ReadArray<CannyTagRaw>(Path.Combine(rawDir, "tags.json"), "tags");
        var posts = ReadArray<CannyPostRaw>(Path.Combine(rawDir, "posts.json"), "posts");
        var comments = ReadArray<CannyCommentRaw>(Path.Combine(rawDir, "comments.json"), "comments");
        var votes = ReadArray<JsonElement>(Path.Combine(rawDir, "votes.json"), "votes");
        var users = ReadArray<CannyUserRaw>(Path.Combine(rawDir, "users.json"), "users");
        var statusChanges = ReadArray<CannyStatusChangeRaw>(Path.Combine(rawDir, "status_changes.json"), "status_changes");
        return CannyDatasetBuilder.Build(cfg, boards, categories, tags, posts, comments, votes.Count, users, statusChanges);
    }

    // Lit un tableau depuis un JSON qui est soit un tableau brut, soit un objet { <key>: [...] }.
    private static List<T> ReadArray<T>(string path, string key)
    {
        if (!File.Exists(path)) return new List<T>();
        var txt = File.ReadAllText(path);
        if (txt.Length > 0 && txt[0] == '﻿') txt = txt.Substring(1); // BOM
        using var doc = JsonDocument.Parse(txt);
        JsonElement arr;
        if (doc.RootElement.ValueKind == JsonValueKind.Array) arr = doc.RootElement;
        else if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty(key, out var e) && e.ValueKind == JsonValueKind.Array) arr = e;
        else return new List<T>();
        var list = new List<T>();
        foreach (var el in arr.EnumerateArray())
        {
            var v = el.Deserialize<T>(RawJson);
            if (v != null) list.Add(v);
        }
        return list;
    }
}
