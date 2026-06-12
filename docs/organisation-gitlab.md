# Organisation GitLab — hiérarchie des groupes & rôles des équipes

Modèle d'organisation des groupes GitLab pour supporter le multi-produits (multi-repos),
avec le principe du moindre privilège.

## 1. Hiérarchie des groupes (modèle)

```
<groupe-racine>/                  ← équipes transverses + token de service
├─ <produit-A>/                   ← un sous-groupe par produit
│   ├─ <repo-app>
│   └─ <repo-api>                 (autant de repos que nécessaire)
├─ <produit-B>/
│   └─ <repo-app>
├─ partage/                       ← libs & services communs (équipe Back)
└─ rnd/                           ← bac à sable R&D (isolé des produits)
```

### Principes

- **Transverses à la racine, équipes produit dans leur sous-groupe.** Un membre d'un groupe
  hérite du même rôle sur tous les projets et sous-groupes en dessous. L'héritage **descend**
  (jamais l'inverse) et il est **additif** : on peut élever un rôle localement sur un projet,
  jamais le réduire.
- **Un nouveau produit = un nouveau sous-groupe.** Les équipes transverses (rattachées à la
  racine) y ont accès automatiquement, sans aucune action.
- **Les équipes produit sont cloisonnées** : membres uniquement de leur sous-groupe, elles ne
  voient pas les autres produits.
- **Éviter les membres directs au niveau projet** : tous les accès passent par les groupes.
  L'ajout direct sur un projet est réservé aux élévations ponctuelles (voir § 3).
- **Token de service** : un *Group Access Token* unique créé à la racine (scope `read_api`,
  rôle Reporter) couvre tous les produits, présents et futurs (extraction de données,
  résolution des membres). Jamais de token par projet.

## 2. Rôles des équipes

| Équipe | Rattachement | Rôle GitLab |
|---|---|---|
| **CTO** | racine | **Owner** |
| **PO** | racine | **Reporter** |
| **QA** | racine | **Reporter** |
| **UI/UX** | racine | **Reporter** |
| **Équipes Dev** (une par produit) | sous-groupe de leur produit | **Developer** (lead : **Maintainer**) |
| **Back** | sous-groupe `partage/` + sous-groupes des produits concernés | **Developer** |
| **R&D** | sous-groupe `rnd/` uniquement | **Developer** (lead : **Maintainer**) |

### Pourquoi ces rôles

| Rôle | Ce qu'il couvre | Attribué à |
|---|---|---|
| **Reporter** | Tout le pilotage du tracker, sans toucher au code : éditer/assigner/fermer les issues, poser les labels, gérer labels/milestones/epics/boards/poids (au niveau groupe comme projet). | Équipes dont la validation passe par les labels (PO, QA, UI/UX) |
| **Developer** | La frontière du code : push (branches non protégées), création et **approbation de merge requests**, wiki. | Équipes qui produisent du code |
| **Maintainer** | Réglages du projet/groupe, membres, branches protégées, tokens. | Leads techniques |
| **Owner** | Suppressions destructrices (issues, MRs, epics, projets, groupes) et administration des groupes. Dans GitLab, le travail se **ferme**, il ne se supprime pas : la suppression efface l'historique — elle est réservée à l'Owner comme soupape exceptionnelle. | CTO (+ 1 backup au plus) |

## 3. Ajustements ponctuels

- **Approbation de MR par un non-développeur** (ex. un QA approbateur sur un produit) :
  l'élever **Developer sur ce projet uniquement** — l'héritage étant additif, le reste de ses
  accès ne change pas.
- **Membre Back intervenant sur un nouveau produit** : l'ajouter Developer au sous-groupe de
  ce produit.

## 4. Points d'attention

- Un Reporter peut **supprimer des labels et des milestones** (« qui gère peut supprimer »).
  Si un workflow dépend de labels précis (ex. `Prod::*`), leur gestion relève d'une discipline
  d'équipe : le PO en est le gardien, les autres équipes se limitent à poser/retirer ces labels
  sur les issues.
- Les **bots** GitLab (`project_*_bot*`, `group_*_bot*`, porteurs des access tokens) sont des
  identités de service : ils n'ont pas vocation à ouvrir des sessions applicatives.
- **Transférer un projet** dans un sous-groupe ne change pas son ID (les intégrations par
  `ProjectId` survivent) ; GitLab pose des redirections sur l'ancien chemin.
- Epics, itérations et poids d'issues sont des fonctionnalités **Premium**.
