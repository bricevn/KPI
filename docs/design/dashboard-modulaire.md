# Dashboard modulaire — architecture

> Ajouté sur la branche `feat/design-system`. Permet à **chaque utilisateur** de composer ses propres
> pages (nav de gauche) à partir d'un catalogue de **composants isolés**, sans écrire de JSON.
> Sans étape de build (cohérent avec le reste du front — cf. [architecture.md](../nettoyage-2026-07/architecture.md)).

## 1. Les trois couches

```
Charte de tokens (CSS)          Assets/design/charte-tokens.css
        │  variables --color-*/--space-*/--radius-*/--text-* (clair + sombre, accent configurable)
        ▼
Composants ISOLÉS (window.KPI)  Assets/components/*.jsx
        │  fonctions PURES de leurs props ; s'auto-enregistrent (registry.js)
        ▼
Renderer + données              Assets/app/page-renderer.jsx  (window.PageRenderer)
                                Assets/app/page-data.jsx      (window.KPIData — adaptateurs)
        │  une PAGE (modèle JSON) → widgets résolus (type→composant, data→props)
        ▼
Nav + éditeur                   Assets/app/shell.jsx  +  Assets/app/tab-page-editor.jsx
```

## 2. Double registre (le cœur)

Deux espaces de noms globaux, volontairement séparés (SRP / DIP) :

| Registre | Contenu | Qui l'écrit |
|---|---|---|
| `window.KPI[nom]` | le **composant** (fonction pure de ses props) | `registry.js` via `window.KPIGallery.register({name, render, …})` |
| `window.KPIData[clé]` | l'**adaptateur** `(APP, params, ctx) => props` — **seule** couche qui lit `window.APP` | `page-data.jsx` (live) / `page-fixtures.js` (galerie) |

Un widget est un triplet déclaratif résolu à l'exécution :

```js
const Comp    = window.KPI[w.type];        // ex. 'KpiCard'
const adapter = window.KPIData[w.data];    // ex. 'kpi.progress'
const props   = { ...adapter(window.APP, w.params, ctx), ...coerceParams(w.params) };
React.createElement(Comp, props);
```

**Conséquence (OCP)** : ajouter un composant ou une source de données n'oblige **jamais** à modifier le
renderer — l'identité `type === spec.name === clé window.KPI` suffit (aucune table de correspondance).

## 3. Modèle de page (JSON)

```json
{
  "id": "ma-vue", "kind": "modular",
  "nav": { "label": "Ma vue", "labelKey": "", "icon": "velocity", "order": 40, "showFilters": true },
  "layout": { "cols": 12, "gap": "var(--space-4)", "rowUnit": 88 },
  "widgets": [
    { "id": "w1", "type": "KpiCard", "data": "kpi.cycle", "layout": { "w": 4 }, "params": {} },
    { "id": "w2", "type": "Donut",  "data": "types.distribution", "layout": { "w": 8 }, "params": {} }
  ]
}
```

- `params` ne portent que des **valeurs sérialisables** (clés d'icône, presets, i18n) — jamais de nœud
  React ni de fonction. Les données riches passent TOUJOURS par `data` (un adaptateur).
- `coerceParams` type les chaînes du JSON (`"3"`→`3`, `"true"`→`true` ; `#fff`/`var(--x)` restent).

## 4. Robustesse (garde-fous)

`page-renderer.jsx` isole les défaillances — jamais de page blanche :
- **try/catch** autour de l'adaptateur → tuile `.widget-error` (« Données « … » indisponibles »).
- **`window.WidgetBoundary`** (React error boundary) → tuile en cas de crash au render.
- Type inconnu → tuile `.widget-missing` (« Widget « … » inconnu »).

## 5. Portée : « tout par utilisateur »

Il n'y a **pas** de pages partagées/globales. Chaque utilisateur a ses propres pages.

| Aspect | Détail |
|---|---|
| Stockage | `user-pages.json` à la racine binaire, **indexé par username GitLab** (portable entre appareils) |
| Lecture | `GET /api/my-pages` (utilisateur connecté) ; injection `window.__USER_PAGES__` **par requête**, HORS du payload mis en cache (jamais mélangé entre comptes) |
| Écriture | `POST /api/my-pages` : écrit UNIQUEMENT sous le username courant (`ctx.User.Identity.Name`) — impossible d'écrire pour autrui |
| Nav | `shell.jsx` liste `window.__USER_PAGES__`, trié par `nav.order` |
| Édition | onglet « Éditeur de pages » (tout utilisateur connecté) : réglages page + widgets + **aperçu live** ; **drag-and-drop** (HTML5 natif) pour réordonner pages et widgets, corbeille pour supprimer un widget ; `POST /api/my-pages` puis reload |

Validation serveur (`NormalizeDashboard`, légère) : id slug unique ≠ onglets natifs (`ReservedPageIds`),
type `[A-Za-z0-9_]`, `w` borné à `[1..cols]`, clés de params sans « : ». La validation profonde
(`type ∈ window.KPI`, `data ∈ window.KPIData`) est faite côté éditeur (le serveur ne peut pas exécuter le registre).

## 6. Sécurité

- **Aucun secret** dans ces flux : `/api/my-pages` ne manipule que des layouts (pas de token/PII sensible).
- **XSS** : `window.__USER_PAGES__` et `window.__DATA__` sont échappés à l'inlining par le helper unique
  `DashboardView.EscapeForInlineScript` (`<`, `>`, `&`, U+2028/U+2029) — cf. posture
  [securite-et-donnees.md](../nettoyage-2026-07/securite-et-donnees.md).
- **Isolation** : l'identité vient du serveur (cookie de session), jamais du corps de requête.
- `user-pages.json` : données non sensibles (layouts), en clair, **gitignoré** — même posture que `accounts.json`.

## 7. Catalogue de composants (aujourd'hui)

`window.KPIWidgets` (widgets-catalog.js) mappe chaque type à ses sources compatibles + largeur par défaut.

| Type | Sources `data` compatibles |
|---|---|
| `KpiCard` | `kpi.progress`, `kpi.weight`, `kpi.approvals`, `kpi.cycle` |
| `PhaseBars` | `phase.worked`, `phase.effective`, `phase.wait` |
| `Donut` | `types.distribution` |
| `DataTable` | `pivot.byType` |

Primitives réutilisables (aussi dans le banc d'isolation `/gallery`) : `Button`, `StatusBadge`, `Avatar`,
`AvatarStack`, `Chip`, `DeltaBadge`, `ProgressBar`, `Sparkline` (+ `KpiCard`, `Donut`, `PhaseBars`, `DataTable`, `GanttChart`).

## 8. Banc d'isolation `/gallery` (DEV only)

`Views/GalleryView.cs` + `Assets/components/gallery.jsx` : monte chaque composant seul (mode « Composants »)
et une page démo depuis un modèle JSON sur fixtures (mode « Pages »), avec bascule thème/accent/densité.
Route montée uniquement en environnement **Development** ([Properties/launchSettings.json](../../Properties/launchSettings.json)).

## 9. Ajouter un composant

1. `Assets/components/MonComposant.jsx` — fonction pure de ses props, auto-enregistrée (`registry.js`).
2. L'ajouter à `Views/DashboardAssets.ComponentFiles` (liste partagée galerie + dashboard).
3. Styles éventuels dans `Assets/components/components.css` (tokens de la charte).
4. Une source de données : ajouter un adaptateur `window.KPIData['ma.cle']` dans `page-data.jsx`
   (+ `page-fixtures.js` pour la galerie) et l'entrée dans `widgets-catalog.js`.
5. `dotnet build`, vérifier sur `/gallery` (clair + sombre) puis dans une page réelle.

## 10. Fichiers

| Fichier | Rôle |
|---|---|
| `Assets/design/charte-tokens.css` | Source de vérité des tokens (couleurs, espacement, rayons, typo, élévation, mouvement). |
| `Assets/design/charte-buttons.css` | Boutons 3 niveaux (chargé par la galerie ; pas encore app-wide). |
| `Assets/components/registry.js` | `window.KPIGallery` (register/all/get) + `window.KPI`. |
| `Assets/components/*.jsx` | Les composants isolés. |
| `Assets/components/components.css` | Styles des composants (`.kpi-*`, `.page-grid`, `.widget-*`). |
| `Assets/components/widgets-catalog.js` | `window.KPIWidgets` + `window.KPIDataCatalog` (métadonnées éditeur). |
| `Assets/components/gallery.jsx`, `page-fixtures.js` | Banc d'isolation + fixtures. |
| `Assets/app/page-renderer.jsx` | `window.PageRenderer`, `WidgetBoundary`, `coerceParams`. |
| `Assets/app/page-data.jsx` | Adaptateurs live `window.KPIData`. |
| `Assets/app/tab-page-editor.jsx` | Éditeur de pages (par utilisateur). |
| `Views/DashboardAssets.cs` | Liste partagée des composants. |
| `Server/WebDashboard.Pages.cs` | `NormalizeDashboard` (validation + plafonds), store `user-pages.json`, `GET/POST /api/my-pages`. |
| `Views/GalleryView.cs` | Page `/gallery` (DEV). |
