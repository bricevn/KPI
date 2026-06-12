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
using System.Linq;

namespace Kpi.Localization;

public static class Loc
{
    // Cultures supportées — SOURCE DE VÉRITÉ UNIQUE (partagée avec RequestLocalizationOptions, /set-lang,
    // DashboardView, et les sélecteurs de langue côté client via window.__LANGS__). Ajouter une langue :
    // 1) ajouter son code ici + son libellé natif dans Native (+ Rtl si droite-à-gauche) ;
    // 2) ajouter son bloc de traductions dans Strings (ci-dessous), Assets/app/i18n.js et Views/SetupView.cs.
    public static readonly string[] Supported = { "en", "fr", "es", "de", "it", "pt", "ru", "ar", "zh", "ja" };
    public const string Default = "en"; // anglais = langue de base (défaut + repli)

    /// <summary>Libellé NATIF de chaque langue (pour les sélecteurs).</summary>
    public static readonly Dictionary<string, string> Native = new()
    {
        ["fr"] = "Français", ["en"] = "English", ["es"] = "Español", ["de"] = "Deutsch",
        ["it"] = "Italiano", ["pt"] = "Português", ["ru"] = "Русский", ["ar"] = "العربية",
        ["zh"] = "中文", ["ja"] = "日本語",
    };

    /// <summary>Langues à écriture droite-à-gauche (RTL).</summary>
    public static readonly HashSet<string> Rtl = new() { "ar" };

    public static bool IsRtl(string? culture) => Rtl.Contains(Normalize(culture));

    /// <summary>Normalise vers un code supporté (sinon <see cref="Default"/>).</summary>
    public static string Normalize(string? culture)
        => culture != null && Array.IndexOf(Supported, culture) >= 0 ? culture : Default;

    /// <summary>Liste [code, libellé natif] dans l'ordre de <see cref="Supported"/> (pour les sélecteurs).</summary>
    public static List<string[]> List()
        => Supported.Select(c => new[] { c, Native.TryGetValue(c, out var n) ? n : c }).ToList();

    // culture courte de la requête, via CultureInfo.CurrentUICulture (posée par le middleware).
    public static string Current() => Normalize(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);

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
        ["es"] = new()
        {
            ["login.title"] = "Inicia sesión en KPI",
            ["login.subtitle"] = "Inicia sesión con tu cuenta de GitLab.",
            ["login.withGitlab"] = "Inicia sesión con GitLab",
            ["login.orToken"] = "o con un token de acceso",
            ["login.instance"] = "URL de GitLab",
            ["login.token"] = "Token de acceso",
            ["login.signin"] = "Iniciar sesión",
            ["login.remember"] = "Recuérdame",
            ["login.encrypted"] = "Conexión cifrada",
            ["login.tag1"] = "Tus KPIs de lanzamiento,",
            ["login.tag2"] = "en tiempo real.",
            ["login.desc"] = "Realiza un seguimiento del progreso, peso validado, velocidad por colaborador y anomalías. Todo se sincroniza desde GitLab en cada actualización.",
            ["login.welcome"] = "Bienvenido",
            ["login.welcomeSub"] = "Ingresa tu instancia de GitLab y luego inicia sesión.",
            ["login.useToken"] = "Usa un token de acceso →",
            ["login.tokenPersonal"] = "token de acceso personal",
            ["login.tokenNote"] = "El token se usa solo para verificar tu identidad. Nunca se almacena.",
            ["login.encryptedTo"] = "Conexión cifrada a ",
            ["login.errNoInstance"] = "Ingresa la URL de tu instancia de GitLab.",
            ["login.errNoToken"] = "Ingresa tu token de acceso personal.",
            ["login.verifying"] = "Verificando…",
            ["login.cantConnect"] = "No se puede conectar.",
            ["login.unreachable"] = "Servidor no disponible. Intenta de nuevo.",
            ["setup.title"] = "Configuración",
            ["setup.step"] = "Paso {0} de 5",
            ["setup.continue"] = "Continuar",
            ["setup.back"] = "Atrás",
            ["setup.launch"] = "Lanzar panel",
            ["setup.cancel"] = "Cancelar",
            ["common.lang"] = "Idioma",
        },
        ["de"] = new()
        {
            ["login.title"] = "Anmeldung bei KPI",
            ["login.subtitle"] = "Melden Sie sich mit Ihrem GitLab-Konto an.",
            ["login.withGitlab"] = "Mit GitLab anmelden",
            ["login.orToken"] = "oder mit einem Zugriffstoken",
            ["login.instance"] = "GitLab-URL",
            ["login.token"] = "Zugriffstoken",
            ["login.signin"] = "Anmelden",
            ["login.remember"] = "Meine Anmeldedaten merken",
            ["login.encrypted"] = "Verschlüsselte Verbindung",
            ["login.tag1"] = "Ihre Release-KPIs,",
            ["login.tag2"] = "in Echtzeit.",
            ["login.desc"] = "Verfolgen Sie den Fortschritt, das validierte Gewicht, die Geschwindigkeit pro Mitwirkender und Anomalien. Alles wird bei jeder Aktualisierung von GitLab synchronisiert.",
            ["login.welcome"] = "Willkommen",
            ["login.welcomeSub"] = "Geben Sie Ihre GitLab-Instanz ein und melden Sie sich an.",
            ["login.useToken"] = "Zugriffstoken verwenden →",
            ["login.tokenPersonal"] = "persönliches Zugriffstoken",
            ["login.tokenNote"] = "Das Token wird nur zur Überprüfung Ihrer Identität verwendet. Es wird niemals gespeichert.",
            ["login.encryptedTo"] = "Verschlüsselte Verbindung zu ",
            ["login.errNoInstance"] = "Geben Sie Ihre GitLab-Instanz-URL ein.",
            ["login.errNoToken"] = "Geben Sie Ihr persönliches Zugriffstoken ein.",
            ["login.verifying"] = "Überprüfung…",
            ["login.cantConnect"] = "Verbindung nicht möglich.",
            ["login.unreachable"] = "Server nicht erreichbar. Versuchen Sie es erneut.",
            ["setup.title"] = "Einrichtung",
            ["setup.step"] = "Schritt {0} von 5",
            ["setup.continue"] = "Fortfahren",
            ["setup.back"] = "Zurück",
            ["setup.launch"] = "Dashboard starten",
            ["setup.cancel"] = "Abbrechen",
            ["common.lang"] = "Sprache",
        },
        ["it"] = new()
        {
            ["login.title"] = "Accedi a KPI",
            ["login.subtitle"] = "Accedi con il tuo account GitLab.",
            ["login.withGitlab"] = "Accedi con GitLab",
            ["login.orToken"] = "oppure con un token di accesso",
            ["login.instance"] = "URL GitLab",
            ["login.token"] = "Token di accesso",
            ["login.signin"] = "Accedi",
            ["login.remember"] = "Ricordami",
            ["login.encrypted"] = "Connessione crittografata",
            ["login.tag1"] = "I tuoi KPI di release,",
            ["login.tag2"] = "in tempo reale.",
            ["login.desc"] = "Traccia avanzamento, peso convalidato, velocità per contributore e anomalie. Tutto si sincronizza da GitLab ad ogni aggiornamento.",
            ["login.welcome"] = "Benvenuto",
            ["login.welcomeSub"] = "Inserisci la tua istanza GitLab, quindi accedi.",
            ["login.useToken"] = "Usa un token di accesso →",
            ["login.tokenPersonal"] = "token di accesso personale",
            ["login.tokenNote"] = "Il token viene utilizzato solo per verificare la tua identità. Non è mai archiviato.",
            ["login.encryptedTo"] = "Connessione crittografata a ",
            ["login.errNoInstance"] = "Inserisci l'URL della tua istanza GitLab.",
            ["login.errNoToken"] = "Inserisci il tuo token di accesso personale.",
            ["login.verifying"] = "Verifica in corso…",
            ["login.cantConnect"] = "Impossibile connettersi.",
            ["login.unreachable"] = "Server non raggiungibile. Riprova.",
            ["setup.title"] = "Configurazione",
            ["setup.step"] = "Passaggio {0} su 5",
            ["setup.continue"] = "Continua",
            ["setup.back"] = "Indietro",
            ["setup.launch"] = "Avvia dashboard",
            ["setup.cancel"] = "Annulla",
            ["common.lang"] = "Lingua",
        },
        ["pt"] = new()
        {
            ["login.title"] = "Iniciar sessão no KPI",
            ["login.subtitle"] = "Inicie sessão com a sua conta GitLab.",
            ["login.withGitlab"] = "Iniciar sessão com GitLab",
            ["login.orToken"] = "ou com um token de acesso",
            ["login.instance"] = "URL GitLab",
            ["login.token"] = "Token de acesso",
            ["login.signin"] = "Iniciar sessão",
            ["login.remember"] = "Lembrar-me",
            ["login.encrypted"] = "Ligação encriptada",
            ["login.tag1"] = "Os seus KPIs de release,",
            ["login.tag2"] = "em tempo real.",
            ["login.desc"] = "Acompanhe o progresso, peso validado, velocidade por colaborador e anomalias. Tudo é sincronizado a partir de GitLab em cada atualização.",
            ["login.welcome"] = "Bem-vindo",
            ["login.welcomeSub"] = "Introduza a sua instância GitLab, depois inicie sessão.",
            ["login.useToken"] = "Usar um token de acesso →",
            ["login.tokenPersonal"] = "token de acesso pessoal",
            ["login.tokenNote"] = "O token é apenas usado para verificar a sua identidade. Nunca é armazenado.",
            ["login.encryptedTo"] = "Ligação encriptada para ",
            ["login.errNoInstance"] = "Introduza a URL da sua instância GitLab.",
            ["login.errNoToken"] = "Introduza o seu token de acesso pessoal.",
            ["login.verifying"] = "A verificar…",
            ["login.cantConnect"] = "Não foi possível ligar.",
            ["login.unreachable"] = "Servidor inacessível. Tente novamente.",
            ["setup.title"] = "Implementação",
            ["setup.step"] = "Passo {0} de 5",
            ["setup.continue"] = "Continuar",
            ["setup.back"] = "Atrás",
            ["setup.launch"] = "Iniciar dashboard",
            ["setup.cancel"] = "Cancelar",
            ["common.lang"] = "Idioma",
        },
        ["ru"] = new()
        {
            ["login.title"] = "Вход в KPI",
            ["login.subtitle"] = "Войдите с вашей учетной записью GitLab.",
            ["login.withGitlab"] = "Войти через GitLab",
            ["login.orToken"] = "или с токеном доступа",
            ["login.instance"] = "URL GitLab",
            ["login.token"] = "Токен доступа",
            ["login.signin"] = "Войти",
            ["login.remember"] = "Запомнить меня",
            ["login.encrypted"] = "Зашифрованное соединение",
            ["login.tag1"] = "Ваши KPI выпуска,",
            ["login.tag2"] = "в реальном времени.",
            ["login.desc"] = "Отслеживайте прогресс, валидный вес, скорость по участникам и аномалии. Все синхронизируется из GitLab при каждом обновлении.",
            ["login.welcome"] = "Добро пожаловать",
            ["login.welcomeSub"] = "Введите вашу инстанцию GitLab, затем войдите.",
            ["login.useToken"] = "Использовать токен доступа →",
            ["login.tokenPersonal"] = "личный токен доступа",
            ["login.tokenNote"] = "Токен используется только для проверки вашей личности. Он никогда не сохраняется.",
            ["login.encryptedTo"] = "Зашифрованное соединение с ",
            ["login.errNoInstance"] = "Введите URL вашей инстанции GitLab.",
            ["login.errNoToken"] = "Введите ваш личный токен доступа.",
            ["login.verifying"] = "Проверка…",
            ["login.cantConnect"] = "Не удается подключиться.",
            ["login.unreachable"] = "Сервер недоступен. Повторите попытку.",
            ["setup.title"] = "Конфигурация",
            ["setup.step"] = "Шаг {0} из 5",
            ["setup.continue"] = "Продолжить",
            ["setup.back"] = "Назад",
            ["setup.launch"] = "Запустить панель управления",
            ["setup.cancel"] = "Отмена",
            ["common.lang"] = "Язык",
        },
        ["ar"] = new()
        {
            ["login.title"] = "تسجيل الدخول إلى KPI",
            ["login.subtitle"] = "سجل الدخول باستخدام حساب GitLab الخاص بك.",
            ["login.withGitlab"] = "تسجيل الدخول باستخدام GitLab",
            ["login.orToken"] = "أو باستخدام رمز الوصول",
            ["login.instance"] = "عنوان URL الخاص بـ GitLab",
            ["login.token"] = "رمز الوصول",
            ["login.signin"] = "تسجيل الدخول",
            ["login.remember"] = "تذكري",
            ["login.encrypted"] = "اتصال مشفر",
            ["login.tag1"] = "مؤشرات الأداء الرئيسية للإصدار الخاص بك،",
            ["login.tag2"] = "في الوقت الفعلي.",
            ["login.desc"] = "تتبع التقدم والوزن المُتحقق منه والسرعة لكل مساهم والحالات الشاذة. يتم مزامنة كل شيء من GitLab عند كل تحديث.",
            ["login.welcome"] = "مرحباً",
            ["login.welcomeSub"] = "أدخل مثيل GitLab الخاص بك، ثم سجل الدخول.",
            ["login.useToken"] = "استخدام رمز وصول →",
            ["login.tokenPersonal"] = "رمز وصول شخصي",
            ["login.tokenNote"] = "يُستخدم الرمز فقط للتحقق من هويتك. لا يتم حفظه أبداً.",
            ["login.encryptedTo"] = "اتصال مشفر إلى",
            ["login.errNoInstance"] = "أدخل عنوان URL لمثيل GitLab الخاص بك.",
            ["login.errNoToken"] = "أدخل رمز الوصول الشخصي الخاص بك.",
            ["login.verifying"] = "التحقق جارٍ…",
            ["login.cantConnect"] = "غير قادر على الاتصال.",
            ["login.unreachable"] = "الخادم غير متاح. حاول مرة أخرى.",
            ["setup.title"] = "الإعداد",
            ["setup.step"] = "الخطوة {0} من 5",
            ["setup.continue"] = "متابعة",
            ["setup.back"] = "رجوع",
            ["setup.launch"] = "تشغيل لوحة التحكم",
            ["setup.cancel"] = "إلغاء",
            ["common.lang"] = "اللغة",
        },
        ["zh"] = new()
        {
            ["login.title"] = "登录KPI",
            ["login.subtitle"] = "使用您的GitLab账户登录。",
            ["login.withGitlab"] = "使用GitLab登录",
            ["login.orToken"] = "或使用访问令牌",
            ["login.instance"] = "GitLab URL",
            ["login.token"] = "访问令牌",
            ["login.signin"] = "登录",
            ["login.remember"] = "记住我",
            ["login.encrypted"] = "加密连接",
            ["login.tag1"] = "您的发布KPI，",
            ["login.tag2"] = "实时。",
            ["login.desc"] = "跟踪进度、已验证权重、每个贡献者的速度和异常。一切都在每次刷新时从GitLab同步。",
            ["login.welcome"] = "欢迎",
            ["login.welcomeSub"] = "输入您的GitLab实例，然后登录。",
            ["login.useToken"] = "使用访问令牌 →",
            ["login.tokenPersonal"] = "个人访问令牌",
            ["login.tokenNote"] = "令牌仅用于验证您的身份。永远不会存储。",
            ["login.encryptedTo"] = "加密连接到 ",
            ["login.errNoInstance"] = "输入您的GitLab实例URL。",
            ["login.errNoToken"] = "输入您的个人访问令牌。",
            ["login.verifying"] = "验证中…",
            ["login.cantConnect"] = "无法连接。",
            ["login.unreachable"] = "服务器无法访问。请重试。",
            ["setup.title"] = "设置",
            ["setup.step"] = "第 {0} 步（共5步）",
            ["setup.continue"] = "继续",
            ["setup.back"] = "返回",
            ["setup.launch"] = "启动仪表板",
            ["setup.cancel"] = "取消",
            ["common.lang"] = "语言",
        },
        ["ja"] = new()
        {
            ["login.title"] = "KPIにサインイン",
            ["login.subtitle"] = "GitLabアカウントでサインイン。",
            ["login.withGitlab"] = "GitLabでサインイン",
            ["login.orToken"] = "またはアクセストークンで",
            ["login.instance"] = "GitLab URL",
            ["login.token"] = "アクセストークン",
            ["login.signin"] = "サインイン",
            ["login.remember"] = "認証情報を記憶する",
            ["login.encrypted"] = "暗号化接続",
            ["login.tag1"] = "リリースKPI、",
            ["login.tag2"] = "リアルタイム。",
            ["login.desc"] = "進捗、検証済み重み、貢献者別速度、異常を追跡。すべてはリフレッシュのたびにGitLabから同期。",
            ["login.welcome"] = "ようこそ",
            ["login.welcomeSub"] = "GitLabインスタンスを入力してからサインイン。",
            ["login.useToken"] = "アクセストークンを使用 →",
            ["login.tokenPersonal"] = "個人アクセストークン",
            ["login.tokenNote"] = "トークンは身元確認にのみ使用。保存されることはありません。",
            ["login.encryptedTo"] = "暗号化接続先 ",
            ["login.errNoInstance"] = "GitLabインスタンスURLを入力。",
            ["login.errNoToken"] = "個人アクセストークンを入力。",
            ["login.verifying"] = "確認中…",
            ["login.cantConnect"] = "接続できません。",
            ["login.unreachable"] = "サーバーに接続できません。再試行してください。",
            ["setup.title"] = "セットアップ",
            ["setup.step"] = "ステップ {0} / 5",
            ["setup.continue"] = "続行",
            ["setup.back"] = "戻る",
            ["setup.launch"] = "ダッシュボードを起動",
            ["setup.cancel"] = "キャンセル",
            ["common.lang"] = "言語",
        },
    };
}
