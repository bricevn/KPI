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
            ["login.withGitlab"] = "Se connecter avec GitLab",
            ["login.encrypted"] = "Connexion chiffrée",
            ["login.tag1"] = "Vos KPI de release,",
            ["login.tag2"] = "en temps réel.",
            ["login.desc"] = "Suivez l'avancement, le poids validé, la vélocité par collaborateur et les anomalies. Tout est synchronisé depuis GitLab à chaque rafraîchissement.",
            ["login.welcome"] = "Bienvenue",
            ["login.firstRunBadge"] = "Première connexion",
            ["login.firstRunTitle"] = "Bienvenue sur KPI",
            ["login.firstRunSub"] = "Cet espace n'est pas encore configuré. Lancez l'assistant de mise en service pour le préparer — environ 5 minutes.",
            ["login.firstRunStep1"] = "Connexion à votre instance GitLab",
            ["login.firstRunStep2"] = "Choix des projets à suivre",
            ["login.firstRunStep3"] = "Phases de production &amp; équipes",
            ["login.setupCta"] = "Lancer la configuration",
            ["login.setupOpening"] = "Ouverture de l'assistant…",
            ["login.firstRunAdmins"] = "Réservé aux administrateurs",
            ["login.frTag1"] = "Configurons votre",
            ["login.frTag2"] = "espace KPI.",
            ["login.frDesc"] = "Quelques étapes pour connecter GitLab, choisir les projets à suivre et définir vos phases de production. Vous pourrez tout ajuster ensuite.",
            ["login.ssoSub"] = "Connectez-vous avec votre compte GitLab.",
            ["login.oauthRequired"] = "La connexion GitLab (SSO) n'est pas configurée sur ce serveur — renseignez Auth.ClientId / ClientSecret puis redémarrez.",
            // --- setup (extraits ; complète au besoin) ---
            // --- commun ---
        },
        ["en"] = new()
        {
            ["login.withGitlab"] = "Sign in with GitLab",
            ["login.encrypted"] = "Encrypted connection",
            ["login.tag1"] = "Your release KPIs,",
            ["login.tag2"] = "in real time.",
            ["login.desc"] = "Track progress, validated weight, per-contributor velocity and anomalies. Everything syncs from GitLab on each refresh.",
            ["login.welcome"] = "Welcome",
            ["login.firstRunBadge"] = "First sign-in",
            ["login.firstRunTitle"] = "Welcome to KPI",
            ["login.firstRunSub"] = "This workspace isn't configured yet. Launch the setup wizard to get it ready — about 5 minutes.",
            ["login.firstRunStep1"] = "Connect to your GitLab instance",
            ["login.firstRunStep2"] = "Choose the projects to track",
            ["login.firstRunStep3"] = "Production phases &amp; teams",
            ["login.setupCta"] = "Start setup",
            ["login.setupOpening"] = "Opening the wizard…",
            ["login.firstRunAdmins"] = "Administrators only",
            ["login.frTag1"] = "Let's set up your",
            ["login.frTag2"] = "KPI workspace.",
            ["login.frDesc"] = "A few steps to connect GitLab, choose the projects to track and define your production phases. You can adjust everything later.",
            ["login.ssoSub"] = "Sign in with your GitLab account.",
            ["login.oauthRequired"] = "GitLab (SSO) sign-in isn't configured on this server — set Auth.ClientId / ClientSecret, then restart.",
        },
        ["es"] = new()
        {
            ["login.withGitlab"] = "Inicia sesión con GitLab",
            ["login.encrypted"] = "Conexión cifrada",
            ["login.tag1"] = "Tus KPIs de lanzamiento,",
            ["login.tag2"] = "en tiempo real.",
            ["login.desc"] = "Realiza un seguimiento del progreso, peso validado, velocidad por colaborador y anomalías. Todo se sincroniza desde GitLab en cada actualización.",
            ["login.welcome"] = "Bienvenido",
            ["login.firstRunBadge"] = "Primera conexión",
            ["login.firstRunTitle"] = "Te damos la bienvenida a KPI",
            ["login.firstRunSub"] = "Este espacio aún no está configurado. Inicia el asistente de puesta en marcha para prepararlo: unos 5 minutos.",
            ["login.firstRunStep1"] = "Conexión a tu instancia de GitLab",
            ["login.firstRunStep2"] = "Elección de los proyectos a seguir",
            ["login.firstRunStep3"] = "Fases de producción y equipos",
            ["login.setupCta"] = "Comenzar la configuración",
            ["login.setupOpening"] = "Abriendo el asistente…",
            ["login.firstRunAdmins"] = "Solo para administradores",
            ["login.frTag1"] = "Configuremos tu",
            ["login.frTag2"] = "espacio KPI.",
            ["login.frDesc"] = "Unos pasos para conectar GitLab, elegir los proyectos a seguir y definir tus fases de producción. Podrás ajustarlo todo después.",
            ["login.ssoSub"] = "Inicia sesión con tu cuenta de GitLab.",
            ["login.oauthRequired"] = "El inicio de sesión con GitLab (SSO) no está configurado en este servidor: define Auth.ClientId / ClientSecret y reinicia.",
        },
        ["de"] = new()
        {
            ["login.withGitlab"] = "Mit GitLab anmelden",
            ["login.encrypted"] = "Verschlüsselte Verbindung",
            ["login.tag1"] = "Ihre Release-KPIs,",
            ["login.tag2"] = "in Echtzeit.",
            ["login.desc"] = "Verfolgen Sie den Fortschritt, das validierte Gewicht, die Geschwindigkeit pro Mitwirkender und Anomalien. Alles wird bei jeder Aktualisierung von GitLab synchronisiert.",
            ["login.welcome"] = "Willkommen",
            ["login.firstRunBadge"] = "Erste Anmeldung",
            ["login.firstRunTitle"] = "Willkommen bei KPI",
            ["login.firstRunSub"] = "Diese Instanz ist noch nicht konfiguriert. Starten Sie den Einrichtungsassistenten, um sie vorzubereiten — etwa 5 Minuten.",
            ["login.firstRunStep1"] = "Verbindung mit Ihrer GitLab-Instanz",
            ["login.firstRunStep2"] = "Auswahl der zu verfolgenden Projekte",
            ["login.firstRunStep3"] = "Produktionsphasen &amp; Teams",
            ["login.setupCta"] = "Einrichtung starten",
            ["login.setupOpening"] = "Assistent wird geöffnet…",
            ["login.firstRunAdmins"] = "Nur für Administratoren",
            ["login.frTag1"] = "Richten wir Ihren",
            ["login.frTag2"] = "KPI-Bereich ein.",
            ["login.frDesc"] = "Wenige Schritte, um GitLab zu verbinden, die zu verfolgenden Projekte auszuwählen und Ihre Produktionsphasen festzulegen. Alles lässt sich später anpassen.",
            ["login.ssoSub"] = "Melden Sie sich mit Ihrem GitLab-Konto an.",
            ["login.oauthRequired"] = "Die GitLab-Anmeldung (SSO) ist auf diesem Server nicht konfiguriert – setzen Sie Auth.ClientId / ClientSecret und starten Sie neu.",
        },
        ["it"] = new()
        {
            ["login.withGitlab"] = "Accedi con GitLab",
            ["login.encrypted"] = "Connessione crittografata",
            ["login.tag1"] = "I tuoi KPI di release,",
            ["login.tag2"] = "in tempo reale.",
            ["login.desc"] = "Traccia avanzamento, peso convalidato, velocità per contributore e anomalie. Tutto si sincronizza da GitLab ad ogni aggiornamento.",
            ["login.welcome"] = "Benvenuto",
            ["login.firstRunBadge"] = "Primo accesso",
            ["login.firstRunTitle"] = "Benvenuto su KPI",
            ["login.firstRunSub"] = "Questo spazio non è ancora configurato. Avvia la procedura guidata per prepararlo — circa 5 minuti.",
            ["login.firstRunStep1"] = "Connessione alla tua istanza GitLab",
            ["login.firstRunStep2"] = "Scelta dei progetti da monitorare",
            ["login.firstRunStep3"] = "Fasi di produzione e team",
            ["login.setupCta"] = "Avvia la configurazione",
            ["login.setupOpening"] = "Apertura della procedura guidata…",
            ["login.firstRunAdmins"] = "Riservato agli amministratori",
            ["login.frTag1"] = "Configuriamo il tuo",
            ["login.frTag2"] = "spazio KPI.",
            ["login.frDesc"] = "Pochi passaggi per collegare GitLab, scegliere i progetti da monitorare e definire le tue fasi di produzione. Potrai modificare tutto in seguito.",
            ["login.ssoSub"] = "Accedi con il tuo account GitLab.",
            ["login.oauthRequired"] = "L'accesso GitLab (SSO) non è configurato su questo server: imposta Auth.ClientId / ClientSecret e riavvia.",
        },
        ["pt"] = new()
        {
            ["login.withGitlab"] = "Iniciar sessão com GitLab",
            ["login.encrypted"] = "Ligação encriptada",
            ["login.tag1"] = "Os seus KPIs de release,",
            ["login.tag2"] = "em tempo real.",
            ["login.desc"] = "Acompanhe o progresso, peso validado, velocidade por colaborador e anomalias. Tudo é sincronizado a partir de GitLab em cada atualização.",
            ["login.welcome"] = "Bem-vindo",
            ["login.firstRunBadge"] = "Primeira sessão",
            ["login.firstRunTitle"] = "Bem-vindo ao KPI",
            ["login.firstRunSub"] = "Esta instância ainda não está configurada. Inicie o assistente de configuração para a preparar — cerca de 5 minutos.",
            ["login.firstRunStep1"] = "Ligação à sua instância GitLab",
            ["login.firstRunStep2"] = "Escolha dos projetos a acompanhar",
            ["login.firstRunStep3"] = "Fases de produção e equipas",
            ["login.setupCta"] = "Começar a configuração",
            ["login.setupOpening"] = "A abrir o assistente…",
            ["login.firstRunAdmins"] = "Reservado a administradores",
            ["login.frTag1"] = "Vamos configurar o seu",
            ["login.frTag2"] = "espaço KPI.",
            ["login.frDesc"] = "Alguns passos para ligar o GitLab, escolher os projetos a acompanhar e definir as suas fases de produção. Poderá ajustar tudo depois.",
            ["login.ssoSub"] = "Inicie sessão com a sua conta GitLab.",
            ["login.oauthRequired"] = "O início de sessão com GitLab (SSO) não está configurado neste servidor — defina Auth.ClientId / ClientSecret e reinicie.",
        },
        ["ru"] = new()
        {
            ["login.withGitlab"] = "Войти через GitLab",
            ["login.encrypted"] = "Зашифрованное соединение",
            ["login.tag1"] = "Ваши KPI выпуска,",
            ["login.tag2"] = "в реальном времени.",
            ["login.desc"] = "Отслеживайте прогресс, валидный вес, скорость по участникам и аномалии. Все синхронизируется из GitLab при каждом обновлении.",
            ["login.welcome"] = "Добро пожаловать",
            ["login.firstRunBadge"] = "Первый вход",
            ["login.firstRunTitle"] = "Добро пожаловать в KPI",
            ["login.firstRunSub"] = "Это пространство ещё не настроено. Запустите мастер настройки, чтобы подготовить его — около 5 минут.",
            ["login.firstRunStep1"] = "Подключение к вашему экземпляру GitLab",
            ["login.firstRunStep2"] = "Выбор проектов для отслеживания",
            ["login.firstRunStep3"] = "Этапы производства и команды",
            ["login.setupCta"] = "Начать настройку",
            ["login.setupOpening"] = "Открытие мастера…",
            ["login.firstRunAdmins"] = "Только для администраторов",
            ["login.frTag1"] = "Настроим ваше",
            ["login.frTag2"] = "пространство KPI.",
            ["login.frDesc"] = "Несколько шагов, чтобы подключить GitLab, выбрать проекты для отслеживания и задать этапы производства. Всё можно изменить позже.",
            ["login.ssoSub"] = "Войдите с помощью вашей учётной записи GitLab.",
            ["login.oauthRequired"] = "Вход через GitLab (SSO) не настроен на этом сервере — укажите Auth.ClientId / ClientSecret и перезапустите.",
        },
        ["ar"] = new()
        {
            ["login.withGitlab"] = "تسجيل الدخول باستخدام GitLab",
            ["login.encrypted"] = "اتصال مشفر",
            ["login.tag1"] = "مؤشرات الأداء الرئيسية للإصدار الخاص بك،",
            ["login.tag2"] = "في الوقت الفعلي.",
            ["login.desc"] = "تتبع التقدم والوزن المُتحقق منه والسرعة لكل مساهم والحالات الشاذة. يتم مزامنة كل شيء من GitLab عند كل تحديث.",
            ["login.welcome"] = "مرحباً",
            ["login.firstRunBadge"] = "الإعداد الأول",
            ["login.firstRunTitle"] = "مرحباً بك في KPI",
            ["login.firstRunSub"] = "لم يتم تكوين هذه المساحة بعد. ابدأ معالج الإعداد لتجهيزها — نحو 5 دقائق.",
            ["login.firstRunStep1"] = "الاتصال بمثيل GitLab الخاص بك",
            ["login.firstRunStep2"] = "اختيار المشاريع المراد تتبعها",
            ["login.firstRunStep3"] = "مراحل الإنتاج والفِرق",
            ["login.setupCta"] = "بدء الإعداد",
            ["login.setupOpening"] = "جارٍ فتح المعالج…",
            ["login.firstRunAdmins"] = "للمسؤولين فقط",
            ["login.frTag1"] = "لنُهيّئ",
            ["login.frTag2"] = "مساحة KPI الخاصة بك.",
            ["login.frDesc"] = "بضع خطوات لربط GitLab واختيار المشاريع المراد تتبعها وتحديد مراحل الإنتاج. يمكنك ضبط كل شيء لاحقاً.",
            ["login.ssoSub"] = "سجّل الدخول بحساب GitLab الخاص بك.",
            ["login.oauthRequired"] = "تسجيل الدخول عبر GitLab (SSO) غير مُكوَّن على هذا الخادم — عيّن Auth.ClientId / ClientSecret ثم أعد التشغيل.",
        },
        ["zh"] = new()
        {
            ["login.withGitlab"] = "使用GitLab登录",
            ["login.encrypted"] = "加密连接",
            ["login.tag1"] = "您的发布KPI，",
            ["login.tag2"] = "实时。",
            ["login.desc"] = "跟踪进度、已验证权重、每个贡献者的速度和异常。一切都在每次刷新时从GitLab同步。",
            ["login.welcome"] = "欢迎",
            ["login.firstRunBadge"] = "首次登录",
            ["login.firstRunTitle"] = "欢迎使用 KPI",
            ["login.firstRunSub"] = "此工作区尚未配置。启动安装向导即可完成准备，大约需要 5 分钟。",
            ["login.firstRunStep1"] = "连接到您的 GitLab 实例",
            ["login.firstRunStep2"] = "选择要跟踪的项目",
            ["login.firstRunStep3"] = "生产阶段与团队",
            ["login.setupCta"] = "开始配置",
            ["login.setupOpening"] = "正在打开向导…",
            ["login.firstRunAdmins"] = "仅限管理员",
            ["login.frTag1"] = "一起配置您的",
            ["login.frTag2"] = "KPI 工作区。",
            ["login.frDesc"] = "只需几步即可连接 GitLab、选择要跟踪的项目并定义您的生产阶段。之后您可随时调整。",
            ["login.ssoSub"] = "使用您的 GitLab 账户登录。",
            ["login.oauthRequired"] = "此服务器尚未配置 GitLab (SSO) 登录——请设置 Auth.ClientId / ClientSecret 后重启。",
        },
        ["ja"] = new()
        {
            ["login.withGitlab"] = "GitLabでサインイン",
            ["login.encrypted"] = "暗号化接続",
            ["login.tag1"] = "リリースKPI、",
            ["login.tag2"] = "リアルタイム。",
            ["login.desc"] = "進捗、検証済み重み、貢献者別速度、異常を追跡。すべてはリフレッシュのたびにGitLabから同期。",
            ["login.welcome"] = "ようこそ",
            ["login.firstRunBadge"] = "初回ログイン",
            ["login.firstRunTitle"] = "KPIへようこそ",
            ["login.firstRunSub"] = "このスペースはまだ構成されていません。セットアップアシスタントを起動して準備しましょう（約5分）。",
            ["login.firstRunStep1"] = "GitLabインスタンスへの接続",
            ["login.firstRunStep2"] = "追跡するプロジェクトの選択",
            ["login.firstRunStep3"] = "制作フェーズとチーム",
            ["login.setupCta"] = "セットアップを開始",
            ["login.setupOpening"] = "アシスタントを起動中…",
            ["login.firstRunAdmins"] = "管理者専用",
            ["login.frTag1"] = "あなたの",
            ["login.frTag2"] = "KPIスペースを構成。",
            ["login.frDesc"] = "GitLabの接続、追跡するプロジェクトの選択、制作フェーズの定義まで数ステップ。すべて後から調整できます。",
            ["login.ssoSub"] = "GitLab アカウントでサインイン。",
            ["login.oauthRequired"] = "このサーバーでは GitLab（SSO）ログインが未設定です — Auth.ClientId / ClientSecret を設定して再起動してください。",
        },
    };
}
