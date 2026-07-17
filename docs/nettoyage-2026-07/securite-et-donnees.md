# Sécurité & données conservées

> Issue de la passe de nettoyage/sécurité de juillet 2026 (audit multi-agents + correctifs).
> Objectif atteint : **aucune donnée critique en clair au repos**.

## 1. Correctifs appliqués

| # | Constat (avant) | Correctif |
|---|---|---|
| 1 | `Servers[].GroupToken` (token GitLab `read_api`) et `Auth.ClientSecret` (secret OAuth) **en clair** dans `appsettings.json` runtime | Chiffrés au repos au format `enc:v1:<base64>` (DataProtection, purpose `Kpi.Config.v1`, clés `dp-keys/`). Déchiffrement **en mémoire seulement** au chargement (serveur et CLI). **Migration automatique au démarrage** : toute valeur en clair trouvée est réécrite chiffrée (idempotent, atomique). |
| 2 | Le wizard `/setup` persistait le **token de groupe GitLab en clair dans `localStorage`** (`kpi-setup.token`) | `token` retiré des clés persistées ; un blob existant est auto-purgé à la prochaine sauvegarde du wizard (le token n'est plus jamais réécrit). À la recharge du wizard, le token doit être resaisi. |
| 3 | `GET /api/config` renvoyait la config au navigateur en masquant `ClientSecret` mais **pas `GroupToken`** ; `GET /api/config/token` renvoyait le token **en clair** au navigateur | **Endpoints supprimés** (`/api/config` GET+POST, `/api/config/token`, `/api/accounts` GET+POST) — plus aucun front ne les appelait. La config s'édite via `/setup`, l'onglet Options, ou le fichier. |
| 4 | Clé maîtresse DataProtection (`dp-keys/*.xml`) stockée **en clair** à côté du binaire — « clé sous le paillasson » | Sous Windows, les clés sont protégées par **DPAPI** (compte utilisateur courant), côté serveur web ET côté SecureStore. ⚠️ Conséquence : serveur et CLI doivent tourner **sous le même compte Windows** ; une migration de machine/compte invalide les clés (re-extraire les données, re-saisir les secrets). |
| 5 | Le repli web mono-serveur (`RunRefreshAsync` → `RunFullExportAsync`) pouvait écrire des exports **en clair** à la racine de `output/` | Branche supprimée : le web ne déclenche plus que le pipeline multi-serveurs **chiffré**. Sans serveur configuré, `/api/refresh` répond par une erreur explicite. |
| 6 | Connexion par token personnel (`POST /api/auth/token`) : surface d'API anonyme devenue morte (le formulaire avait été retiré de `/login`) | Endpoint et handler supprimés — **SSO GitLab uniquement**. |

## 2. Inventaire des données conservées

### Côté serveur (disque)

| Emplacement | Contenu | Sensibilité | Protection |
|---|---|---|---|
| `bin/…/appsettings.json` (runtime) | Config : serveurs GitLab, phases, labels, équipes (usernames), fériés, fenêtre de travail. Secrets : `GroupToken`, `ClientSecret` | Secrets + PII légère (usernames, `Auth.AdminUsers`) | Secrets **chiffrés** (`enc:v1:`) ; le reste en clair (nécessaire au fonctionnement). Fichier gitignoré. |
| `output/<serveur>/issues.json`, `labels.json`, `milestones.json` | Issues extraites : titres, états, poids, dates, URLs, et PII sous forme d'**usernames GitLab** (auteur, assignés, événements de label, approbateurs de MR). Ni e-mail ni nom complet. | PII légère (usernames) + contenu projet | **Chiffrés** (SecureStore, AES-256-CBC + HMAC, sous-clé par serveur). Gitignorés. |
| `output/accounts.json` | Comptes & vues restreintes (`ResolveAccount`) : usernames, noms d'affichage, vues par équipe. *N'existe pas tant que la fonctionnalité n'est pas utilisée.* | PII légère | En clair (lecture seule par le serveur). Gitignoré. Piste : le chiffrer via SecureStore s'il devient utilisé. |
| `bin/…/user-pages.json` | Layouts du **dashboard modulaire par utilisateur** (indexé par username) : ids/labels de pages, types de widgets, largeurs. Aucun secret, aucune PII au-delà du username (clé). | Faible | En clair, gitignoré. Écrit par `POST /api/my-pages` (uniquement sous le username courant). Même posture qu'`accounts.json`. Détail : [dashboard-modulaire.md](../design/dashboard-modulaire.md). |
| `dp-keys/*.xml` | Clés DataProtection (sessions + chiffrement au repos) | Critique (déverrouille le reste) | **DPAPI** sous Windows (compte courant). Gitignoré. |
| Logs | stdout/stderr uniquement (aucun fichier de log) : progression d'extraction, erreurs. Aucun secret loggé. | Faible | — |

### Côté navigateur

| Stockage | Contenu | Sensibilité |
|---|---|---|
| Cookie `gle_session` | Session (username + serveur), **chiffré** par DataProtection ; `HttpOnly`, `SameSite=Lax`, 8 h glissantes | OK |
| Cookie `.AspNetCore.Culture` | Langue de l'interface | Aucune |
| `localStorage app-*` (8 clés) | Préférences UI (onglet, thème, accent, densité…) | Aucune |
| `localStorage kpi-setup` | État du wizard `/setup` (étapes, projets, associations) — **sans le token** depuis le correctif #2 | Faible |

### Points de rétention (à connaître)

- **Aucun mécanisme de purge** : le merge d'extraction conserve indéfiniment les issues sorties du périmètre, et les données restent tant que `output/` existe. Suppression = supprimer le dossier `output/<serveur>/` (les fichiers étant chiffrés par sous-clé serveur, la suppression de `dp-keys/` les rend aussi illisibles).
- Le store existe **en double** en développement (`output/` à la racine du dépôt ET `bin/Debug/net10.0/output/`) — artefact du répertoire de travail ; penser à purger les deux.

## 3. Risques résiduels assumés (documentés, non corrigés)

| Risque | Pourquoi c'est accepté |
|---|---|
| CSP avec `unsafe-inline`/`unsafe-eval` | Inhérent à l'architecture (JSX transpilé par Babel **dans le navigateur**, scripts inline). Le payload est neutralisé à l'inlining (échappement `<`, `>`, `&`, U+2028/9). Une vraie correction = étape de build front (gros chantier). |
| `/api/setup/test` et `/api/setup/labels` anonymes **pendant le bootstrap** | Nécessaire à la 1ʳᵉ mise en service (aucun admin n'existe encore). Verrouillés dès que l'instance est configurée ; garde `SetupHostAllowed` + rate-limit. Fenêtre d'exposition = les minutes du premier setup, sur localhost. |
| `/api/status` accessible à tout membre connecté (pas seulement admin) | Ne divulgue que des compteurs de progression et un éventuel message d'erreur. |
| Commandes **CLI** historiques (`dotnet run` sans `--serve`, `--fetch-all`, `--views-only`) : écrivent CSV/JSON/HTML **en clair** dans `output/` | Usage volontaire et local (export « pour humains », type téléchargement CSV). À réserver aux besoins ponctuels ; ne pas planifier en tâche automatique. Candidat à suppression si personne ne s'en sert — à décider. |
| Secrets visibles de l'admin local | Un admin ayant accès au serveur ET au compte Windows peut déchiffrer (DPAPI). C'est le modèle de menace assumé d'une app auto-hébergée mono-machine. |

## 4. Ce que le chiffrement `enc:v1:` implique au quotidien

- **Rien à faire** : le setup chiffre à l'écriture, le boot migre les valeurs collées à la main, le chargement déchiffre en mémoire.
- Un secret peut toujours être fourni **par variable d'environnement** (`KPI_Servers__0__GroupToken=…`) : il est alors lu tel quel (jamais écrit sur disque).
- Si `dp-keys/` est perdu : les secrets et les données extraites deviennent illisibles → re-saisir les tokens via `/setup` et relancer une extraction.
