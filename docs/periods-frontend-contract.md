# Périodes dynamiques — contrat frontend (pour l'équipe UI / Claude design)

> Le **backend est prêt** (config + persistance + payload). Ce document décrit ce que **le frontend
> doit consommer** et **les points en dur à rendre dynamiques**. Le backend n'a PAS touché aux `.jsx`
> ni à `mapper.js` : c'est le périmètre de l'UI.

## 1. Nouvelle source de vérité : `window.__DATA__.periods`

Le payload expose désormais :

```js
window.__DATA__.periods = [
  { key: "dev",    name: "Développement", color: "#2188ff", timed: true  },
  { key: "review", name: "Revue de code", color: "#8957e5", timed: true  },
  { key: "qawait", name: "Attente QA",    color: "#b8800a", timed: true  },
  { key: "qa",     name: "QA",            color: "#c79a06", timed: true  },
  { key: "tofix",  name: "À corriger",    color: "#ec4899", timed: true  },
  { key: "po",     name: "Validation PO", color: "#0f9e8e", timed: true  },
  { key: "uiux",   name: "UI/UX",         color: "#2dd4bf", timed: false }
]
```

- **`key`** : identifiant stable (référencé par `window.__DATA__.labelPhases` : `label → key`).
- **`name`** : libellé affiché (renommable par l'admin).
- **`color`** : couleur hex de la période (légende, segments Gantt, colonnes).
- **`timed`** : `true` = comptée dans les durées ; `false` = segment Gantt seul (ex. `uiux`).
- **L'ordre de la liste = ordre des colonnes / de la légende.** Déterministe, piloté par l'admin.
- **`none` n'est jamais présent** dans `periods` (c'est le marqueur « non suivi » de `labelPhases`).

## 2. Règle FORTE : pas de config ⇒ aucune phase

`periods` **vide** ⇒ **aucune phase calculée ni affichée**. **Retirer le repli `Prod::*` historique** et
`DEFAULT_PH` : `phaseOf()` ne doit renvoyer une phase que via `labelPhases`. Si `labelPhases`/`periods`
sont vides, durées et Gantt restent vides (c'est voulu). Une instance non configurée passe par `/setup`
(qui seed les périodes par défaut ci-dessus).

## 3. Points en dur à rendre dynamiques (recensement)

**`Assets/app/mapper.js`** (cœur) :
- `DEFAULT_PH` (~l.18-23) : **supprimer**.
- `if (!PH_HAS_CFG) PH_MAP = DEFAULT_PH;` (~l.28) : **supprimer** (⇒ `PH_MAP = {}` si pas de config).
- `TIMED` en dur (~l.24) : dériver de `periods` → `var TIMED={}; (D.periods||[]).forEach(p=>{if(p.timed)TIMED[p.key]=1;});`
- `phaseOf` (~l.32) : retirer le repli préfixe `prod::ui/ux` ; ne garder que `PH_MAP[lo] || null`.
- `times()` (~l.54-70) : initialiser accumulateurs en itérant les keys `timed` de `periods` (au lieu des 6 keys en dur). **Préserver le cas spécial `tofix` ⇒ +1 retour** (voir §4).
- Listes/alias en dur (~l.186-190, 220) : `['dev','rev','qawait',...]` + alias `rev→review` → dériver de `periods` ; **supprimer l'alias `rev`**.

**`Assets/app/ui.jsx`** : `PHASE_VAR`/`PHASE_NAME` (~l.5-6) → construire depuis `periods` ; `phaseColor()` lit `p.color`. `window.PHASE_NAME` (~l.405) peuplé dynamiquement.

**`Assets/app/tab-dashboard.jsx`** : colonnes `<Th>` en dur (~l.79-84), `r.dev…` + totaux magiques (~l.95-96), grid `phaseAvg` (~l.154-160) → itérer les périodes.

**`Assets/app/tab-charts.jsx`** : `PH` + dual-key/alias `rev` (~l.6-8, 18, 26, 237, 252, 264-289) → dériver de `periods`, supprimer l'aliasing. **Rappel CLAUDE.md** : l'interactivité de l'onglet Graphiques doit AUSSI marcher dans l'export HTML autonome (`exportChartsHtml()`) — piloter par les `data-*` du DOM, valider `node --check` du JS reconstruit.

**`Assets/app/tab-calendar.jsx`** : `PHASES` (~l.5), légende toggle/tri (~l.13, 25-31), segments Gantt (~l.54-56) → itérer les périodes. **Préserver l'attribution d'auteur sur le segment `dev`** (`opacity 0.92` si `k==='dev' && who`).

**`Assets/app/tab-issues.jsx`** : `LABEL`/`PHASE_OF` en dur (~l.6-7, 11, 18, 73) → reconstruire `PHASE_OF` (reverse map) depuis `labelPhases` + couleurs depuis `periods`.

**CSS** `Assets/design/shared.css` (~l.26-31, 45-50) + `studio.css` (~l.350, 369) : variables `--p-dev`… en dur → soit couleurs inline via `phaseColor()`, soit injecter des `--p-<key>` au runtime depuis `periods`.

## 4. Limites / keys réservées
- **`tofix`** est couplée au compteur de « retours » (`mapper.js` `times()`). Autoriser le renommage du
  **`name`** mais **pas de la `key`** `tofix` (ou documenter comme réservée), sinon le compteur casse.
- **`uiux`** : `timed:false` ⇒ exclue des durées mais présente comme segment Gantt. Respecter le flag.
- `times()`/`segKey()` ne doivent dépendre QUE de `periods[].key/timed`, jamais de noms de labels en dur.

## 5. Côté backend (déjà fait — pour info)
- `Config/ExportConfig.cs` : `PeriodDefinition { Key, Name, Color, Timed }` + `ExportConfig.Periods`.
- `Views/DashboardView.cs` : payload `periods` (DTO `PeriodPayload`), `none` exclu, échappement anti-XSS hérité.
- `Server/WebDashboard.cs` : `/setup` persiste `Export.Periods` (PascalCase) + validation croisée `labelPhases`.
- `Views/SetupView.cs` : émet le seed `periods` (les 8 périodes par défaut) dans `POST /api/setup`.
- `appsettings.example.json` / `appsettings.json` : seed `Export.Periods`.
- ⚠ L'**éditeur de périodes** (renommer / ajouter / supprimer + couleur) dans `/setup` reste à faire côté UI.
