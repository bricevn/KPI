# Architecture du code — carte de lecture

> Pour relire le projet sans surprise. À jour après la passe de nettoyage de juillet 2026.

## Vue d'ensemble

```
GitLab (API v4)
   │  extraction (tokens de groupe, scope read_api)
   ▼
Pipeline/ExportPipeline.cs ──► output/<serveur>/{issues,labels,milestones}.json   (CHIFFRÉS — SecureStore)
   │                                                       ▲
   ▼                                                       │ déchiffrement + merge
Server/WebDashboard.*.cs  (ASP.NET Core, localhost:5050) ──┘
   │  BuildScopedPayloadAsync → payload JSON filtré par compte
   ▼
Views/DashboardView.cs  →  page HTML AUTONOME :
   react.js + babel.js (vendor) + window.__DATA__ + mapper.js + i18n.js + *.jsx inlinés
```

## Le front : une page auto-contenue, sans build

Il n'y a **aucune étape de build front**. `DashboardView.BuildReferencePage` inline, dans l'ordre :

1. `Assets/vendor/react.js`, `react-dom.js`, `babel.js` ;
2. `window.__DATA__ = <payload JSON>` (échappé anti-XSS : `<`, `>`, `&`, U+2028/9), puis
   **`mapper.js`** qui construit `window.APP` de façon synchrone ;
3. `window.__LANG__` + **`i18n.js`** (définit `window.t`) ;
4. les `.jsx` dans l'ordre : `ui.jsx`, `tab-*.jsx`, `tweaks-panel.jsx`, `shell.jsx` —
   chacun dans son `<script type="text/babel">`, transpilé **dans le navigateur**.

### Conséquences (pièges connus)

- **Chaque `.jsx` est un scope isolé** : le partage inter-fichiers passe UNIQUEMENT par des globals
  `window.*` (`window.t`, `window.APP`, `window.InfoTip`, `window.TabDashboard`…). La liste des
  exports « publics » de `ui.jsx` est le `Object.assign(window, {…})` en fin de fichier — n'y mettre
  que ce qui est consommé ailleurs.
- Un littéral `</script>` dans un `.jsx` inliné **tue toute la page** silencieusement (composer la
  chaîne : `'</scri' + 'pt>'`). De même, U+2028/U+2029 dans un littéral regex JS est illégal.
- L'ajout d'un nouveau fichier `.jsx` doit être répercuté dans la **liste ordonnée** de
  `DashboardView.cs` (et l'ordre compte : `ui.jsx` avant les tabs, `shell.jsx` en dernier).

### Rôles des fichiers front

| Fichier | Rôle |
|---|---|
| `mapper.js` | `window.__DATA__` → `window.APP` : durées de phase (fenêtre ouvrée, anti-bruit, rôles actif/attente), KPIs, pivots, vélocité, anomalies, Gantt. Refiltré en place par `__applyFilters`. |
| `i18n.js` | 10 langues (fr en es de it pt ru ar zh ja). `window.t(clé, vars)`. Clés parfois construites dynamiquement (`t('nav_'+id)`, `t('an.'+k)`) — en tenir compte avant de « nettoyer ». |
| `ui.jsx` | Bibliothèque partagée : icônes, InfoTip, Modal, IssueDrill, MultiSelect, tris, navigation Gantt, export HTML des graphiques. |
| `tab-*.jsx` | Un fichier par onglet (`window.Tab<Nom>`). |
| `tweaks-panel.jsx` | Panneau « Tweaks » (styles alternatifs de l'onglet Graphiques). |
| `shell.jsx` | Coquille : sidebar, filtres globaux (synchro équipe→utilisateurs), thème, montage des onglets. |

## Le serveur : `WebDashboard` en classe partielle

| Fichier | Contenu |
|---|---|
| `Server/WebDashboard.cs` | Bootstrap Kestrel + auth cookie/OAuth, routes, refresh (extraction en tâche de fond), payload/data (`BuildScopedPayloadAsync`, cache par signature), migration des secrets au boot, état. |
| `Server/WebDashboard.Auth.cs` | `RequireAdmin`, rôles GitLab (cache des access levels, fail-closed), `ResolveAccount` (accounts.json), `/api/me`, impersonation. |
| `Server/WebDashboard.Setup.cs` | Assistant `/setup` : test de connexion, labels, OAuth, sauvegarde (écrit `appsettings.json`, secrets chiffrés), extraction initiale. |
| `Server/WebDashboard.Options.cs` | API de l'onglet Options : catalogue GitLab en direct (projets/labels/milestones), calcul du temps (fenêtre ouvrée, fériés, anti-bruit), sauvegarde phases/équipes (rôles). |

Autres briques : `Config/ExportConfig.cs` (modèle de config + `RepairColonKeyedMaps` — ⚠️ `.Bind()`
corrompt les clés contenant `:`), `Export/SecureStore.cs` (chiffrement au repos + secrets de config
`enc:v1:`), `Pipeline/ExportPipeline.cs` (extraction multi-serveurs, merge scopé par projet/milestone),
`Views/SetupView.cs` et `Views/LoginView.cs` (pages autonomes en `const string`), `Localization/Loc.cs`
(i18n des pages serveur).

## Modèle de données des phases (Piste 2, juillet 2026)

Chaque période porte un **`role`** : `active` (chronométrée + comptée dans le temps effectif),
`wait` (chronométrée, exclue de l'effectif), `nogc` (segment Gantt seul). `Timed` est dérivé
(`role != "nogc"`), `EffectivePhases` n'existe plus qu'en lecture de migration.
Contrat front détaillé : [periods-frontend-contract.md](../periods-frontend-contract.md).

## Vérifier une modification front

Harness local (données synthétiques, indépendant de GitLab) :

```powershell
node obj/harness/make-harness.js   # régénère obj/harness/harness.html depuis Assets/
node obj/harness/serve.js          # sert sur http://localhost:5610
```

Puis contrôler la console (0 erreur) et les onglets touchés. Le serveur réel (`--serve`, port 5050 —
imposé par le callback OAuth) doit être **arrêté avant `dotnet build`** (verrou sur `Kpi.exe`).

## Documents liés

- [rapport-nettoyage-2026-07.md](rapport-nettoyage-2026-07.md) — ce qui a été supprimé et pourquoi.
- [rapport-obvious.md](rapport-obvious.md) — spécificités Obvious restantes et pistes de configuration.
- [securite-et-donnees.md](securite-et-donnees.md) — posture sécurité + inventaire des données.
- [periods-frontend-contract.md](../periods-frontend-contract.md), [organisation-gitlab.md](../organisation-gitlab.md), [MIGRATION.md](../MIGRATION.md).
