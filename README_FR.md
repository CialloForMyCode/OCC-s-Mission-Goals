# OCC's Mission & Goals

Un outil de suivi des mises à jour / correctifs pour ONC Compiler Collection, conçu pour simplifier la gestion des tâches et améliorer la productivité. Double mode : GUI avec WPF et CLI produisant du JSON standard pour l'IA / les scripts / CI.

# Language

[中文 README](README_ZH.md) **|**
[README for English](README.md) **|**
[README на русском](README_RU.md) **|**
[日本語の README](README_JP.md) **|**
[한국어 README](README_KR.md) **|**
[README en français](README_FR.md) **|**

# Sommaire

- [Installation](#installation)
- [Utilisation](#utilisation)
- [Commandes CLI](#commandes-cli)
- [Architecture](#architecture)
- [Contributeurs](#contributeurs)

---

# Installation

### Prérequis

- Windows 10 / 11
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

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

Aucune dépendance NuGet tierce — pur .NET 8 + WPF, prêt à l'emploi.

---

# Utilisation

### Flux de travail

1. **Créer un projet** — Menu → Nouveau projet (`Ctrl+N`) : définir le nom, la description, la version initiale
2. **Créer une version** — Dialogue de version pour itérer le numéro (ex. `0.1.0-alpha.1` → `0.1.0-alpha.2`)
3. **Ajouter des entrées** — Barre d'outils → Nouvelle entrée : titre, sévérité, échéance, fichiers liés, etc.
4. **Suivre la progression** — Parcourir et gérer les entrées sur la page « En cours »
5. **Terminer et archiver** — Après marquage terminé, les entrées apparaissent sur la page « Terminées ». Lorsque toutes les entrées d'une version sont terminées, archiver en un clic.

### Pages

| Page | Fonction |
|------|----------|
| Tableau de bord | Graphique de distribution des sévérités, tendances récentes et aperçu du projet |
| En cours | Toutes les entrées en attente groupées par version : recherche, tri, édition, terminaison, suppression |
| Terminées | Entrées terminées groupées par version : annuler, éditer, supprimer ; archivage quand la version est entièrement terminée |
| Centre d'extensions | Gestion des plugins / extensions |
| Aide | Guide utilisateur complet : opérations de base, raccourcis, référence des champs, référence CLI |

### Options de tri

La barre d'outils inférieure propose 7 modes de tri :

| Tri | Description |
|-----|-------------|
| Sévérité croissante | Fatal → Update |
| Sévérité décroissante | Update → Fatal |
| Échéance croissante | Proche → Lointaine |
| Échéance décroissante | Lointaine → Proche |
| Version croissante | Ordre alphabétique des versions |
| Version décroissante | Ordre inverse des versions |
| Favoris uniquement | Entrées favorites seulement, triées par sévérité |

### Niveaux de sévérité

| Valeur | Signification |
|--------|---------------|
| `Fatal` | Critique — action immédiate requise |
| `Severe` | Grave — haute priorité |
| `General` | Normal — niveau par défaut |
| `Patch` | Correctif — mineur |
| `Update` | Mise à jour — fonctionnalité |

### Stockage des données

Toutes les données sont stockées dans `Projects/` à côté de l'exécutable :

```
Projects/
└── <NomDuProjet>/
    ├── project.json              # Métadonnées du projet
    └── versions/
        ├── 0.1.0-alpha.0.json    # Fichiers de version
        ├── 0.2.0-alpha.0.json
        └── archive/              # Versions archivées
```

Les IDs d'entrée utilisent le format `PPPEEEEEE` (9 chiffres) : 3 premiers pour le numéro du projet, 6 suivants auto-incrémentés.

### Double mode

L'application détecte les arguments au démarrage dans `Main` : sans arguments → **mode GUI** (fenêtre WPF) ; avec arguments → **mode CLI** (console avec sortie JSON).

---

# Commandes CLI

Le mode CLI est conçu pour l'IA / les scripts / CI. Toute sortie normale est en JSON vers stdout ; les erreurs vers stderr.

```
OCCMissionGoals.exe [-p <projet>] [-v <version>] <commande> [args]
```

### Commandes d'entrée

| Commande | Court | Long | Args | Description |
|----------|-------|------|------|-------------|
| Ajouter | `-a` | `--add` | `{Title="...", Severity="Fatal", ...}` | Ajouter une entrée en JSON ou syntaxe simplifiée `Key="Value"` |
| Vérifier | `-c` | `--check` | `<id>` | Afficher les détails complets (JSON) |
| Terminer | `-d` | `--done` | `<id>` | Marquer comme terminée |
| Rouvrir | `-u` | `--undone` | `<id>` | Remettre en cours |
| Supprimer | `-D` | `--delete` | `<id>` | Supprimer (irréversible) |
| Favori | `-f` | `--favorited` | `<id> true\|false` | Définir le statut favori |
| Lister | `-l` | `--list` | — | Lister toutes les entrées (tableau JSON) |

### Commandes de version (`-v`)

| Usage | Description |
|------|-------------|
| `-v <version>` | Basculer vers la version spécifiée |
| `-v Iterate` | Incrémenter le numéro d'itération (ex. `alpha.0` → `alpha.1`) |
| `-v Delete <version>` | Supprimer le fichier de version (impossible pour la version courante) |
| `-v Archive <version>` | Archiver la version vers `versions/archive/` (toutes les entrées doivent être terminées ; impossible pour la version courante) |

### Options globales

| Drapeau | Description |
|---------|-------------|
| `-p <nom>` / `--project <nom>` | Cibler un projet spécifique |
| `-v <version>` | Cibler une version spécifique (avec les commandes d'entrée) |
| `help` / `-h` / `--help` | Afficher l'aide |

### Format d'ajout d'entrée

```
-a {Title="CorrigerBug", Severity="Fatal", Brief="Résumé", Detail="Détails",
    IsFavorited=false, Version="0.1.0", Type=["Bug"],
    RelatedFiles={"C:\\src\\App.cs"=[25,10,"App.Init"]}}
```

Seul `Title` est obligatoire. `Severity` par défaut `General`. `Type` est un tableau de chaînes, `RelatedFiles` un mapping chemin → `[ligne, colonne, fonction]`.

### Exemples

```bash
# Lister toutes les entrées du projet "ONC"
OCCMissionGoals.exe -p ONC -l

# Ajouter un bug fatal
OCCMissionGoals.exe -a {Title="Crash NullRef", Severity="Fatal", Brief="Crash au démarrage", Version="0.1.0-alpha.0", Type=["Bug"], RelatedFiles={"C:\\src\\App.cs"=[25,10,"App.Init"]}}

# Marquer comme terminée
OCCMissionGoals.exe -d 001000001

# Changer de version et ajouter une entrée
OCCMissionGoals.exe -v 0.2.0-alpha.0 -a {Title="Ajouter connexion", Severity="Update"}
```

---

# Architecture

```
OCC-s-Mission-Goals/
├── App.xaml / .cs              # Point d'entrée : détection des arguments → GUI ou CLI
├── MainWindow.xaml / .cs       # Fenêtre principale, sans bordure + superposition floue
├── CliCommand.cs               # Analyse et exécution des commandes CLI
├── ConfigManager.cs            # Lecture / écriture de config.ini
├── ThemeManager.cs             # Basculement thème clair / sombre
├── FolderPicker.cs             # Enveloppe de sélection de dossier
├── AssemblyInfo.cs             # Métadonnées d'assemblage
│
├── Models/                     # Modèles de données
│   ├── GoalEntry.cs            # Entité d'entrée + énumération SortMode
│   ├── DataFile.cs             # Structure du fichier JSON
│   ├── ProjectConfig.cs        # Configuration du projet
│   ├── PageRegistration.cs     # Enregistrement des pages
│   └── SeverityHelper.cs       # Sévérité → texte d'affichage
│
├── Services/                   # Couche de services
│   ├── DataService.cs          # Lecture / écriture JSON + CRUD inter-versions
│   ├── ProjectService.cs       # Gestion multi-projets et versions
│   └── TipService.cs           # Génération de notifications
│
├── Pages/                      # Pages principales
│   ├── LogPage.xaml            # Tableau de bord
│   ├── UnDonePage.xaml         # Entrées en cours
│   ├── DonePage.xaml           # Entrées terminées
│   ├── ExpandPage.xaml         # Centre d'extensions
│   └── HelpPage.xaml           # Aide (avec référence CLI complète)
│
├── ToolPages/                  # Pages de la barre d'outils inférieure
│   ├── SwitchPage.xaml         # Sélecteur d'onglets
│   ├── SortPage.xaml           # Sélecteur de tri
│   ├── ControlButtonPage.xaml  # Boutons d'actions rapides
│   └── MenuPage.xaml           # Barre de menu
│
├── Dialogs/                    # Dialogues
│   ├── NewEntryDialog.xaml     # Nouvelle / Modifier une entrée
│   ├── NewProjectDialog.xaml   # Nouveau / Modifier un projet
│   └── VersionDialog.xaml      # Gestion des versions
│
├── Styles.xaml                 # Styles WPF globaux
└── ThemeBrushes.xaml           # Pinceaux de couleur du thème
```

### Stack technique

- **Runtime** : .NET 8
- **UI** : WPF (Windows Presentation Foundation)
- **Format de données** : JSON (System.Text.Json)
- **Format de configuration** : INI
- **Dépendances tierces** : Aucune

### Modèle de données

```
project.json          →  ProjectConfig (Name, Description, CurrentVersion, ProjectNumber)
versions/*.json       →  DataFile (User, Unfinished[], Finished[])
Chaque entrée         →  GoalEntry
```

Champs d'entrée :

| Champ | Type JSON | Description |
|-------|-----------|-------------|
| `Id` | `string` | ID masqué `PPPEEEEEE` (9 chiffres) |
| `Title` | `string` | Titre (seul champ obligatoire) |
| `Severity` | `string` | Fatal / Severe / General / Patch / Update |
| `Brief` | `string` | Description courte |
| `Detail` | `string` | Description détaillée |
| `Deadline` | `[année,mois,jour]` | Date d'échéance |
| `CompletedAt` | `[année,mois,jour]` | Date de complétion |
| `ChangeDemand` | `int` | Compteur de demandes de changement |
| `IsFavorited` | `bool` | Drapeau favori |
| `Version` | `string` | Chaîne de version |
| `Type` | `string[]` | Étiquettes de type (Bug, UI, Feature, etc.) |
| `RelatedFiles` | `{chemin: [ligne,colonne,fonction]}` | Références de fichiers liés |

---

# Contributeurs

| Nom | Rôle | Contribution |
|------|------|--------------|
| [OCCO](https://github.com/OCCOCCO) | Auteur | Conception, développement, maintenance |
| [Reasonix](https://github.com/Reasonix) | Assistant IA | Refonte CLI, corrections de bugs, documentation |

---

> Licence MIT.
> Dépôt : [https://github.com/CialloForMyCode/OCC-s-Mission-Goals](https://github.com/CialloForMyCode/OCC-s-Mission-Goals)
