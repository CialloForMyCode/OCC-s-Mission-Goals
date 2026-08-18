# OCC's Mission & Goals

An update / fix tracking tool for the ONC Compiler Collection, designed to streamline entry tracking and boost productivity. Dual-mode: GUI with WPF and CLI that outputs standard JSON for AI / script / CI integration.

# Language

[中文 README](README_ZH.md) **|**
[README for English](README.md) **|**
[README на русском](README_RU.md) **|**
[日本語の README](README_JP.md) **|**
[한국어 README](README_KR.md) **|**
[README en français](README_FR.md) **|**

# Table of Contents

- [Installation](#installation)
- [Usage](#usage)
- [CLI Commands](#cli-commands)
- [Architecture](#architecture)
- [Contributors](#contributors)

---

# Installation

### Requirements

- Windows 10 / 11
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Build

```bash
git clone https://github.com/CialloForMyCode/OCC-s-Mission-Goals.git
cd "OCC-s-Mission-Goals"
dotnet build
```

### Run

```bash
# GUI mode
dotnet run

# CLI mode (show help)
dotnet run -- -h
```

No third-party NuGet dependencies — pure .NET 8 + WPF, ready out of the box.

---

# Usage

### Basic Workflow

1. **Create a project** — Menu → New Project (`Ctrl+N`): set name, description, initial version
2. **Create a version** — Version dialog to iterate version numbers (e.g. `0.1.0-alpha.1` → `0.1.0-alpha.2`)
3. **Add entries** — Toolbar → New Entry: fill in title, severity, deadline, related files, etc.
4. **Track progress** — Browse and manage entries in the "Unfinished" page
5. **Complete & Archive** — After marking done, entries appear in the "Finished" page. When all entries in a version are complete, archive with one click.

### Pages

| Page | Function |
|------|----------|
| Dashboard | Severity distribution chart, recent trends & project status overview |
| Unfinished | All pending entries grouped by version, with search, sort, edit, complete, delete |
| Finished | Completed entries grouped by version; undo, edit, delete; archive when version is fully complete |
| Extension Center | Plugin / extension management |
| Help | Complete user guide: basic operations, shortcuts, field reference, CLI reference |

### Sort Options

The bottom toolbar offers 7 sort modes:

| Sort | Description |
|------|-------------|
| Severity Ascending | Fatal → Update |
| Severity Descending | Update → Fatal |
| Deadline Ascending | Earliest → Latest |
| Deadline Descending | Latest → Earliest |
| Version Ascending | By version string alphabetical |
| Version Descending | By version string reverse |
| Favorites Only | Only favorited entries, sorted by severity |

### Severity Levels

| Value | Meaning |
|------|---------|
| `Fatal` | Highest priority, needs immediate action |
| `Severe` | High priority |
| `General` | Default level |
| `Patch` | Minor fix |
| `Update` | Feature update |

### Data Storage

All data is stored in `Projects/` next to the executable:

```
Projects/
└── <ProjectName>/
    ├── project.json              # Project metadata
    └── versions/
        ├── 0.1.0-alpha.0.json    # Version data files
        ├── 0.2.0-alpha.0.json
        └── archive/              # Archived versions
```

Entry IDs use the format `PPPEEEEEE` (9 digits): first 3 for the project number, last 6 auto-incremented.

### Extension Storage

Extensions installed from the Extension Center are stored next to the executable in two directories:

```
Languages/            # Language packs (one *.xaml per UI language)
Expand/               # Extension plugins
```

- **Language packs** are downloaded and installed into `Languages/`, where `LocalizationManager` auto-loads them on startup.
- **Extension plugins** are downloaded and installed into `Expand/`; both directories are created automatically as needed and are shipped with the published app.

### Dual Mode

The app checks startup arguments in `Main`: no arguments launches **GUI mode** (WPF window); arguments launch **CLI mode** (console with JSON output).

---

# CLI Commands

CLI mode is designed for AI / scripts / CI. All normal output is JSON to stdout; errors go to stderr.

```
OCCMissionGoals.exe [-p <project>] [-v <version>] <command> [args]
```

### Entry Commands

| Command | Short | Long | Args | Description |
|---------|-------|------|------|-------------|
| Add | `-a` | `--add` | `{Title="...", Severity="Fatal", ...}` | Add an entry with JSON or simplified `Key="Value"` syntax |
| Check | `-c` | `--check` | `<id>` | View full entry details (JSON) |
| Done | `-d` | `--done` | `<id>` | Mark entry as finished |
| Undone | `-u` | `--undone` | `<id>` | Revert finished to unfinished |
| Delete | `-D` | `--delete` | `<id>` | Delete entry (irreversible) |
| Favorite | `-f` | `--favorited` | `<id> true\|false` | Set favorite status |
| List | `-l` | `--list` | — | List all entries (JSON array) |

### Version Commands (`-v`)

| Usage | Description |
|------|-------------|
| `-v <version>` | Switch to a specific version |
| `-v Iterate` | Bump iteration number (e.g. `alpha.0` → `alpha.1`) |
| `-v Delete <version>` | Delete a version file (cannot delete current version) |
| `-v Archive <version>` | Archive a version to `versions/archive/` (requires all entries finished; cannot archive current version) |

### Global Options

| Flag | Description |
|------|-------------|
| `-p <name>` / `--project <name>` | Target a specific project |
| `-v <version>` | Target a specific version (used with entry commands) |
| `help` / `-h` / `--help` | Print help |

### Add Entry Format

```
-a {Title="FixBug", Severity="Fatal", Brief="Short desc", Detail="Long desc",
    IsFavorited=false, Version="0.1.0", Type=["Bug"],
    RelatedFiles={"P:\\auth.cs"=[10,5,"Login"]}}
```

Only `Title` is required. `Severity` defaults to `General`. `Type` is a string array, `RelatedFiles` a path → `[line, col, function]` map.

### Examples

```bash
# List all entries in project "ONC"
OCCMissionGoals.exe -p ONC -l

# Add a fatal bug
OCCMissionGoals.exe -a {Title="NullRef crash", Severity="Fatal", Brief="Crash on startup", Version="0.1.0-alpha.0", Type=["Bug"], RelatedFiles={"C:\\src\\App.cs"=[25,10,"App.Init"]}}

# Mark as done
OCCMissionGoals.exe -d 001000001

# Switch version and add an entry
OCCMissionGoals.exe -v 0.2.0-alpha.0 -a {Title="Add login", Severity="Update"}
```

---

# Architecture

```
OCC-s-Mission-Goals/
├── App.xaml / .cs              # Entry point: detects args → GUI or CLI
├── MainWindow.xaml / .cs       # Main window, custom frameless + blur overlay
├── CliCommand.cs               # CLI parsing & dispatch
├── ConfigManager.cs            # config.ini read / write
├── ThemeManager.cs             # Light / dark theme switching
├── FolderPicker.cs             # Folder picker wrapper
├── AssemblyInfo.cs             # Assembly metadata
│
├── Models/                     # Data models
│   ├── GoalEntry.cs            # Entry entity + SortMode enum
│   ├── DataFile.cs             # JSON data-file structure
│   ├── ProjectConfig.cs        # Project configuration
│   ├── PageRegistration.cs     # Page registration
│   └── SeverityHelper.cs       # Severity → display text
│
├── Services/                   # Service layer
│   ├── DataService.cs          # JSON read/write + cross-version CRUD
│   ├── ProjectService.cs       # Multi-project & version management
│   └── TipService.cs           # Toast message generation
│
├── Pages/                      # Main pages
│   ├── LogPage.xaml            # Dashboard
│   ├── UnDonePage.xaml         # Unfinished entries
│   ├── DonePage.xaml           # Finished entries
│   ├── ExpandPage.xaml         # Extension Center
│   └── HelpPage.xaml           # Help (with full CLI reference)
│
├── ToolPages/                  # Bottom toolbar pages
│   ├── SwitchPage.xaml         # Tab switcher
│   ├── SortPage.xaml           # Sort selector
│   ├── ControlButtonPage.xaml  # Quick-action buttons
│   └── MenuPage.xaml           # Menu bar
│
├── Dialogs/                    # Dialogs
│   ├── NewEntryDialog.xaml     # New / Edit entry
│   ├── NewProjectDialog.xaml   # New / Edit project
│   └── VersionDialog.xaml      # Version management
│
├── Styles.xaml                 # Global WPF styles
├── ThemeBrushes.xaml           # Theme colour brushes
│
├── Languages/                  # Language packs (*.xaml UI translations)
└── Expand/                     # Extension plugins
```

### Tech Stack

- **Runtime**: .NET 8
- **UI**: WPF (Windows Presentation Foundation)
- **Data format**: JSON (System.Text.Json)
- **Config format**: INI
- **Third-party deps**: None

### Data Model

```
project.json          →  ProjectConfig (Name, Description, CurrentVersion, ProjectNumber)
versions/*.json       →  DataFile (User, Unfinished[], Finished[])
Each entry            →  GoalEntry
```

Entry fields:

| Field | JSON Type | Description |
|-------|-----------|-------------|
| `Id` | `string` | Hidden ID `PPPEEEEEE` (9 digits) |
| `Title` | `string` | Title (only required field) |
| `Severity` | `string` | Fatal / Severe / General / Patch / Update |
| `Brief` | `string` | Short description |
| `Detail` | `string` | Full description |
| `Deadline` | `[year,month,day]` | Due date |
| `CompletedAt` | `[year,month,day]` | Completion date |
| `ChangeDemand` | `int` | Change demand counter |
| `IsFavorited` | `bool` | Favorite flag |
| `Version` | `string` | Version string |
| `Type` | `string[]` | Type tags (Bug, UI, Feature, etc.) |
| `RelatedFiles` | `{path: [line,col,func]}` | Linked file references |

---

# Contributors

| Name | Role | Contribution |
|------|------|--------------|
| [I-AM-SOLO](https://github.com/CialloForMyCode) | Author | Design, development, maintenance |

---

> GPL-2.0 License.
> Repo: [https://github.com/CialloForMyCode/OCC-s-Mission-Goals](https://github.com/CialloForMyCode/OCC-s-Mission-Goals)
