# Rapport — code spécifique à Obvious Technologies

> Demandé lors de la passe de nettoyage de juillet 2026 : inventaire de tout ce qui, dans le code,
> est propre à l'organisation (URLs, conventions de labels, noms, valeurs par défaut).
> Classement : **(a)** repli légitime · **(b)** comportement codé en dur à rendre configurable · **(c)** donnée à retirer.

## Corrigé pendant la passe ✅

| Élément | Classe | Correctif |
|---|---|---|
| `ui.jsx` — `ISSUE_BASE = 'https://gitlab.obvious.tech/hypervisor/-/issues/'` en dur | (c) | Supprimé : les liens d'issues n'utilisent plus que `meta.issueBase` dérivé des données réelles (`webUrl`). |
| `shell.jsx` — « Admin · Brice M. » codé en dur dans la sidebar | (c) | Remplacé par l'identité du compte connecté (`/api/me`), repli vide. |
| `DashboardView.cs` — `<title>Release 2026-R2 — Dashboard</title>` figé | (c) | Titre neutre « KPI — Dashboard ». |
| `data.js` (démo) — prénoms d'équipe reconnaissables, projet/milestone internes | (c) | Fichier supprimé avec le mode démo (`/ref`). |
| `ExportConfig.cs` — commentaire XML citant `https://gitlab.obvious.tech` | (c) | Exemple neutre `gitlab.example.com`. |
| `docs/MIGRATION.md` / doc — exemples « 2026-R2 » | (c) | Mention démo retirée de `periods-frontend-contract.md` ; MIGRATION.md conservé (historique). |

## Replis légitimes, conservés et documentés (a)

Ces valeurs ne s'appliquent **que si rien n'est configuré** — une autre organisation qui passe par
`/setup` ne les verra jamais. Elles reflètent le workflow historique d'Obvious :

- **`mapper.js` — `DEFAULT_PH`** : mapping de repli des labels `Prod::*` historiques
  (`Prod::Code In Progress` → dev, `Prod::QA (Hotfix) Backlog` → qawait, etc.). Actif seulement si
  `Export.LabelPhases` est vide. Documenté dans le README (§ Sans config).
- **Jeu de phases par défaut** dev / review / qawait / qa / tofix / po (+ uiux) : sert de repli au
  mapper et de seed au `/setup`. Noms neutres (anglais métier), pas propres à Obvious stricto sensu.
- **`appsettings.example.json`** : les exemples `TrackedLabels`/`LabelPhases` reprennent les labels
  `Prod::*` réels. Conservés volontairement : ils illustrent le repli ci-dessus. À neutraliser si le
  repli `Prod::` disparaît un jour.
- **`LICENSE`** — « Copyright (c) 2026 OODA » : mention légale voulue.

## Comportements codés en dur à rendre configurables (b) — reste à faire

Par ordre d'impact pour une réutilisation hors Obvious :

1. **Labels transversaux** (`mapper.js` — `TRANSV`) : `CONTRACTUAL`, `Unplanned`, `Surcharge QA`
   sont des noms de labels GitLab exacts, en dur. La section « Labels transversaux » du dashboard et
   l'anomalie « Surcharge QA » (comparaison au label `surcharge qa` en minuscules) ne fonctionnent
   qu'avec ces labels-là. → Proposition : liste configurable dans Options (comme les phases).
2. **Taxonomie `Type::*`** (`mapper.js`) : le préfixe scoped `type::`, la liste `KNOWN` des types
   curés et les regex des super-groupes (Features/Bugs/…) présument la convention de nommage
   d'Obvious. → Proposition : dériver les types des labels réellement présents + config des
   super-groupes.
3. **Filtre `Prod::` du setup** (`SetupView.cs`) : l'étape « Phases » ne propose à l'association
   **que** les labels préfixés `Prod::` — une organisation avec un autre préfixe ne voit aucun label.
   → Proposition : préfixe de scope saisissable dans le wizard (défaut `Prod::`).
4. **`guessPhase`** (`tab-options.jsx` / `SetupView.cs`) : heuristique de pré-association fondée sur
   des mots-clés du workflow d'Obvious (`code…progress`, `backlog`, `to fix`, `validation`…).
   Inoffensif (simple pré-remplissage), mais à garder en tête. Classe (a)/(b) limite.

## Verdict

Après la passe, il ne reste **aucune donnée organisationnelle en dur dans le code exécuté**
(URLs, personnes, releases). Ce qui reste relève de **conventions de workflow par défaut**
(labels `Prod::`/`Type::`, transversaux) — fonctionnelles pour Obvious, à basculer en configuration
le jour où l'app doit servir une autre organisation (points 1-3 ci-dessus).
