# Migration — retrait du bloc `GitLab` (config mono-serveur → multi-serveur)

À partir de la version **1c-D**, KPI est **100 % multi-serveur** : l'ancien bloc de configuration
mono-serveur `GitLab` (`appsettings.json`) a été **supprimé**. La configuration se fait désormais
uniquement via la liste `Servers`.

> ⚠️ **Action requise** pour toute instance configurée avec l'ancien bloc `GitLab` : convertir ce bloc
> en une entrée `Servers` **avant** de redémarrer (sinon l'app considère l'instance non configurée et
> redirige vers `/setup`). Il n'y a **pas** de migration automatique au démarrage.

## Conversion

**Avant** (`appsettings.json`, ancien format) :

```json
{
  "GitLab": {
    "BaseUrl": "https://gitlab.exemple.com",
    "PrivateToken": "glpat-xxxxxxxx",
    "ProjectId": "42",
    "Milestone": "2026-R2",
    "AllowSelfSignedCertificates": false,
    "RequestTimeoutSeconds": 60
  },
  "Export": { ... },
  "Auth": { ... }
}
```

**Après** (nouveau format multi-serveur) :

```json
{
  "Servers": [
    {
      "Id": "interne",
      "BaseUrl": "https://gitlab.exemple.com",
      "GroupToken": "glpat-xxxxxxxx",
      "ProjectIds": ["42"],
      "AllowSelfSignedCertificates": false,
      "RequestTimeoutSeconds": 60
    }
  ],
  "Export": { ... },
  "Auth": { ... }
}
```

### Correspondance des champs

| Ancien `GitLab.*` | Nouveau `Servers[].*` | Note |
|---|---|---|
| `BaseUrl` | `BaseUrl` | identique |
| `PrivateToken` | `GroupToken` | un **Group Access Token** (scope `read_api`) est recommandé pour couvrir plusieurs projets |
| `ProjectId` | `ProjectIds: [...]` | devient une **liste** ; vide = tous les projets accessibles au token |
| `Milestone` | *(supprimé)* | plus de milestone globale — l'extraction prend **toutes** les issues, le filtrage milestone se fait dans l'UI |
| `AllowSelfSignedCertificates` | `AllowSelfSignedCertificates` | identique |
| `RequestTimeoutSeconds` | `RequestTimeoutSeconds` | identique |
| *(nouveau)* | `Id` | identifiant court, stable, unique (`[a-z0-9_-]`) ; sert de dossier sous `output/<Id>/` |

## Données extraites

- Le nouveau chemin d'extraction multi-serveur écrit les données **chiffrées** sous `output/<Id>/`
  (`--fetch-servers`, ou le refresh / la fin de `/setup`).
- Le serveur lit en priorité `output/<Id>/` (déchiffré) et **retombe** sur l'ancien `output/` en clair
  s'il existe — une instance migrée continue donc d'afficher ses anciennes données jusqu'au prochain refresh.
- L'export complet CLI (run par défaut, `--fetch-all`, `--views-only`) et les fetch unitaires
  (`--fetch-labels`, `--fetch-milestones`) utilisent désormais le **premier serveur** de `Servers`.

## Le plus simple : passer par `/setup`

Plutôt que d'éditer le JSON à la main, un admin peut (re)lancer l'assistant **`/setup`** : il écrit
directement une entrée `Servers` (instance + token de groupe + projets) et lance l'extraction.
