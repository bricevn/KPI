using System.Text;
using Microsoft.AspNetCore.DataProtection;

namespace Kpi.Export;

/// <summary>
/// Chiffrement AU REPOS des données extraites (issues.json &amp; co.), pour que KPI ne soit pas un
/// point de fuite : les fichiers sur disque sont illisibles sans les clés de l'application.
/// <list type="bullet">
///   <item>Clés gérées par ASP.NET Data Protection — mêmes clés que le serveur web (dossier
///   <c>dp-keys</c>, application « Kpi ») → utilisable par le CLI d'extraction ET par le serveur.</item>
///   <item><b>Sous-clé par serveur</b> (purpose dérivé de l'Id) ⇒ cloisonnement CRYPTOGRAPHIQUE :
///   les données d'un serveur GitLab ne peuvent pas être déchiffrées avec la clé d'un autre.</item>
/// </list>
/// ⚠ En multi-instance, monter <c>dp-keys</c> sur un volume PARTAGÉ (sinon données illisibles ailleurs).
/// </summary>
public static class SecureStore
{
    private static readonly IDataProtectionProvider _provider = DataProtectionProvider.Create(
        new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "dp-keys")),
        b => b.SetApplicationName("Kpi"));

    private static IDataProtector Protector(string serverId) =>
        _provider.CreateProtector("Kpi.AtRest.v1", string.IsNullOrWhiteSpace(serverId) ? "default" : serverId);

    /// <summary>Écrit du texte CHIFFRÉ de façon atomique (tmp + rename), avec la sous-clé du serveur.</summary>
    public static async Task WriteEncryptedAsync(string serverId, string path, string plaintext, CancellationToken ct = default)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var cipher = Protector(serverId).Protect(Encoding.UTF8.GetBytes(plaintext));
        var tmp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(tmp, cipher, ct);
            File.Move(tmp, path, overwrite: true);
        }
        catch { try { if (File.Exists(tmp)) File.Delete(tmp); } catch { /* best effort */ } throw; }
    }

    /// <summary>Lit et déchiffre ; <c>null</c> si absent ou indéchiffrable (mauvaise sous-clé / corrompu).</summary>
    public static string? TryReadDecrypted(string serverId, string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            var cipher = File.ReadAllBytes(path);
            return Encoding.UTF8.GetString(Protector(serverId).Unprotect(cipher));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[SecureStore] Déchiffrement impossible de {Path.GetFileName(path)} : {ex.Message}");
            return null;
        }
    }
}
