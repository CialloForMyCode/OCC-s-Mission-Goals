<div align="center">

# OCC's Mission & Goals

Un outil de suivi des mises à jour et des corrections pour ONC Compiler Collection : il accélère la saisie des entrées et rend immédiatement visible ce qui reste à faire.
Double mode : une interface WPF pour le travail quotidien, et une CLI qui produit du JSON standard pour l'intégration avec l'IA / les scripts / la CI.

[![Platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-0078D6)](https://github.com/CialloForMyCode/OCC-s-Mission-Goals/releases/latest)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![UI](https://img.shields.io/badge/UI-WPF-5C2D91)](https://learn.microsoft.com/dotnet/desktop/wpf/)
[![License](https://img.shields.io/badge/license-GPL--2.0-blue)](LICENSE.txt)
[![Release](https://img.shields.io/github/v/release/CialloForMyCode/OCC-s-Mission-Goals?label=release&color=brightgreen)](https://github.com/CialloForMyCode/OCC-s-Mission-Goals/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/CialloForMyCode/OCC-s-Mission-Goals/total?label=downloads&color=blue)](https://github.com/CialloForMyCode/OCC-s-Mission-Goals/releases)

**Langues :** [中文](README_ZH.md) | [English](README.md) | [Русский](README_RU.md) | [日本語](README_JP.md) | [한국어](README_KR.md) | [Français](README_FR.md)

</div>

---

# Sommaire

- [Installation](#installation)
- [Utilisation](#utilisation)
- [Commandes CLI](#commandes-cli)
- [Architecture](#architecture)
- [Contributeurs](#contributeurs)
- [Licence](#licence)

---

# Installation

### Téléchargement

Les installateurs prêts à l'emploi sont publiés sur la page [Releases](https://github.com/CialloForMyCode/OCC-s-Mission-Goals/releases/latest).
Les deux sont autonomes : aucun runtime .NET n'est requis.

| Architecture | Installateur |
|--------------|--------------|
| x64 (recommandé) | `OCC-Mission-Goals-<version>-x64-setup.exe` |
| x86 | `OCC-Mission-Goals-<version>-x86-setup.exe` |

### Prérequis

| Élément | Exigence |
|---------|----------|
| Système d'exploitation | Windows 10 / 11 |
| Exécution de l'installateur | aucune (version autonome) |
| Compilation depuis les sources | [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) |

### Compilation

```bash
git clone https://github.com/CialloForMyCode/OCC-s-Mission-Goals.git
cd "OCC-s-Mission-Goals"
dotnet build
```

### Exécution

```bash
# Mode GUI
dotnet run

# Mode CLI (afficher l'aide)
dotnet run -- -h
```

### Créer un installateur

```bash
installer\build-installer.cmd
```

Nécessite [Inno Setup 6](https://jrsoftware.org/isinfo.php) ; le résultat est écrit dans `output/`.

Aucune dépendance NuGet tierce : du .NET 8 + WPF pur, utilisable immédiatement.

---

# Utilisation

### Flux de travail

1. **Créer un projet** — Menu → Nouveau projet (`Ctrl+N`) : nom, description, version initiale
2. **Créer une version** — la boîte de dialogue des versions incrémente le numéro (ex. `0.1.0-alpha.1` → `0.1.0-alpha.2`)
3. **Ajouter des entrées** — barre d'outils → Nouvelle entrée : titre, sévérité, étiquettes de type et blocs de contenu
4. **Suivre l'avancement** — parcourez et gérez les entrées sur la page « Non terminées »
5. **Terminer et archiver** — les entrées terminées passent sur la page « Terminées » ; une version peut être archivée dès que toutes ses entrées sont terminées
6. **Gérer le projet** — Paramètres → Informations du projet : modifier le nom et la description, ou supprimer le projet courant (toutes les versions et entrées sont supprimées définitivement)

### Pages

| Page | Fonction |
|------|----------|
| Tableau de bord | Répartition par sévérité, graphique d'évolution sur 30 jours, statistiques d'achèvement et vue d'ensemble du projet |
| Non terminées | Toutes les entrées en attente, groupées par version : recherche, tri, édition, achèvement, suppression |
| Terminées | Entrées terminées groupées par version : annulation, édition, suppression, archivage de la version |
| Centre d'extensions | Installation / désinstallation de packs de langue, thèmes et extensions avec barre de progression ; les éléments installés sont comparés au dépôt et mis à jour en un clic |
| Aide | Guide complet : opérations de base, raccourcis, champs, référence CLI |

### Contenu d'une entrée

Le corps d'une entrée est constitué de blocs réorganisables par glisser-déposer, et non d'un seul bloc Markdown.
Faites glisser la poignée à gauche pour réordonner, `+` ajoute un sous-bloc :

| Bloc | Description |
|------|-------------|
| Texte | Titres `#`, gras, italique, barré, listes numérotées et à puces |
| Tableau | Ligne d'en-tête et lignes libres |
| Séparateur | Trait horizontal |
| Code | Langage (facultatif) et numéros de ligne |
| Référence de fichier | Chemin + ligne / colonne / fonction |
| Liste multi-niveaux | Éléments imbriqués |
| Sous-tâche | Éléments à cocher qui déterminent le taux d'achèvement de l'entrée |

À l'enregistrement, les fichiers liés et le taux d'achèvement sont calculés automatiquement à partir de la zone de contenu.

### Options de tri

| Tri | Description |
|-----|-------------|
| Sévérité croissante | Fatal → Update |
| Sévérité décroissante | Update → Fatal |
| Version croissante | Par chaîne de version, croissant |
| Version décroissante | Par chaîne de version, décroissant |
| Type croissant | Par première étiquette de type, alphabétique |
| Favoris uniquement | Uniquement les entrées favorites, triées par sévérité |

### Niveaux de sévérité

| Valeur | Marqueur | Signification |
|--------|----------|---------------|
| `Fatal` | rouge | Critique — action immédiate requise |
| `Severe` | orange | Grave — haute priorité |
| `General` | jaune | Normal — niveau par défaut |
| `Patch` | vert | Correctif — mineur |
| `Update` | bleu | Mise à jour — fonctionnalité |

### Recherche

La zone de recherche filtre la liste courante en temps réel ; un préfixe change le mode de correspondance :

| Préfixe | Portée |
|---------|--------|
| `Text:` | Titre et résumé (mode par défaut sans préfixe) |
| `Tag:` | Étiquettes de type |
| `Setting:` | Raccourcis de paramètres (projet / thème / statistiques) ; Entrée exécute l'élément sélectionné |
| `Function:` | Commandes : nouvelle entrée, nouveau projet, ouvrir un projet |
| `File:` | Chemins des fichiers liés |
| `Date:` | Dates |
| `Plugins:` | Tous les plugins du centre d'extensions (recherche globale) |
| `Expand:` | Uniquement les plugins installés (recherche globale) |

### Stockage des données

Toutes les données sont stockées dans `Projects/`, à côté de l'exécutable :

```
Projects/
└── <NomDuProjet>/
    ├── project.json              # Métadonnées du projet
    └── versions/
        ├── 0.1.0-alpha.0.json    # Fichiers de données de version
        ├── 0.2.0-alpha.0.json
        └── archive/              # Versions archivées
```

Les identifiants d'entrée suivent le format `PPPEEEEEE` (9 chiffres) : les 3 premiers correspondent au numéro de projet, les 6 derniers sont auto-incrémentés.

### Stockage des extensions

Les packs de langue, thèmes et extensions installés depuis le centre d'extensions sont enregistrés à côté de l'exécutable :

```
Languages/            # Packs de langue (*.xaml)
Themes/               # Thèmes (*.xaml)
Expand/<nom>/         # Extensions : manifeste expand.json et XAML facultatif
```

Une extension peut remplacer des clés de ressources (couleurs, rayon des coins, épaisseur des bordures…) ou fournir des fragments de mise en page qui remplacent une partie du contenu principal.

### Double mode

Le point d'entrée `Main` examine les arguments de démarrage : sans argument il lance le **mode GUI** (fenêtre WPF), avec arguments le **mode CLI** (sortie console).
Le GUI et la CLI sont deux processus distincts qui sérialisent leurs accès aux données via un mutex inter-processus nommé ; ils peuvent donc tourner simultanément sans perdre d'écriture.

---

# Commandes CLI

Le mode CLI est conçu pour l'IA / les scripts / la CI. Par défaut la sortie est du texte lisible ; avec `--json` elle devient du JSON standard. Les erreurs vont sur stderr.

```
OCCMissionGoals.exe [options globales] <commande> [sous-commande] [arguments]
```

### Options globales

| Option | Description |
|--------|-------------|
| `-p`, `--project <nom>` | Projet cible (nom de dossier ou nom de projet) |
| `--json` | Produire du JSON au lieu du texte lisible |
| `--version <version>` | Lire une version précise sans y basculer |
| `-h`, `--help` | Afficher l'aide ; à placer après une commande pour le détail (ex. `entry --help`) |

### project — gestion des projets

| Usage | Description |
|-------|-------------|
| `project list` | Lister tous les projets |
| `project info [nom]` | Afficher les informations du projet (celui de `-p` par défaut) |

### version — gestion des versions

| Usage | Description |
|-------|-------------|
| `version list` | Lister toutes les versions (`*` marque la version courante) |
| `version current` | Afficher la version courante |
| `version switch <version>` | Basculer vers une version (mémorisé) |
| `version iterate` | Incrémenter la pré-version (`0.1.0-alpha.0` → `0.1.0-alpha.1`) |
| `version delete <version>` | Supprimer une version (la version courante est protégée) |
| `version archive <version>` | Archiver dans `versions/archive/` (toutes les entrées doivent être terminées) |

### entry — gestion des entrées

| Commande | Description |
|----------|-------------|
| `entry list` | Lister les entrées |
| `entry show <id\|index\|titre>` | Afficher une entrée (JSON) |
| `entry add` | Ajouter une entrée |
| `entry edit <id\|index\|titre>` | Modifier une entrée |
| `entry done <id\|index\|titre>` | Marquer comme terminée |
| `entry undone <id\|index\|titre>` | Annuler l'achèvement |
| `entry delete <id\|index\|titre>` | Supprimer une entrée (irréversible) |
| `entry favorite <id\|index\|titre> <true\|false>` | Définir le favori |

Syntaxe complète :

```
entry list     [--type u|f|a] [--search <mot-clé>] [--tag <étiquette>] [--favorite] [--all] [--version <version>]
entry show     <id|index|titre> [--type u|f] [--version <version>]
entry add      --title <titre> [--severity <niveau>] [--brief <résumé>] [--type <étiquette1,étiquette2>]
               [--favorite] [--version <version>]
entry edit     <id|index|titre> [--title ...] [--severity ...] [--brief ...] [--type ...]
               [--favorite|--unfavorite] [--version <version>]
```

`<id|index|titre>` accepte l'identifiant masqué (`001000001`), l'index dans la liste ou une correspondance exacte du titre.
`--type` désigne la portée dans `list` / `show` (`u` non terminées, `f` terminées, `a` toutes) et les étiquettes de type (séparées par des virgules) dans `add` / `edit`.

### tag — gestion des étiquettes

Gère les étiquettes de type du projet courant ; les modifications sont synchronisées sur les entrées de toutes les versions.

| Usage | Description |
|-------|-------------|
| `tag list` | Lister toutes les étiquettes (avec couleurs) |
| `tag add <nom> [--color <hex>]` | Créer une étiquette |
| `tag delete <nom>` | Supprimer une étiquette et la retirer de toutes les entrées |
| `tag rename <ancien> <nouveau>` | Renommer une étiquette et synchroniser toutes les entrées |

### Ancienne syntaxe

L'ancienne syntaxe à drapeaux simples reste acceptée :

```
-a/--add   -c/--check   -d/--done   -u/--undone   -D/--delete
-f/--favorited   -l/--list   -v <version|Iterate|Delete|Archive>
```

`-a` / `--add` accepte en plus l'ancienne forme JSON : `-a {Title="...", Severity="Fatal", ...}`

### Exemples

```bash
# Lister toutes les entrées du projet "ONC"
OCCMissionGoals.exe -p ONC entry list

# Ajouter un bug critique
OCCMissionGoals.exe -p ONC entry add --title "NullReferenceException au démarrage" --severity Fatal --brief "Plante au lancement" --type Bug --version 0.1.0-alpha.0

# Sortie exploitable par un script
OCCMissionGoals.exe -p ONC --json entry list

# Marquer comme terminée
OCCMissionGoals.exe -p ONC entry done 001000001

# Changer de version et ajouter une entrée
OCCMissionGoals.exe -p ONC version switch 0.2.0-alpha.0
OCCMissionGoals.exe -p ONC entry add --title "Ajouter la connexion" --severity Update

# Étiquettes
OCCMissionGoals.exe -p ONC tag add UI --color "#3D9DE8"
```

---

# Architecture

```
OCC-s-Mission-Goals/
├── App.xaml / .cs              # Point d'entrée : analyse des arguments → GUI ou CLI
├── MainWindow.xaml / .cs       # Fenêtre principale, sans bordure avec voile flouté
├── CliCommand.cs               # Analyse et exécution des commandes CLI
├── ConfigManager.cs            # Lecture / écriture de config.ini
├── LocalizationManager.cs      # Recherche dans les packs de langue, T(key)
├── ThemeManager.cs             # Bascule thème clair / sombre
├── FolderPicker.cs             # Encapsulation du sélecteur de dossier
├── AssemblyInfo.cs             # Informations d'assembly
├── Styles.xaml                 # Styles WPF globaux
│
├── Models/                     # Modèles de données
│   ├── GoalEntry.cs            # Entité d'entrée ; GoalSeverity / SortMode / SearchMode
│   ├── ContentBlock.cs         # Bloc de contenu (texte / tableau / code / fichier / liste / sous-tâche)
│   ├── DataFile.cs             # Racine du fichier de données : User + Entries
│   ├── ProjectConfig.cs        # project.json
│   ├── PageRegistration.cs     # Enregistrement des pages
│   ├── SeverityHelper.cs       # Sévérité → texte et couleur
│   ├── TypeTag.cs              # Modèle d'étiquette de type (texte + couleur)
│   ├── SearchMatcher.cs        # Correspondance des entrées selon le mode de recherche
│   ├── RelativeTime.cs         # Formulations du type « il y a quelques minutes »
│   ├── ExpandInfo.cs           # Manifeste d'extension (expand.json)
│   ├── PluginInfo.cs           # État de la carte d'extension
│   └── ColorUtil.cs            # Chaîne de couleur ↔ pinceau
│
├── Services/                   # Couche services
│   ├── DataService.cs          # Lecture / écriture des fichiers de données
│   ├── ProjectService.cs       # Gestion multi-projets et versions
│   ├── ContentBlocks.cs        # Rendu de la zone de contenu et données dérivées
│   ├── Markdown.cs             # Rendu Markdown léger
│   ├── EntryCopyFormatter.cs   # Texte produit par « copier les informations »
│   ├── FileLock.cs             # Mutex inter-processus pour les fichiers de données
│   ├── FileRefJump.cs          # Saut vers un emplacement de fichier référencé
│   ├── ExpandService.cs        # Analyse, chargement et gestion des extensions
│   ├── ExpandAnimation.cs      # Transition fluide d'ouverture / fermeture
│   ├── LanguagePackService.cs  # Téléchargement et installation des packs de langue
│   ├── ThemePackService.cs     # Téléchargement et installation des thèmes
│   ├── PluginCatalog.cs        # Catalogue partagé par le centre d'extensions et la recherche
│   ├── UpdateService.cs        # Vérification des mises à jour
│   ├── AutoStartService.cs     # Démarrage automatique (clé HKCU Run)
│   └── TipService.cs           # Textes des astuces
│
├── Pages/                      # Pages principales
│   ├── LogPage.xaml            # Tableau de bord (répartition + graphique d'évolution + statistiques)
│   ├── UnDonePage.xaml         # Entrées non terminées
│   ├── DonePage.xaml           # Entrées terminées
│   ├── ExpandPage.xaml         # Centre d'extensions
│   ├── SettingsPage.xaml       # Paramètres
│   └── HelpPage.xaml           # Aide (avec la référence CLI complète)
│
├── Controls/                   # Contrôles personnalisés
│   ├── Marquee.cs              # Défilement d'une ligne de texte
│   └── ProgressBarAnimation.cs # Remplissage fluide de la barre de progression
│
├── ToolPages/                  # Pages de la barre d'outils inférieure
│   ├── SwitchPage.xaml         # Onglets de pages
│   ├── SortPage.xaml           # Choix du tri
│   ├── ControlButtonPage.xaml  # Boutons d'action rapide
│   └── MenuPage.xaml           # Barre de menus
│
├── Dialogs/                    # Boîtes de dialogue
│   ├── NewEntryDialog.xaml     # Nouvelle entrée / édition
│   ├── BlockEditor.xaml        # Éditeur de blocs de contenu
│   ├── NewProjectDialog.xaml   # Nouveau projet / édition
│   └── VersionDialog.xaml      # Gestion des versions
│
├── Languages/                  # Packs de langue embarqués (zh, en, ja, ko, ru)
├── Themes/                     # Thèmes embarqués (Default, ItIsPinkish)
└── installer/                  # Script Inno Setup et script de compilation
```

### Stack technique

| Couche | Choix |
|--------|-------|
| Runtime | .NET 8 |
| Framework UI | WPF (Windows Presentation Foundation) |
| Format de données | JSON (System.Text.Json) |
| Format de configuration | INI |
| Installateur | Inno Setup 6 |
| Dépendances tierces | aucune |

### Modèle de données

```
project.json        → ProjectConfig (Name, Description, CurrentVersion, ProjectNumber,
                                      TypeOptions, TypeColors, NextEntryId, StatsVersions, CreatedAt)
versions/*.json     → DataFile (User, Entries[])      # non terminées / terminées selon Entry.Status
chaque entrée       → GoalEntry
```

Champs d'une entrée :

| Champ | Type JSON | Description |
|-------|-----------|-------------|
| `Id` | `string` | Identifiant masqué `PPPEEEEEE` (9 chiffres) ; référence stable de l'entrée |
| `Title` | `string` | Titre (seul champ obligatoire) |
| `Severity` | `string` | `Fatal` / `Severe` / `General` / `Patch` / `Update` |
| `Status` | `string` | `Unfinished` / `Finished` |
| `Brief` | `string` | Description courte |
| `Contents` | `ContentBlock[]` | Corps de l'entrée : blocs ordonnés (texte / tableau / séparateur / code / référence de fichier / liste multi-niveaux / sous-tâche) |
| `Progress` | `int` | Achèvement 0–100, déduit des blocs « sous-tâche » (les éléments de liste sont exclus) ; actualisé à l'enregistrement et recalculé au chargement |
| `CompletedAt` | `string` (ISO 8601) | Date d'achèvement ; vide tant que l'entrée n'est pas terminée |
| `CreatedAt` | `string` (ISO 8601) | Date de création |
| `UpdatedAt` | `string` (ISO 8601) | Date de dernière modification |
| `IsFavorited` | `bool` | Favori |
| `Type` | `string[]` | Étiquettes de type (Bug, UI, Feature, …) |
| `RelatedFiles` | `{chemin: [ligne,colonne,fonction]}` | Références de fichiers, calculées depuis la zone de contenu |

---

# Contributeurs

| Nom | Rôle | Contribution |
|-----|------|--------------|
| [I-AM-SOLO](https://github.com/CialloForMyCode) | Auteur | Conception, développement, maintenance |

---

# Licence

Projet distribué sous [GPL-2.0 License](LICENSE.txt).

Dépôt : [https://github.com/CialloForMyCode/OCC-s-Mission-Goals](https://github.com/CialloForMyCode/OCC-s-Mission-Goals)
