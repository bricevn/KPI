# Charte graphique KPI — socle existant (brief pour Claude Design)

> **But de ce document.** Il extrait *tel quel* le système de design déjà en place dans le code
> ([`Assets/design/studio.css`](../../Assets/design/studio.css) — 776 lignes,
> [`Assets/design/shared.css`](../../Assets/design/shared.css) — palette daltonien-safe).
> À donner à **Claude Design** comme base : la charte doit **formaliser et faire évoluer** ce socle
> (échelles nommées, cohérence, comblement des trous), **pas en inventer un nouveau** — l'app tourne
> déjà là-dessus, chaque token ci-dessous est câblé dans les composants.
>
> **Contraintes non négociables** (elles pilotent une app réelle) : thème clair **et** sombre complets ;
> accent configurable par l'utilisateur ; palette **daltonien-safe** (Okabe-Ito) où la couleur n'est
> **jamais** le seul signal (toujours doublée d'un glyphe/texte) ; densité compacte ; sidebar repliable.

---

## 1. Couleurs

Toutes les couleurs sont des **variables CSS** portées par `.app` (surfaces/chrome) et `.kpi-root`
(sémantique data). Deux thèmes complets. Claude Design doit les reprendre comme **tokens sémantiques
nommés** (pas des valeurs en dur) et proposer, si besoin, une échelle de gris/teintes plus régulière.

### 1.1 Surfaces & chrome (`.app`)

| Token | Rôle | Clair | Sombre |
|---|---|---|---|
| `--bg` | Fond application | `#eef1f6` | `#0a0e13` |
| `--panel` | Fond carte/panneau | `#ffffff` | `#141a22` |
| `--panel-2` | Surface secondaire (hover, pills, inputs) | `#f3f6fb` | `#1b232d` |
| `--panel-3` | Surface tertiaire (tracks, fills vides) | `#e9eef6` | `#222c37` |
| `--sidebar` | Fond sidebar | `#ffffff` | `#10151c` |
| `--ink` | Texte principal | `#0f1722` | `#e9eef4` |
| `--ink-dim` | Texte secondaire | `#56627a` | `#9aa6b6` |
| `--ink-faint` | Texte tertiaire / labels | `#8b97ad` | `#5f6b7a` |
| `--line` | Bordures | `#e6ebf3` | `#222c37` |
| `--line-2` | Bordures internes (lignes de tableau) | `#eef2f8` | `#1b232d` |
| `--accent` | Accent principal (**surchargé par l'utilisateur**) | `#1f6feb` | `#2b7fff` |
| `--accent-2` | Accent survol/pressé | `#155bcc` | `#4d97ff` |
| `--accent-soft` | Accent 10-16 % (fonds actifs) | `rgba(31,111,235,.10)` | `rgba(43,127,255,.16)` |
| `--link` | Liens | `#1a63d6` | `#5aa2ff` |
| `--contractual-border` / `--contractual-bg` | Cartes « contractuel » | `#0969da` / `rgba(9,105,218,.07)` | `#58a6ff` / `rgba(88,166,255,.11)` |

**Avatars** (6 teintes, attribution stable par personne) — clair : `#0072B2 #8957e5 #0f9e8e #d97706 #b3231b #1f6feb` ·
sombre : `#4DA3E0 #a978f0 #2dd4bf #f0a13b #ff6f63 #4d97ff`.

### 1.2 Sémantique data (`.kpi-root`, `shared.css`) — daltonien-safe (Okabe-Ito)

**Types d'issue** (synchronisés couleurs GitLab, séparés par luminance) :

| Token | Sens | Clair | Sombre |
|---|---|---|---|
| `--c-feature` | Feature | `#9400d3` | `#b86fe0` |
| `--c-enh` | Enhancement | `#c77dff` | `#dcb3ff` |
| `--c-bug` | Bug | `#e5392f` | `#ff6f63` |
| `--c-clientbug` | Client Bug | `#a51d16` | `#c75b50` |
| `--c-regression` | Régression | `#ff8f6b` | `#ffb59a` |

**Statuts** (jamais rouge+vert seuls — toujours doublés d'un glyphe) :

| Token | Sens | Clair | Sombre |
|---|---|---|---|
| `--c-good` | Validé | `#008A63` | `#2FB88E` |
| `--c-warn` | Attention | `#C77F00` | `#F2B441` |
| `--c-bad` | Risque | `#D55E00` | `#F0813B` |
| `--c-done` | Fermé/fait | `#0072B2` | `#4DA3E0` |
| `--c-neutral` | Ouvert/inactif | `#6B7682` | `#8893A0` |

**Phases de production** (`--p-*`, aussi surchargées à chaud par `Export.Periods`) :

| Token | Phase | Clair | Sombre |
|---|---|---|---|
| `--p-dev` | Development | `#2188ff` | `#58a6ff` |
| `--p-review` | Code review | `#8957e5` | `#a978f0` |
| `--p-qawait` | QA wait | `#b8800a` | `#f0a13b` |
| `--p-qa` | QA | `#c79a06` | `#eab308` |
| `--p-tofix` | To fix | `#ec4899` | `#f472b6` |
| `--p-po` | PO validation | `#0f9e8e` | `#2dd4bf` |

> Note pour Claude Design : les phases sont **dynamiques** (l'utilisateur crée/renomme/recolore ses phases
> au `/setup`). La charte doit donc définir *comment* générer une teinte de phase lisible (clair + sombre)
> plutôt qu'une liste figée — c'est un **générateur de couleur**, pas une palette close.

---

## 2. Typographie

| Rôle | Police (token) | Usage |
|---|---|---|
| **Display / chiffres** | `--disp-font` = `Space Grotesk` | Grands nombres KPI, titres, valeurs de tableau. Alternatives choisies par l'utilisateur : `IBM Plex Mono`, `system-ui`. |
| **Corps** | `system-ui, 'Segoe UI', sans-serif` | Texte courant, labels de ligne. |
| **Mono** | `--font-mono` = `ui-monospace, monospace` | Labels techniques (`Prod::…`), noms de cartes contractuelles. |

**Échelle observée** (à régulariser par Claude Design en échelle nommée) :

| Niveau | Taille | Graisse | Notes |
|---|---|---|---|
| KPI géant | 32px (27px compact) | 700 | `letter-spacing:-.02em`, `line-height:1` |
| Titre de page (h1) | 21px | 700 | |
| Nombre de carte | 30–32px | 700 | |
| Valeur de drill / stat | 20–21px | 700 | |
| Titre de section (h3) | 14–15px | 700 | |
| Corps / base | 13–14px | 400–500 | `line-height:1.45` |
| Tableau | 13px | 500–700 | |
| Meta / petit | 11–12px | 500–600 | |
| **Eyebrow / label** | 9.5–11px | 700 | `text-transform:uppercase`, `letter-spacing:.04–.08em` |

Graisses en usage : **400** (texte atténué), **500** (medium), **600** (labels/boutons), **700** (gras/chiffres), **800** (flèches, swatches).

---

## 3. Espacement, rayons, élévation, mouvement

### 3.1 Espacement
Valeurs en usage : `2 3 4 5 6 7 8 9 10 11 12 13 14 16 17 18 20 22 24 26 32 36`.
Rythme fin (pas ~2px), **irrégulier** (valeurs impaires fréquentes : 7, 9, 11, 13, 17).
**Ancres** : padding carte 16–18px · padding panneau 14–17px · gap sections 14–16px · gap filtres 9px.
→ *Recommandation Claude Design : proposer une échelle propre (base 4 ou 8) et une table de correspondance vers l'existant.*

### 3.2 Rayons de bordure
Valeurs : `3 4 5 6 7 8 9 10 11 12 13 14 16 18`, plus `999px` (pilule) et `50%` (cercle).
**Ancres** : bouton 11px · petit bouton/input 9px · carte 14–16px · panneau 16px · modale 18px · pilule/chip 999px · avatar 50% · points 3–4px.

### 3.3 Élévation (ombres)
- `--shadow` (clair) : `0 2px 4px rgba(15,30,60,.05), 0 8px 24px rgba(15,30,60,.05)`
- `--shadow-sm` : `0 1px 2px rgba(15,30,60,.06)`
- Popover : `0 12px 32px rgba(15,30,60,.18)` · Dropdown : `0 16px 40px rgba(0,0,0,.5)` · Modale : `0 24px 64px rgba(0,0,0,.4)`
- **Sombre : ombres désactivées** — l'élévation passe par les bordures (`--line`). *La charte doit définir les deux registres d'élévation.*

### 3.4 Mouvement
- Transitions hover/couleur/transform : **.12–.16s ease** ; tooltips **.13–.15s**.
- Modale `modal-pop .16s` · panneau latéral `panel-in .2s` · variable `--ease-out`.
- **Principe** : animer `transform` (jamais `opacity`) — le composant reste visible si l'animation est gelée/désactivée.

### 3.5 Dimensions de layout
Sidebar **228px** (repliée **68px**) · padding main `20px 24px 36px` (compact `14px 18px 28px`) ·
colonne Gantt 230px / Vélocité 236px · largeurs max : Options 860px, modale 560px (large 760px, plein 1120px).

---

## 4. Modèle de thème & personnalisation

- **Thème** : `auto | clair | sombre` — attribut `data-theme` sur `.app` / `.kpi-root`.
- **Accent** : choisi par l'utilisateur ; `--accent-soft` dérivé via `color-mix(in srgb, <accent> 15%, transparent)`.
- **Police des chiffres** : `Space Grotesk | IBM Plex Mono | system-ui`.
- **Densité** : classe `.compact` (paddings et tailles réduits).
- **Sidebar** : repliable (228 ↔ 68px).
- Préférences persistées en `localStorage` (`app-theme`, `app-accent`, `app-numfont`, `app-compact`, `app-sb`, `app-drill`, `app-lang`).

---

## 5. Inventaire des composants (futur registre modulaire)

Le socle contient déjà une bibliothèque — c'est la matière première des « éléments choisissables » par page.

- **Chrome** : sidebar (`.sb`), header (`.hd`), barre de filtres (`.fl`/`.pill`), panneau générique (`.pnl`), boutons (`.btn`, `-primary`, `-sm`, `-outline-accent`), contrôle segmenté (`.seg-lg`/`.g-seg`), multi-select (`.ms`).
- **Affichage data** : carte KPI (`.kcard`), tuile stat (`.dm-kpi`/`.cycstat-item`), tableaux (`.tbl`, `.cmpv-tbl`, `.pw-matrix`), donut, barres de phase (`.phase`), sparkline (`.sparkline`), barre de progression (`.kbar`/`.track`), Gantt (`.gantt`), graphe de vélocité (`.vrow`), badge delta (`.cmpv-delta`), badge statut (`.st-badge`), chips (`.chip`/`.lbl-chip`/`.tag`), avatars (`.avatar`/`.av-stack`), cartes super-groupe (`.sg-card`), cartes transversales (`.tv-card`), cartes anomalie (`.acard`).
- **Overlays** : modale (`.modal`, 3 layouts : `modal`/`panel`/`full`), popover, tooltip (`.infotip`).
- **Formulaires** : champ (`.field`), checklist, swatches, color picker (`.opt-pop`).

> Composants React déjà exportés (`window.*`) dans [`ui.jsx`](../../Assets/app/ui.jsx) :
> `MultiSelect, Donut, DonutMulti, Avatar, Spark, Progress, SparkLine, Modal, InfoTip, IssueLink, IssueRowMini, IssueDrill`, icônes et helpers couleur.

---

## 6. Trous à combler / décisions attendues de Claude Design

1. **Échelles nommées** : figer une échelle d'espacement (base 4/8) et de rayons, avec mapping vers l'existant.
2. **Échelle typographique** : nommer les niveaux (display / h1 / h2 / body / caption / eyebrow…) et fixer les tailles.
3. **Générateur de couleur de phase** : règle pour dériver une teinte lisible (clair + sombre) à partir d'une couleur utilisateur.
4. **Registre d'élévation** : deux jeux cohérents (ombres en clair, bordures en sombre).
5. **États** : normaliser hover / focus / actif / désactivé / sélectionné pour tous les composants (aujourd'hui traités au cas par cas).
6. **Nommage des tokens** : convention unique (ex. `--color-surface-1`, `--space-3`, `--radius-md`) — la charte devient la source de vérité, le CSS s'y aligne ensuite.
