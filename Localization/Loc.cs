// Loc.cs — localisation CÔTÉ SERVEUR pour les pages rendues en C# (LoginView, SetupView).
// Drop dans GitLabExporter/Localization/  (namespace Kpi.Localization).
//
// Pourquoi un dictionnaire et pas directement des .resx ?
//   LoginView/SetupView sont des raw-strings HTML géantes : les migrer vers IStringLocalizer
//   + .resx d'un coup est risqué. Loc.cs offre la MÊME ergonomie (Loc.T(culture,"key"))
//   et se remplace plus tard par IStringLocalizer sans changer les appels (signature identique).
//
// Idiomatique (cible) : AddLocalization + IStringLocalizer<SharedRes> + Resources/SharedRes.{fr,en}.resx,
//   injecté dans les vues. Voir README §6. Loc.cs est l'étape pragmatique d'amorçage.
using System.Globalization;

namespace Kpi.Localization;

public static class Loc
{
    // Cultures supportées (source de vérité partagée avec Program.cs / RequestLocalizationOptions).
    public static readonly string[] Supported = { "fr", "en" };
    public const string Default = "fr";

    // culture courte ("fr"/"en") de la requête, via CultureInfo.CurrentUICulture (posée par le middleware).
    public static string Current()
    {
        var c = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return Array.IndexOf(Supported, c) >= 0 ? c : Default;
    }

    public static string T(string key) => T(Current(), key);

    public static string T(string culture, string key)
    {
        var c = Array.IndexOf(Supported, culture) >= 0 ? culture : Default;
        if (Strings.TryGetValue(c, out var d) && d.TryGetValue(key, out var v)) return v;
        if (Strings[Default].TryGetValue(key, out var fb)) return fb; // repli FR
        return key;                                                    // dernier repli : la clé
    }

    // Dictionnaires des PAGES SERVEUR (login + setup). Étend au fur et à mesure que tu externalises
    // les chaînes de LoginView/SetupView. Garde les mêmes clés des deux côtés.
    private static readonly Dictionary<string, Dictionary<string, string>> Strings = new()
    {
        ["fr"] = new()
        {
            // --- login ---
            ["login.title"] = "Connexion à KPI",
            ["login.subtitle"] = "Connectez-vous avec votre compte GitLab.",
            ["login.withGitlab"] = "Se connecter avec GitLab",
            ["login.orToken"] = "ou par token d'accès",
            ["login.instance"] = "Adresse GitLab",
            ["login.token"] = "Token d'accès",
            ["login.signin"] = "Se connecter",
            ["login.remember"] = "Se souvenir de moi",
            ["login.encrypted"] = "Connexion chiffrée",
            ["login.tag1"] = "Vos KPI de release,",
            ["login.tag2"] = "en temps réel.",
            ["login.desc"] = "Suivez l'avancement, le poids validé, la vélocité par collaborateur et les anomalies. Tout est synchronisé depuis GitLab à chaque rafraîchissement.",
            ["login.welcome"] = "Bienvenue",
            ["login.welcomeSub"] = "Indiquez votre instance GitLab, puis connectez-vous.",
            ["login.useToken"] = "Utiliser un token d'accès →",
            ["login.tokenPersonal"] = "token d'accès personnel",
            ["login.tokenNote"] = "Le token sert uniquement à vérifier votre identité. Il n'est pas conservé.",
            ["login.encryptedTo"] = "Connexion chiffrée à ",
            ["login.errNoInstance"] = "Renseignez l'adresse de votre instance GitLab.",
            ["login.errNoToken"] = "Saisissez votre token d'accès personnel.",
            ["login.verifying"] = "Vérification…",
            ["login.cantConnect"] = "Connexion impossible.",
            ["login.unreachable"] = "Serveur injoignable. Réessayez.",
            // --- setup (extraits ; complète au besoin) ---
            ["setup.title"] = "Mise en service",
            ["setup.step"] = "Étape {0} sur 5",
            ["setup.continue"] = "Continuer",
            ["setup.back"] = "Retour",
            ["setup.launch"] = "Lancer le dashboard",
            ["setup.cancel"] = "Annuler",
            // --- commun ---
            ["common.lang"] = "Langue",
        },
        ["en"] = new()
        {
            ["login.title"] = "Sign in to KPI",
            ["login.subtitle"] = "Sign in with your GitLab account.",
            ["login.withGitlab"] = "Sign in with GitLab",
            ["login.orToken"] = "or with an access token",
            ["login.instance"] = "GitLab URL",
            ["login.token"] = "Access token",
            ["login.signin"] = "Sign in",
            ["login.remember"] = "Remember me",
            ["login.encrypted"] = "Encrypted connection",
            ["login.tag1"] = "Your release KPIs,",
            ["login.tag2"] = "in real time.",
            ["login.desc"] = "Track progress, validated weight, per-contributor velocity and anomalies. Everything syncs from GitLab on each refresh.",
            ["login.welcome"] = "Welcome",
            ["login.welcomeSub"] = "Enter your GitLab instance, then sign in.",
            ["login.useToken"] = "Use an access token →",
            ["login.tokenPersonal"] = "personal access token",
            ["login.tokenNote"] = "The token is only used to verify your identity. It is never stored.",
            ["login.encryptedTo"] = "Encrypted connection to ",
            ["login.errNoInstance"] = "Enter your GitLab instance URL.",
            ["login.errNoToken"] = "Enter your personal access token.",
            ["login.verifying"] = "Verifying…",
            ["login.cantConnect"] = "Unable to connect.",
            ["login.unreachable"] = "Server unreachable. Try again.",
            ["setup.title"] = "Setup",
            ["setup.step"] = "Step {0} of 5",
            ["setup.continue"] = "Continue",
            ["setup.back"] = "Back",
            ["setup.launch"] = "Launch dashboard",
            ["setup.cancel"] = "Cancel",
            ["common.lang"] = "Language",
        },
    };
}
