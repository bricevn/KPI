# Rapport de nettoyage — juillet 2026

> Passe « clean code » : suppression du code mort et du code démo, vérifiée élément par élément
> (chaque suppression a été re-contrôlée par recherche exhaustive : usages `window.*`, clés i18n
> dynamiques `t(préfixe+x)`, classes CSS construites, HTML généré depuis C#). Build 0 erreur /
> 0 avertissement + tous les onglets vérifiés dans le harness après chaque lot.

## 1. Code démo supprimé

- **`Assets/app/data.js`** (12,6 Ko) : générateur de données factices — servait uniquement la route
  de démo `GET /ref` (dev only). Supprimé avec la route, la branche `payloadJson == null` de
  `BuildReferencePage` (le payload est désormais obligatoire) et le repli `ISSUE_BASE` de `ui.jsx`.
- Fichier parasite `Calcul` (0 octet, racine) supprimé.

## 2. Code mort supprimé — front (`Assets/app`)

| Fichier | Supprimé |
|---|---|
| `mapper.js` | Chaîne complète « traversée » (`spn`/`_span`/`_work`, `phaseAvg[].span`, `phaseTotals.work/span`) — résidus des sous-vues retirées ; `weightBuckets` (calculé, jamais affiché) ; champs exportés jamais lus (`milestone.startDay/endDay/today/weeks`, `cal.START`, libellés de `tabs`) |
| `ui.jsx` | Composants `Donut`, `DonutMulti`, `Spark`, helper `labelColor` (jamais référencés) ; exports `window.*` réduits au contrat réellement consommé (retrait de TYPE_VAR, PHASE_VAR, gitlabColor, WeightRecap, IssueRowMini, buildChartsExportDoc, chartsExportName — les fonctions internes restent) |
| `tab-charts.jsx` | Deux blocs `view === 'matrice'` inaccessibles (aucun toggle ne produit cette valeur) + helper `typeTotal` |
| `tweaks-panel.jsx` | 6 contrôles jamais utilisés (`TweakSlider/Toggle/Text/Number/Color/Button`) + helpers `__twkIsLight`/`__TwkCheck` ; en-tête de doc réécrit |
| `tab-anomalies.jsx` | `totalAnom` (calculé à chaque rendu, jamais affiché) |
| `tab-comparison.jsx` | Prop `big` de `DeltaPill` (jamais passée) |
| `tab-dashboard.jsx` | Attribut `data-comment-anchor` (artefact d'outil d'annotation du prototype) |
| `tab-options.jsx` | Libellés français en dur de `DRILL_LAYOUTS` (les libellés viennent de l'i18n) + correction du `.map(([k])…)` associé |
| `i18n.js` | **53 clés mortes × 10 langues** (~530 entrées) : anciennes générations de l'onglet Options (`opt.scope`, `opt.connection`, `opt.ph*`…), colonnes de phases figées (`tbl.*`), sous-vues retirées (`dash.tSpan/tRatio`, `charts.typeWeight/typePhase/lead`), `reglages`, `dash.issuesOpenClosed` ; export `window.I18N` retiré (tout passe par `window.t`) |
| `studio.css` / `shared.css` | **62 règles mortes** (`.donut`, `.sbar`, `.wbar`, `.legtbl`, `.sb-theme`, `.cmpv-chips`, `.cmpv-focus*`, `.charts-empty`, `.vel-foot`, `.kpi-pip`, etc.) + 2 doublons exacts (`.acard:hover`, `.muted`). Vérifié par tokenisation de toutes les classes émises (JSX + HTML généré en C#). |

## 3. Code mort supprimé — serveur (C#)

| Zone | Supprimé | Note |
|---|---|---|
| `WebDashboard` | Endpoints `GET/POST /api/config`, `GET /api/config/token`, `GET/POST /api/accounts` + handlers + DTO (`ConfigPayload`, `TokenPayload`, `TokenSentinel`, `ReadCurrentToken`) | Aucun front ne les appelait ; gain sécurité (plus de token renvoyé au navigateur). La **lecture** de `accounts.json` par `ResolveAccount` (rôles) est conservée. |
| `WebDashboard` | `POST /api/auth/token` + `LoginWithTokenAsync` (~80 l.) | Le formulaire token avait déjà été retiré de `/login` — SSO uniquement. Restaurable depuis l'historique git si besoin d'une API programmatique. |
| `WebDashboard` | Branches legacy mono-serveur de `RunRefreshAsync` (repli `RunFullExportAsync`) ; champ rétro-compat `RefreshRequest.milestone` (singulier) | Le repli écrivait des exports en clair ; remplacé par une erreur explicite. |
| `DashboardView` | Champs de payload jamais lus par le front : `transitionsConfig`, `transitionCounts`, `transitionDurations`, `commentsByAuthor`, `mergeRequests[].reviewers/isClosing` + paramètre `trackedTransitions` de toute la chaîne `BuildPayload*` | Allège chaque page servie. Les données restent dans le store chiffré. |
| `LoginView` | Page `Welcome`/`WelcomeHtml` (jamais servie), 6 `Replace()` sans placeholder cible, dictionnaire `jsI18n` réduit aux 4 clés réellement lues | |
| `Loc.cs` | **24 clés mortes × 10 langues** (`login.title`, `login.token*`, `setup.step`…) | |
| `ExportService` | Paramètre `filter` jamais alimenté de `BuildIssueExportsAsync` ; calcul + stockage `Comments.ByAuthor` (plus aucun consommateur) | Les anciens `issues.json` restent lisibles (propriété JSON inconnue ignorée). |
| `GitLabClient` | Propriété `ProjectSegment` | |

## 4. Conservé volontairement (signalé, pas supprimé)

| Élément | Pourquoi conservé |
|---|---|
| `Export.PeriodsByProject`, `Export.TeamGroups` | **Fonctionnalité à moitié câblée** : le wizard les écrit, rien ne les consomme (le mapper ne lit que les périodes globales). Décision produit à prendre : finir le câblage ou retirer l'option du wizard. |
| Chaîne « transitions » d'extraction (`TrackedTransitions` config → `ComputeTransitions` → store) | Calculée et stockée mais plus émise au front. Touche le chemin d'extraction (invérifiable sans GitLab) → à retirer lors d'une prochaine extraction contrôlée si personne n'en veut. |
| Chaîne CLI legacy (`RunFullExportAsync`, `CsvExporter`, vues statiques) | Fonctionnalité CLI volontaire (export CSV « pour humains ») ; écrit en clair — voir [securite-et-donnees.md](securite-et-donnees.md) §3. Candidate à suppression si inutilisée. |
| Champs `/api/status` (`lastRefreshAt`, `startedAt`) | Surface d'API diagnostique (curl), pas du code mort au sens strict. |
| Propriétés de désérialisation des DTO GitLab peu lues | Modèles d'API : le coût de retrait (risque de parsing) dépasse le gain. |

## 5. Réorganisation lisibilité

- **`Server/WebDashboard.cs` (1 826 l.) découpé en classe partielle** :
  `WebDashboard.cs` (bootstrap, routes, refresh, payload/data, état — 817 l.),
  `WebDashboard.Auth.cs` (231 l.), `WebDashboard.Setup.cs` (466 l.), `WebDashboard.Options.cs` (313 l.).
  Doc de classe réécrite (l'ancienne décrivait une étape « sans authentification » périmée).
- `Views/SetupView.cs` (145 Ko) **non découpé** : c'est un unique template `const string` (HTML+JS
  inline) — une découpe partielle n'apporterait rien. Piste réelle : sortir le JS du wizard dans
  `Assets/setup/*.js` inlinés comme le dashboard.
- Front : fichiers déjà séparés par onglet ; conventions documentées dans [architecture.md](architecture.md).

## 6. Vérifications

- `dotnet build` : **0 erreur / 0 avertissement** après chaque lot.
- Harness (données synthétiques, port 5610) : rendu des **8 onglets sans aucune erreur console**
  après la purge complète ; `node --check` sur `i18n.js` après chaque réécriture scriptée.
- Serveur réel (5050) : boot propre, `/login` rendue, SSO configuré détecté (donc secrets déchiffrés).
- Bilan volumétrique : `i18n.js` −22 Ko, `studio.css` −6 Ko, payload de page allégé
  (champs × nombre d'issues), ~700 lignes de C# mort retirées.
