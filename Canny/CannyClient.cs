using System.Text.Json;
using Kpi.Config;

namespace Kpi.Canny;

/// <summary>
/// Client de l'API Canny v1 (port de <c>scripts/extract-canny.ps1</c>). Chaque ressource est un POST
/// <c>&lt;base&gt;/&lt;resource&gt;/list</c> en <c>application/x-www-form-urlencoded</c> avec <c>apiKey</c>
/// (jamais loggée). Ressources simples (enveloppe <c>{ boards: [...] }</c>) ou paginées
/// (<c>{ posts: [...], hasMore }</c>, boucle limit=100 + skip).
/// </summary>
public sealed class CannyClient : IDisposable
{
    private const string Base = "https://canny.io/api/v1/";
    private const int PageSize = 100;

    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    public CannyClient(CannyConfig cfg)
    {
        if (string.IsNullOrWhiteSpace(cfg.ApiKey))
            throw new InvalidOperationException("Canny : ApiKey vide — configurez la connexion (setup/options).");
        _apiKey = cfg.ApiKey;
        _http = new HttpClient
        {
            BaseAddress = new Uri(Base),
            Timeout = TimeSpan.FromSeconds(cfg.RequestTimeoutSeconds <= 0 ? 60 : cfg.RequestTimeoutSeconds),
        };
    }

    private async Task<JsonDocument> PostAsync(string resource, IReadOnlyDictionary<string, string>? extra, CancellationToken ct)
    {
        var form = new Dictionary<string, string> { ["apiKey"] = _apiKey };
        if (extra != null) foreach (var kv in extra) form[kv.Key] = kv.Value;
        using var content = new FormUrlEncodedContent(form);
        using var resp = await _http.PostAsync(resource, content, ct);
        resp.EnsureSuccessStatusCode();
        var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
        return JsonDocument.Parse(bytes);
    }

    /// <summary>Ressource NON paginée : renvoie le tableau sous la clé <paramref name="arrayKey"/> désérialisé en T.</summary>
    public async Task<List<T>> ListSimpleAsync<T>(string resource, string arrayKey, CancellationToken ct)
    {
        using var doc = await PostAsync(resource, null, ct);
        return DeserializeArray<T>(doc.RootElement, arrayKey);
    }

    /// <summary>Ressource paginée : boucle limit/skip jusqu'à <c>hasMore=false</c> (ou lot vide).</summary>
    public async Task<List<T>> ListPagedAsync<T>(string resource, string arrayKey, CancellationToken ct)
    {
        var all = new List<T>();
        var skip = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            using var doc = await PostAsync(resource,
                new Dictionary<string, string> { ["limit"] = PageSize.ToString(), ["skip"] = skip.ToString() }, ct);
            var batch = DeserializeArray<T>(doc.RootElement, arrayKey);
            all.AddRange(batch);
            var hasMore = doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("hasMore", out var hm) && hm.ValueKind == JsonValueKind.True;
            if (!hasMore || batch.Count == 0) break;
            skip += PageSize;
        }
        return all;
    }

    /// <summary>Compte les éléments d'une ressource paginée sans les matérialiser (ex. votes : seul le total sert).</summary>
    public async Task<int> CountPagedAsync(string resource, string arrayKey, CancellationToken ct)
    {
        var total = 0;
        var skip = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            using var doc = await PostAsync(resource,
                new Dictionary<string, string> { ["limit"] = PageSize.ToString(), ["skip"] = skip.ToString() }, ct);
            var count = FindArray(doc.RootElement, arrayKey, out var arr) ? arr.GetArrayLength() : 0;
            total += count;
            var hasMore = doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("hasMore", out var hm) && hm.ValueKind == JsonValueKind.True;
            if (!hasMore || count == 0) break;
            skip += PageSize;
        }
        return total;
    }

    private List<T> DeserializeArray<T>(JsonElement root, string key)
    {
        if (!FindArray(root, key, out var arr)) return new List<T>();
        var list = new List<T>();
        foreach (var el in arr.EnumerateArray())
        {
            var v = el.Deserialize<T>(_json);
            if (v != null) list.Add(v);
        }
        return list;
    }

    // Trouve le tableau : d'abord la clé attendue, sinon la 1re propriété tableau (comme le script PS), sinon la racine si elle est déjà un tableau.
    private static bool FindArray(JsonElement root, string key, out JsonElement arr)
    {
        if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty(key, out var e) && e.ValueKind == JsonValueKind.Array) { arr = e; return true; }
            foreach (var p in root.EnumerateObject())
                if (p.Value.ValueKind == JsonValueKind.Array) { arr = p.Value; return true; }
        }
        else if (root.ValueKind == JsonValueKind.Array) { arr = root; return true; }
        arr = default;
        return false;
    }

    public void Dispose() => _http.Dispose();
}
