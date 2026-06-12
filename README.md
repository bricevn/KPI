# GitLab Exporter

Outil .NET 10 qui exporte les issues d'un projet GitLab pour une milestone donnée, avec :
- les labels actuels, le statut, le poids, les assignés ;
- l'historique daté des changements de labels (sur une liste de labels configurable) ;
- les transitions configurées (paires `From → To`) avec leurs **dates** et **durées** ;
- les Merge Requests liées (tous statuts) avec leurs approbateurs ;
- un **dashboard HTML interactif** (un seul fichier autonome) avec plusieurs onglets : Dashboard (KPIs), Graphiques (camemberts + barres, exportables), Anomalies, Issues (timeline d'événements), Calendrier (Gantt des phases), Vélocité (Gantt par personne).

Le dashboard est servi par un mini-serveur HTTP local (mode `--serve`) ou ouvrable directement comme fichier statique.

## Démarrage rapide (nouvelle installation)

1. **Cloner** ce dépôt et installer le **.NET SDK 10**.
2. **Configurer le minimum** : copier `appsettings.example.json` → `appsettings.json`, puis renseigner uniquement la section `Auth` (laisser `GitLab.*` vide — l'assistant le remplira) :
   ```jsonc
   "Auth": {
     "Authority":  "https://gitlab.votre-instance.com",  // votre instance GitLab
     "AdminUsers": ["votre_username_gitlab"],             // qui peut administrer / configurer
     "ClientId": "", "ClientSecret": "", "CallbackPath": "/signin-gitlab", "DefaultViewId": ""
   }
   ```
3. **Lancer** : `dotnet run -- --serve` → ouvrir http://localhost:5050/.
4. **Se connecter** (`/login`) : collez un **Personal Access Token** GitLab (scope `read_api`) de votre compte — qui doit être **membre du projet** à analyser. *(OAuth GitLab possible si vous renseignez `Auth.ClientId/ClientSecret` + le Redirect URI `…/signin-gitlab`.)*
5. **Assistant de mise en service** (`/setup`, admin) : testez la connexion, choisissez les projets, **associez librement vos labels aux phases de temps**, vérifiez les équipes, puis « Lancer le dashboard ».

> **Modèle d'accès** : seuls les **membres GitLab du projet** peuvent se connecter ; l'**admin** = la liste `Auth.AdminUsers` du fichier serveur (non modifiable via l'app, à éditer sur le serveur). Aucun token utilisateur n'est stocké ; le token de service saisi au `/setup` sert à l'extraction et reste côté serveur. Les comptes bot GitLab sont refusés à la connexion.

## Prérequis

- .NET SDK 10 (`dotnet --version` doit afficher `10.x` ou plus).
- Un Personal Access Token GitLab avec au minimum le scope `read_api`.
- Accès réseau à l'instance GitLab.

## Configuration

Toute la configuration est dans [appsettings.json](appsettings.json) (gitignoré). Un template versionnable est dans [appsettings.example.json](appsettings.example.json).

| Clé | Description |
|---|---|
| `GitLab.BaseUrl` | URL **racine** de l'instance (ex: `https://gitlab.obvious.tech`), **sans** `/api/v4`. |
| `GitLab.PrivateToken` | Personal Access Token (scope `read_api`). |
| `GitLab.ProjectId` | ID numérique (ex: `4`) ou chemin URL-encoded (`groupe/projet`). |
| `GitLab.Milestone` | Titre **exact** de la milestone — sensible à la casse (ex: `2026-R2`, pas `2026-r2`). |
| `GitLab.AllowSelfSignedCertificates` | `true` si l'instance utilise un certificat self-signed. |
| `GitLab.RequestTimeoutSeconds` | Timeout HTTP (défaut 60). |
| `Export.OutputDirectory` | Dossier de sortie relatif (défaut `output`). |
| `Export.TrackedLabels` | Liste des labels `Prod::*` suivis dans l'historique (events `add`/`remove`). Inclut Code In Progress, Code (pre-)review, QA, To Fix, PO Validation, et les `Prod::UI/UX *`. |
| `Export.TrackedTransitions` | Liste de paires `{ "From": "...", "To": "..." }`. Pour chacune on enregistre dates et durée. |
| `Export.Teams` | Dictionnaire `{ "Équipe": ["user1", "user2"] }` pour le filtre « Équipe » du dashboard. |

> 💡 **Édition via l'UI** : en mode `--serve`, l'onglet **Options → Configuration** permet d'éditer la plupart de ces réglages sans toucher au fichier : section **Gitlab** (Base URL, Private Token masqué, Project ID, Allow Self Signed Certificates, Request Timeout Seconds), plus **Tracked Labels** (liste déroulante à cocher), **Tracked Transitions** (paires From→To) et **Teams** (équipes + membres). « Sauvegarder » écrit `appsettings.json` et **recharge la config à chaud**. `GitLab.Milestone` et `Export.OutputDirectory` ne sont pas exposés dans l'UI (modifiés à la main si besoin). Voir [endpoints serveur](#endpoints-du-serveur).

## Commandes

Depuis le dossier `GitLabExporter/`. Compilez une fois après chaque modif de code C# :

```powershell
dotnet build
```

### Mise à jour des données (export complet, appelle GitLab)

```powershell
# Export de la milestone configurée dans appsettings.json (GitLab.Milestone)
dotnet run --no-build

# Export de TOUT le projet (toutes milestones) → écrase issues.json avec l'ensemble
# (équivalent à "Rafraîchir → Tout le projet" dans le serveur ; préserve toutes les milestones)
dotnet run --no-build -- --fetch-all
```

L'avancement s'affiche issue par issue. Compter 2–5 min pour 250 issues, beaucoup plus pour un projet entier (plusieurs milliers d'issues = dizaines de minutes). `--fetch-all` est la commande à utiliser quand `issues.json` doit contenir tout le projet (cas courant ici).

### Mise à jour des vues HTML uniquement (rapide, hors-ligne)

Régénère les fichiers de `output/views/` à partir de `output/issues.json` sans rappeler GitLab :

```powershell
dotnet run --no-build -- --views-only
```

Quelques secondes. Utile après une modif de code dans [Views/](Views/) ou un changement dans `appsettings.json` qui ne nécessite pas de nouvelles données (ex: `Teams`). Si vous modifiez `TrackedLabels` / `TrackedTransitions`, faites un export complet pour que les nouveaux events/calculs soient présents.

### Récupérations rapides (sans re-fetch des issues)

```powershell
# Couleurs des labels du projet → output/labels.json (rapide)
dotnet run --no-build -- --fetch-labels

# Milestones du projet avec start_date / due_date → output/milestones.json
dotnet run --no-build -- --fetch-milestones
```

`labels.json` fournit les vraies couleurs GitLab des labels (utilisées par le dashboard). `milestones.json` fournit les dates de début/fin de chaque milestone (utilisées pour la moyenne hebdomadaire de vélocité et les lignes verticales début/fin de milestone dans les timelines). Ces deux fichiers sont aussi régénérés automatiquement lors d'un export complet.

### Mode serveur interactif (recommandé)

```powershell
dotnet run --no-build -- --serve
```

Démarre un mini-serveur HTTP sur **http://localhost:5050/** qui sert le dashboard interactif.

Port personnalisé : `dotnet run --no-build -- --serve --port 8080`.

Dans la page :

| Bouton | Action |
|---|---|
| **Rafraîchir** | Relance une extraction GitLab. Le select juxtaposé choisit la portée : `Tout le projet` (toutes milestones) ou une milestone précise (les autres milestones sont **conservées** dans `issues.json` — merge intelligent). |
| **Annuler** | Apparaît pendant un refresh en cours ; annule l'extraction. Le `issues.json` existant n'est pas modifié. |
| **Auto / Light / Dark** | Thème de la page (persisté dans `localStorage`). |

#### Onglets du dashboard

Un bandeau de **filtres globaux** (Milestone · Label · Équipe · Utilisateur) est partagé par tous les onglets (sauf Options) et met à jour toutes les vues en direct.

| Onglet | Contenu |
|---|---|
| **Dashboard** | Tableau pivot des KPIs (issues, approvals, poids, temps de dev/review/QA, retours, commentaires) par `Type::*`. Plus une section « Issues créées pendant la période de la milestone » (comptage *GitLab-like* par `Type::*`). |
| **Graphiques** | Vue synthétique visuelle (SVG/CSS pur, aucune librairie) : camemberts **issues ouvertes/fermées**, **poids validé/non validé**, **issues par `Type::*`**, **poids par `Type::*`** (ces deux derniers affichent le détail fermées/ouvertes — resp. validé/non validé — par ligne), **approvals** (avec/sans), barres empilées **issues par valeur de poids** (validé vert / non validé orange), barres **temps moyen par phase**. Segments triés du plus grand au plus petit. **Bouton « Exporter en HTML »** (dans le bloc de filtres, visible sur cet onglet) → fichier HTML autonome de la vue. |
| **Anomalies** | Raccourcis + listes détaillées des anomalies (sans poids, sans approval, sans assigné, multi-`Type::*`, fermées sans MR…). |
| **Issues** | Une ligne dépliable par issue avec le détail des événements de labels (tableau triable) et les MR liées. |
| **Calendrier** | Gantt horizontal : chaque issue = une ligne, ses phases en segments colorés (UI/UX, Dev, Review, QA Wait, QA, To Fix, PO Validation). Le bloc **UI/UX** regroupe tous les `Prod::UI/UX *`. Le segment **Dev** est teinté par développeur (qui a déclenché `Code In Progress`) quand plusieurs ont codé. La **légende est interactive** : clic sur une phase = trier les issues par sa date (▼/▲, basée sur la 1ʳᵉ entrée dans la phase) ; **œil** = masquer/afficher. Lignes verticales « aujourd'hui » + début/fin de milestone. Barre de contrôles + légende groupées dans une même carte. |
| **Vélocité** | Gantt par personne. Le **poids** d'une issue multi-assignés est **réparti au prorata du temps de dev** de chacun (repli : partage égal si aucun dev tracé). Barres = poids validé/semaine (segmenté par `Type::*`), bande **période de dev** (`Code In Progress`, attribuée à son auteur, même couleur/forme que le Calendrier), répartition Fibonacci, moyenne hebdo sur les semaines de la milestone. |
| **Options** | Thème, **Régénération des données** (Rafraîchir / Annuler + portée), et **Configuration** (édition de `appsettings.json` en formulaire — voir plus haut). Le bloc de filtres y est masqué. |

#### Arrêter le serveur

- **`Ctrl+C`** dans le terminal qui le fait tourner (méthode propre — annule proprement le `CancellationToken`).
- Sinon, depuis un autre terminal PowerShell :

```powershell
Get-Process GitLabExporter -ErrorAction SilentlyContinue | Stop-Process -Force
```

#### Endpoints du serveur

Le mode `--serve` expose, en plus de la page :

| Méthode | Route | Rôle |
|---|---|---|
| `GET` | `/` | Sert le dashboard HTML. |
| `GET` | `/api/status` | État du refresh en cours (progression, dernière extraction, erreur). |
| `POST` | `/api/refresh` | Lance une extraction. Body JSON optionnel `{ "milestones": [...] }` (vide = tout le projet). |
| `POST` | `/api/cancel` | Annule l'extraction en cours. |
| `GET` | `/api/config` | Renvoie `appsettings.json` (**token masqué** par `********`). |
| `POST` | `/api/config` | Écrit `appsettings.json` (token masqué → conservé ; sinon remplacé), puis **recharge la config à chaud**. Écrit aussi le fichier source à côté du `.csproj` s'il est trouvé. |
| `GET` | `/api/config/token` | Renvoie le token **en clair** — seulement sur action explicite (bouton œil de l'éditeur). |

### Cycle de travail typique

```powershell
# 1. Première fois : extraction complète
dotnet build
dotnet run --no-build

# 2. Itérer sur les vues (changer du CSS/JS dans Views/)
dotnet build
dotnet run --no-build -- --views-only

# 3. Exploration interactive avec rafraîchissements à la demande
dotnet run --no-build -- --serve
# → ouvrir http://localhost:5050/ — utiliser "Rafraîchir" pour ré-extraire
# → Ctrl+C pour stopper
```

## Fichiers générés dans `output/`

### Données brutes

| Fichier | Contenu |
|---|---|
| `issues.json` | Source de vérité riche : toutes les issues filtrées par milestone, avec leurs labels actuels, events de labels tracés (avec auteur), transitions (dates + durées), et MR liées (avec approbateurs). Format JSON indenté, ré-importable. **Long à re-fetch** — c'est la donnée à préserver. |
| `labels.json` | Couleurs GitLab des labels du projet (hex). Rapide à régénérer (`--fetch-labels`). Utilisé par le dashboard pour colorer les `Type::*`. |
| `milestones.json` | Milestones du projet avec `start_date` / `due_date`. Rapide (`--fetch-milestones`). Utilisé pour la moyenne hebdo de vélocité et les lignes verticales de milestone. |

### Vues plates (CSV, délimiteur `;`)

Encodage UTF-8 avec BOM, ouvrables directement dans Excel FR.

| Fichier | Granularité | Usage typique |
|---|---|---|
| `issues.csv` | 1 ligne / issue | Vue principale. Colonnes dynamiques `first_added_<label>`, `last_added_<label>`, `count_<From>_to_<To>`, `last_<From>_to_<To>` selon `TrackedLabels` / `TrackedTransitions`. Inclut la MR de clôture et un agrégat sur toutes les MR liées (`all_approvers`, `related_mrs_count`). |
| `issues_assignees.csv` | 1 ligne / (issue, assigné) | Pivot par personne. |
| `issues_labels.csv` | 1 ligne / (issue, label) | Pivot par label, utile pour filtrer sur n'importe quel label sans pré-déclaration. |
| `label_events.csv` | 1 ligne / event de label tracé | Date et auteur de chaque ajout/retrait d'un label déclaré dans `TrackedLabels`. |
| `transitions.csv` | 1 ligne / occurrence de transition tracée | Pour chaque issue, chaque occurrence d'une transition `From → To` configurée, avec sa date. Une issue peut apparaître plusieurs fois pour la même paire si elle a oscillé. |
| `merge_requests.csv` | 1 ligne / MR liée | Toutes les MR liées à une issue (tous statuts), avec dates création/merge/closed, auteur, reviewers, approbateurs, et le flag `is_closing`. |
| `approvers.csv` | 1 ligne / (issue, MR, approuveur) | Pivot facile par approuveur ou par MR. Inclut `mr_state` et `is_closing` pour filtrer. |

### Vues HTML

| Fichier | Contenu |
|---|---|
| `views/release_<milestone>_hypervisor.html` | **Dashboard interactif complet** (un seul fichier autonome : HTML + CSS + JS + données embarquées, aucune dépendance réseau). Contient tous les onglets décrits plus haut. Les données `issues.json` / `labels.json` / `milestones.json` sont injectées à la génération. |

Ouvrir un fichier HTML : double-clic ou via PowerShell : `Start-Process .\output\views\release_2026-R2_hypervisor.html`. Le mode `--serve` sert ce même fichier mais permet en plus le bouton « Rafraîchir ».

## Définitions importantes

### Détection d'une transition `From → To`

Une transition est comptée lorsque le label `To` est **ajouté** alors que le label `From` est **actuellement actif** sur l'issue. Pour les scoped labels (`Prod::*`), GitLab retire automatiquement le label précédent quand on ajoute un nouveau du même scope, ce qui rend la détection naturelle.

Si `From` a été retiré (manuellement ou via un autre add du même scope) **avant** l'ajout de `To`, la transition n'est pas comptée — c'est volontaire.

### Durée d'une transition

Pour chaque occurrence détectée, la durée est `date(ajout de To) - date(dernier ajout de From)`. Une issue peut donc contribuer plusieurs durées à la même paire si elle a oscillé. Les moyennes affichées dans les vues HTML utilisent toutes ces durées (et non une seule par issue) — le `n=...` indique le nombre d'occurrences agrégées.

### Phases de temps (dashboard)

Les temps par phase (Dashboard, Graphiques, Issues, Calendrier, Vélocité) sont calculés côté navigateur à partir des events de labels, selon un **mapping label → phase CONFIGURABLE**.

- **Configurez le mapping dans l'assistant `/setup`** (étape « Phases ») : tous les labels du projet sont proposés, et vous associez chacun à une phase de temps — `dev`, `review`, `qawait` (attente QA), `qa`, `tofix`, `po` — ou à `uiux` (segment Gantt only) — ou « Non suivi ». Le choix est écrit dans `Export.LabelPhases` (`{ "Nom Du Label": "dev", … }`). On peut mapper **plusieurs labels sur la même phase** (le temps s'accumule tant qu'au moins un est actif).
- **Sans config** (`Export.LabelPhases` vide), repli automatique sur les labels `Prod::*` historiques : `dev`=`Prod::Code In Progress`, `review`=`Prod::Code (pre-)review`, `qawait`=`Prod::QA (Hotfix) Backlog`, `qa`=`Prod::QA (Hotfix) InProgress`, `tofix`=`Prod::To Fix`, `po`=`Prod:: PO Validation`, `uiux`=`Prod::UI/UX *`.

⚠️ Les labels mappés à une phase doivent figurer dans `Export.TrackedLabels` (l'assistant les y ajoute automatiquement) pour que leurs events soient extraits. Après ajout/changement de mapping, un **export complet** (`--fetch-all` ou « Rafraîchir ») est nécessaire pour ré-extraire les events.

### Bloc UI/UX (Calendrier)

Tous les labels commençant par `Prod::UI/UX` (To Do, in progress, R&D, Done, Block, Review des WIP, Current/Next Release) sont regroupés en **un seul bloc** « UI/UX » sur la timeline du Calendrier (les sous-états scoped étant mutuellement exclusifs, ils forment une bande continue).

### Attribution multi-développeurs (Vélocité & Calendrier)

Chaque intervalle de `Prod::Code In Progress` est attribué à **l'auteur de l'event d'ajout** (≈ qui a réellement codé), pas dupliqué sur tous les assignés.
- **Calendrier** : le segment Dev est teinté d'une nuance de bleu par auteur (tooltip nominatif + durée).
- **Vélocité** : la bande de dev de chaque personne ne montre que ses propres intervalles ; le **poids** d'une issue est réparti entre assignés **au prorata de leur temps de dev** (repli : partage égal si aucun dev tracé). Fallback si l'auteur d'un event est inconnu/non-assigné : attribué à tous les assignés.

### Choix de la MR de clôture

Parmi les MR retournées par l'endpoint `issues/:iid/closed_by`, on choisit :
1. la plus récente **mergée**, sinon
2. la plus récente **fermée**, sinon
3. la plus récente **créée**.

## Ajouter une nouvelle vue HTML

1. Créer une classe dans `Views/` qui prend `List<IssueExport>` en entrée et écrit un fichier `.html` dans `output/views/`.
2. La brancher dans [Program.cs](Program.cs) après les exporters CSV.
3. Si la vue a besoin de transitions ou labels non encore trackés, les ajouter à `Export.TrackedLabels` / `Export.TrackedTransitions` dans `appsettings.json` (et relancer l'export, les durées ne sont calculées qu'à l'extraction).

## Déploiement (hébergement)

L'app est un service Kestrel qui écoute sur `localhost:5050`, **conçu pour tourner derrière un reverse proxy qui assure le HTTPS**. Pour l'héberger sur un domaine :

1. **Publier** : `dotnet publish -c Release -o /chemin/app`. Copier le dossier publié **+** `appsettings.json` **+** `output/` ; créer un dossier `dp-keys/` inscriptible.
2. **Lancer en service** (systemd / service Windows / conteneur) avec `--serve`. Fournir le token de service par **variable d'environnement** plutôt qu'en clair : `GITLAB_EXPORTER_GitLab__PrivateToken=...`.
3. **Reverse proxy + TLS** (Caddy = HTTPS automatique, ou nginx/IIS) vers `127.0.0.1:5050`, en transmettant `X-Forwarded-Proto/Host/For`. Le proxy doit être sur le **même hôte** (les en-têtes ne sont acceptés que depuis loopback) ; sinon ajuster `KnownProxies`.
4. **OAuth (optionnel)** : enregistrer le Redirect URI `https://<domaine>/signin-gitlab` (scope `read_user`) et renseigner `Auth.ClientId/ClientSecret`.

Checklist prod : HSTS/TLS au proxy · token de service par variable d'env (et roté) · une seule instance, sinon `dp-keys/` sur un **volume partagé** (sessions multi-instances). L'app crée et persiste ses clés de session dans `dp-keys/`.

## Sécurité

- **Aucun secret dans le dépôt** : `appsettings.json` (token), `output/` (données + comptes) et `dp-keys/` sont **gitignorés**. Seul `appsettings.example.json` (placeholders) est versionné.
- Token de service avec **scope minimal** (`read_api`) et **expiration courte** ; fourni de préférence par variable d'environnement en prod.
- Le token d'un utilisateur qui se connecte (PAT) n'est **jamais stocké** — il sert une fois à valider l'identité contre `{instance}/api/v4/user`.
- Accès verrouillé aux **membres GitLab du projet** ; admin = `Auth.AdminUsers` (fichier serveur uniquement). En-têtes durcis (CSP, HSTS si https, X-Frame-Options, anti-CSRF), rate-limit du login, garde anti-SSRF sur la connexion par token.
- Révoquer un token dans GitLab : `{votre-instance}/-/user_settings/personal_access_tokens` dès qu'il n'est plus nécessaire.

## Licence

Distribué sous licence **MIT** — voir [LICENSE](LICENSE). Vous êtes libre d'utiliser, modifier et redistribuer le projet, y compris à des fins commerciales, en conservant la notice de licence.
