<div align="center">

# OCC's Mission & Goals

An update / fix tracking tool for the ONC Compiler Collection, built to make entry tracking fast and progress review obvious.
Dual-mode: a WPF GUI for daily use, plus a CLI that emits standard JSON for AI / script / CI integration.

[![Platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-0078D6)](https://github.com/CialloForMyCode/OCC-s-Mission-Goals/releases/latest)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![UI](https://img.shields.io/badge/UI-WPF-5C2D91)](https://learn.microsoft.com/dotnet/desktop/wpf/)
[![License](https://img.shields.io/badge/license-GPL--2.0-blue)](LICENSE.txt)
[![Release](https://img.shields.io/github/v/release/CialloForMyCode/OCC-s-Mission-Goals?label=release&color=brightgreen)](https://github.com/CialloForMyCode/OCC-s-Mission-Goals/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/CialloForMyCode/OCC-s-Mission-Goals/total?label=downloads&color=blue)](https://github.com/CialloForMyCode/OCC-s-Mission-Goals/releases)

**Languages:** [中文](README_ZH.md) | [English](README.md) | [Русский](README_RU.md) | [日本語](README_JP.md) | [한국어](README_KR.md) | [Français](README_FR.md)

</div>

---

# Table of Contents

- [Installation](#installation)
- [Usage](#usage)
- [CLI Commands](#cli-commands)
- [Architecture](#architecture)
- [Contributors](#contributors)
- [License](#license)

---

# Installation

### Download

Prebuilt installers are published on the [Releases](https://github.com/CialloForMyCode/OCC-s-Mission-Goals/releases/latest) page.
Both are self-contained — no .NET runtime required:

| Architecture | Installer |
|--------------|-----------|
| x64 (recommended) | `OCC-Mission-Goals-<version>-x64-setup.exe` |
| x86 | `OCC-Mission-Goals-<version>-x86-setup.exe` |

### Requirements

| Item | Requirement |
|------|-------------|
| Operating system | Windows 10 / 11 |
| Runtime for the installer | none — self-contained |
| Runtime for building from source | [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) |

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

### Package an installer

```bash
installer\build-installer.cmd
```

Requires [Inno Setup 6](https://jrsoftware.org/isinfo.php); output lands in `output/`.

No third-party NuGet dependencies — pure .NET 8 + WPF, ready out of the box.

---

# Usage

### Basic Workflow

1. **Create a project** — Menu → New Project (`Ctrl+N`): set name, description, initial version
2. **Create a version** — version dialog to iterate version numbers (e.g. `0.1.0-alpha.1` → `0.1.0-alpha.2`)
3. **Add entries** — toolbar → New Entry: fill in title, severity, type tags and content blocks
4. **Track progress** — browse and manage entries on the "Unfinished" page
5. **Complete & archive** — finished entries move to the "Finished" page; archive a version once all of its entries are complete
6. **Manage the project** — Settings → Project Info: edit the name & description, or delete the current project (removes every version and entry, irreversible)

### Pages

| Page | Function |
|------|----------|
| Dashboard | Severity distribution, 30-day severity trend chart, completion statistics and project status overview |
| Unfinished | All pending entries grouped by version, with search, sort, edit, complete, delete |
| Finished | Completed entries grouped by version; undo, edit, delete; archive when the version is fully complete |
| Extension Center | Install / uninstall language packs, themes and extensions with a progress bar; installed items are checked against the repository and can be upgraded in one click |
| Help | Complete user guide: basic operations, shortcuts, field reference, CLI reference |

### Entry Content

The body of an entry is built from draggable, reorderable blocks instead of a single Markdown blob.
Drag the handle on the left to reorder, press `+` to add a sub-block:

| Block | Notes |
|-------|-------|
| Text | `#` headings, bold, italic, strikethrough, ordered and bulleted lists |
| Table | Header row plus free-form rows |
| Divider | Horizontal separator |
| Code | Optional language tag and line numbers |
| File reference | Path plus line / column / function |
| Multi-level list | Nested list items |
| Sub-task | Checkable items; these drive the entry's completion percentage |

Related files and the completion percentage are rolled up from the content area whenever the entry is saved.

### Sort Options

| Sort | Description |
|------|-------------|
| Severity Ascending | Fatal → Update |
| Severity Descending | Update → Fatal |
| Version Ascending | By version string, ascending |
| Version Descending | By version string, descending |
| Type Ascending | By first type tag, alphabetical |
| Favorites Only | Favorited entries only, sorted by severity |

### Severity Levels

| Value | Meaning | Marker |
|-------|---------|--------|
| `Fatal` | Highest priority, needs immediate action | red |
| `Severe` | High priority | orange |
| `General` | Default level | yellow |
| `Patch` | Minor fix | green |
| `Update` | Feature update | blue |

### Search

The search box filters the current list as you type; a prefix switches the match mode:

| Prefix | Scope |
|--------|-------|
| `Text:` | Title and brief (also the default when no prefix is used) |
| `Tag:` | Type tags |
| `Setting:` | Settings shortcuts (project info / theme / statistics); pressing Enter runs the selected item |
| `Function:` | Commands such as new entry, new project, open project |
| `File:` | Related file paths |
| `Date:` | Dates |
| `Plugins:` | Every plugin listed in the Extension Center (global search) |
| `Expand:` | Installed plugins only (global search) |

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

Entry IDs use the format `PPPEEEEEE` (9 digits): the first 3 digits are the project number, the last 6 auto-increment.

### Extension Storage

Language packs, themes and extensions installed from the Extension Center are stored next to the executable:

```
Languages/            # Language packs (*.xaml)
Themes/               # Themes (*.xaml)
Expand/<name>/        # Extensions: expand.json manifest plus optional XAML
```

An extension can override resource keys (colors, corner radius, border thickness…) or supply layout fragments that replace parts of the main content.

### Dual Mode

The `Main` entry point inspects the startup arguments: with no arguments it starts **GUI mode** (WPF window), with arguments it starts **CLI mode** (console output).
GUI and CLI are separate processes that serialize their data access through a named cross-process mutex, so both can run at the same time without losing writes.

---

# CLI Commands

CLI mode is designed for AI / scripts / CI. Human-readable text is the default; pass `--json` for standard JSON. Errors are written to stderr.

```
OCCMissionGoals.exe [global options] <command> [subcommand] [arguments]
```

### Global Options

| Flag | Description |
|------|-------------|
| `-p`, `--project <name>` | Target project (folder name or project name) |
| `--json` | Emit JSON instead of human-readable text |
| `--version <version>` | Read a specific version without switching to it |
| `-h`, `--help` | Print help; append it to a command (`entry --help`) for details |

### project — Project Management

| Usage | Description |
|-------|-------------|
| `project list` | List all projects |
| `project info [name]` | Show project info (the `-p` project by default) |

### version — Version Management

| Usage | Description |
|-------|-------------|
| `version list` | List all versions (`*` marks the current one) |
| `version current` | Print the current version |
| `version switch <version>` | Switch to a version (persisted) |
| `version iterate` | Increment the pre-release number (`0.1.0-alpha.0` → `0.1.0-alpha.1`) |
| `version delete <version>` | Delete a version (the current version cannot be deleted) |
| `version archive <version>` | Archive into `versions/archive/` (every entry must be finished) |

### entry — Entry Management

| Command | Description |
|---------|-------------|
| `entry list` | List entries |
| `entry show <id\|index\|title>` | Show one entry as JSON |
| `entry add` | Add an entry |
| `entry edit <id\|index\|title>` | Edit an entry |
| `entry done <id\|index\|title>` | Mark as finished |
| `entry undone <id\|index\|title>` | Revert to unfinished |
| `entry delete <id\|index\|title>` | Delete an entry (irreversible) |
| `entry favorite <id\|index\|title> <true\|false>` | Set the favorite flag |

Full syntax:

```
entry list     [--type u|f|a] [--search <keyword>] [--tag <tag>] [--favorite] [--all] [--version <version>]
entry show     <id|index|title> [--type u|f] [--version <version>]
entry add      --title <title> [--severity <level>] [--brief <brief>] [--type <tag1,tag2>]
               [--favorite] [--version <version>]
entry edit     <id|index|title> [--title ...] [--severity ...] [--brief ...] [--type ...]
               [--favorite|--unfavorite] [--version <version>]
```

`<id|index|title>` accepts the hidden ID (`001000001`), the list index, or an exact title match.
`--type` selects the scope in `list` / `show` (`u` unfinished, `f` finished, `a` all) and sets the type tags (comma-separated) in `add` / `edit`.

### tag — Tag Management

Manage the type tags of the current project; changes are synced to the entries of every version.

| Usage | Description |
|-------|-------------|
| `tag list` | List all tags (with colors) |
| `tag add <name> [--color <hex>]` | Create a tag |
| `tag delete <name>` | Delete a tag and remove it from every entry |
| `tag rename <old> <new>` | Rename a tag and sync every entry |

### Legacy Flags

The old single-flag syntax is still accepted:

```
-a/--add   -c/--check   -d/--done   -u/--undone   -D/--delete
-f/--favorited   -l/--list   -v <version|Iterate|Delete|Archive>
```

`-a` / `--add` additionally accepts the old JSON form: `-a {Title="...", Severity="Fatal", ...}`

### Examples

```bash
# List every entry of project "ONC"
OCCMissionGoals.exe -p ONC entry list

# Add a fatal bug
OCCMissionGoals.exe -p ONC entry add --title "NullReferenceException on startup" --severity Fatal --brief "Crashes at launch" --type Bug --version 0.1.0-alpha.0

# Machine-readable output
OCCMissionGoals.exe -p ONC --json entry list

# Mark as done
OCCMissionGoals.exe -p ONC entry done 001000001

# Switch version and add an entry
OCCMissionGoals.exe -p ONC version switch 0.2.0-alpha.0
OCCMissionGoals.exe -p ONC entry add --title "Add login" --severity Update

# Tag management
OCCMissionGoals.exe -p ONC tag add UI --color "#3D9DE8"
```

---

# Architecture

```
OCC-s-Mission-Goals/
├── App.xaml / .cs              # Entry point: detect arguments → GUI or CLI
├── MainWindow.xaml / .cs       # Main window, borderless with blur overlay
├── CliCommand.cs               # CLI parsing and execution
├── ConfigManager.cs            # config.ini read / write
├── LocalizationManager.cs      # Language pack lookup, T(key)
├── ThemeManager.cs             # Light / dark theme switching
├── FolderPicker.cs             # Folder picker wrapper
├── AssemblyInfo.cs             # Assembly information
├── Styles.xaml                 # Global WPF styles
│
├── Models/                     # Data models
│   ├── GoalEntry.cs            # Entry entity; GoalSeverity / SortMode / SearchMode
│   ├── ContentBlock.cs         # Content block (text / table / code / file / list / sub-task)
│   ├── DataFile.cs             # Data file root: User + Entries
│   ├── ProjectConfig.cs        # project.json
│   ├── PageRegistration.cs     # Page registration
│   ├── SeverityHelper.cs       # Severity → label and color
│   ├── TypeTag.cs              # Type tag display model (text + color)
│   ├── SearchMatcher.cs        # Per-mode entry matching
│   ├── RelativeTime.cs         # "a few minutes ago" wording
│   ├── ExpandInfo.cs           # Extension manifest (expand.json)
│   ├── PluginInfo.cs           # Extension card state
│   └── ColorUtil.cs            # Color string ↔ brush
│
├── Services/                   # Service layer
│   ├── DataService.cs          # Data file read / write
│   ├── ProjectService.cs       # Multi-project & version management
│   ├── ContentBlocks.cs        # Content area rendering and derived data
│   ├── Markdown.cs             # Lightweight Markdown renderer
│   ├── EntryCopyFormatter.cs   # Text produced by "copy info"
│   ├── FileLock.cs             # Cross-process mutex for data files
│   ├── FileRefJump.cs          # Jump to a referenced file position
│   ├── ExpandService.cs        # Scan / load / manage extensions
│   ├── ExpandAnimation.cs      # Smooth expand / collapse transition
│   ├── LanguagePackService.cs  # Language pack download / install
│   ├── ThemePackService.cs     # Theme download / install
│   ├── PluginCatalog.cs        # Catalog shared by the Extension Center and search
│   ├── UpdateService.cs        # Update check
│   ├── AutoStartService.cs     # Run at startup (HKCU Run key)
│   └── TipService.cs           # Tip wording
│
├── Pages/                      # Main pages
│   ├── LogPage.xaml            # Dashboard (distribution + trend chart + statistics)
│   ├── UnDonePage.xaml         # Unfinished entries
│   ├── DonePage.xaml           # Finished entries
│   ├── ExpandPage.xaml         # Extension Center
│   ├── SettingsPage.xaml       # Settings
│   └── HelpPage.xaml           # Help (with the full CLI reference)
│
├── Controls/                   # Custom controls
│   ├── Marquee.cs              # Single-line text marquee
│   └── ProgressBarAnimation.cs # Smooth progress bar fill
│
├── ToolPages/                  # Bottom toolbar pages
│   ├── SwitchPage.xaml         # Page tabs
│   ├── SortPage.xaml           # Sort selection
│   ├── ControlButtonPage.xaml  # Quick action buttons
│   └── MenuPage.xaml           # Menu bar
│
├── Dialogs/                    # Dialogs
│   ├── NewEntryDialog.xaml     # New / edit entry
│   ├── BlockEditor.xaml        # Content block editor
│   ├── NewProjectDialog.xaml   # New / edit project
│   └── VersionDialog.xaml      # Version management
│
├── Languages/                  # Bundled language packs (zh, en, ja, ko, ru)
├── Themes/                     # Bundled themes (Default, ItIsPinkish)
└── installer/                  # Inno Setup script and build script
```

### Tech Stack

| Layer | Choice |
|-------|--------|
| Runtime | .NET 8 |
| UI framework | WPF (Windows Presentation Foundation) |
| Data format | JSON (System.Text.Json) |
| Config format | INI |
| Installer | Inno Setup 6 |
| Third-party dependencies | none |

### Data Model

```
project.json        → ProjectConfig (Name, Description, CurrentVersion, ProjectNumber,
                                      TypeOptions, TypeColors, NextEntryId, StatsVersions, CreatedAt)
versions/*.json     → DataFile (User, Entries[])      # unfinished / finished split by Entry.Status
each entry          → GoalEntry
```

Entry fields:

| Field | JSON type | Description |
|-------|-----------|-------------|
| `Id` | `string` | Hidden ID `PPPEEEEEE` (9 digits); the stable reference for an entry |
| `Title` | `string` | Title (the only required field) |
| `Severity` | `string` | `Fatal` / `Severe` / `General` / `Patch` / `Update` |
| `Status` | `string` | `Unfinished` / `Finished` |
| `Brief` | `string` | Short description |
| `Contents` | `ContentBlock[]` | Entry body: ordered blocks (text / table / divider / code / file reference / multi-level list / sub-task) |
| `Progress` | `int` | Completion 0–100, derived from the sub-task blocks (list items are excluded); refreshed on save and recomputed on load |
| `CompletedAt` | `string` (ISO 8601) | Completion time; empty until finished |
| `CreatedAt` | `string` (ISO 8601) | Creation time |
| `UpdatedAt` | `string` (ISO 8601) | Last modified time |
| `IsFavorited` | `bool` | Favorite flag |
| `Type` | `string[]` | Type tags (Bug, UI, Feature, …) |
| `RelatedFiles` | `{path: [line,col,func]}` | Linked file references, rolled up from the content area |

---

# Contributors

| Name | Role | Contribution |
|------|------|--------------|
| [I-AM-SOLO](https://github.com/CialloForMyCode) | Author | Design, development, maintenance |

---

# License

Released under the [GPL-2.0 License](LICENSE.txt).

Repo: [https://github.com/CialloForMyCode/OCC-s-Mission-Goals](https://github.com/CialloForMyCode/OCC-s-Mission-Goals)
