using System.Text;
using Kpi.Config;
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
        b =>
        {
            b.SetApplicationName("Kpi");
            // Windows : la clé maîtresse dp-keys est elle-même chiffrée via DPAPI (compte courant).
            // Sans ça, le XML de clé en clair à côté du binaire rend le chiffrement contournable
            // par simple lecture du disque. NB : lie les clés à l'UTILISATEUR WINDOWS qui exécute
            // l'app (serveur ET CLI doivent tourner sous le même compte).
            if (OperatingSystem.IsWindows()) b.ProtectKeysWithDpapi();
        });

    private static IDataProtector Protector(string serverId) =>
        _provider.CreateProtector("Kpi.AtRest.v1", string.IsNullOrWhiteSpace(serverId) ? "default" : serverId);

    // ---- Secrets de configuration (appsettings.json : GroupToken, ClientSecret) ----
    // Format au repos : « enc:v1:<base64> ». Transparent : une valeur non préfixée est traitée
    // comme du clair (migrée au boot par le serveur), une valeur préfixée est déchiffrée au chargement.
    private const string SecretPrefix = "enc:v1:";
    private static IDataProtector ConfigProtector() => _provider.CreateProtector("Kpi.Config.v1");

    /// <summary>Chiffre un secret de config pour l'écriture au repos. Idempotent (déjà chiffré ⇒ inchangé).</summary>
    public static string ProtectSecret(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext) || plaintext.StartsWith(SecretPrefix, StringComparison.Ordinal)) return plaintext;
        return SecretPrefix + Convert.ToBase64String(ConfigProtector().Protect(Encoding.UTF8.GetBytes(plaintext)));
    }

    /// <summary>Déchiffre un secret de config lu depuis appsettings.json. Une valeur en clair passe telle
    /// quelle (rétro-compat) ; une valeur chiffrée indéchiffrable (clés dp-keys perdues) ⇒ "" + erreur loggée.</summary>
    public static string UnprotectSecret(string stored)
    {
        if (string.IsNullOrEmpty(stored) || !stored.StartsWith(SecretPrefix, StringComparison.Ordinal)) return stored;
        try
        {
            var cipher = Convert.FromBase64String(stored.Substring(SecretPrefix.Length));
            return Encoding.UTF8.GetString(ConfigProtector().Unprotect(cipher));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[SecureStore] Secret de configuration indéchiffrable (dp-keys changées ?) : " + ex.Message);
            return "";
        }
    }

    /// <summary>Déchiffre EN PLACE les secrets d'une config chargée (Servers[].GroupToken, Auth.ClientSecret).
    /// À appeler juste après le bind + RepairColonKeyedMaps, côté serveur ET côté CLI.</summary>
    public static void UnprotectConfig(AppConfig cfg)
    {
        foreach (var s in cfg.Servers ?? new()) s.GroupToken = UnprotectSecret(s.GroupToken);
        cfg.Auth.ClientSecret = UnprotectSecret(cfg.Auth.ClientSecret);
        // Connexions externes : la clé API Canny est un secret au même titre que les tokens GitLab.
        if (cfg.ExternalConnections?.Canny is { } canny) canny.ApiKey = UnprotectSecret(canny.ApiKey);
    }

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
