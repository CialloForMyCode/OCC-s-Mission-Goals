# OCC's Mission & Goals

ONC Compiler Collection 向けの更新 / 修正追跡ツール。タスク管理を効率化し、生産性を向上させます。GUI（WPF）と CLI（標準 JSON 出力）のデュアルモードで、AI / スクリプト / CI との統合に最適です。

# Language

[中文 README](README_ZH.md) **|**
[README for English](README.md) **|**
[README на русском](README_RU.md) **|**
[日本語の README](README_JP.md) **|**
[한국어 README](README_KR.md) **|**
[README en français](README_FR.md) **|**

# 目次

- [インストール](#インストール)
- [使い方](#使い方)
- [CLI コマンド](#cli-コマンド)
- [アーキテクチャ](#アーキテクチャ)
- [貢献者](#貢献者)

---

# インストール

### 要件

- Windows 10 / 11
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### ビルド

```bash
git clone https://github.com/CialloForMyCode/OCC-s-Mission-Goals.git
cd "OCC-s-Mission-Goals"
dotnet build
```

### 実行

```bash
# GUI モード
dotnet run

# CLI モード（ヘルプ表示）
dotnet run -- -h
```

サードパーティ NuGet 依存なし — 純粋な .NET 8 + WPF ですぐに使用可能。

---

# 使い方

### 基本ワークフロー

1. **プロジェクト作成** — メニュー → 新規プロジェクト (`Ctrl+N`): 名前、説明、初期バージョンを設定
2. **バージョン作成** — バージョンダイアログで番号をイテレーション（例: `0.1.0-alpha.1` → `0.1.0-alpha.2`）
3. **エントリ追加** — ツールバー → 新規エントリ: タイトル、重要度、期限、関連ファイルなどを入力
4. **進捗追跡** — 「未完了」ページでエントリを閲覧・管理
5. **完了とアーカイブ** — 完了マーク後、「完了済み」ページに表示。バージョン内の全エントリが完了したらワンクリックでアーカイブ

### ページ一覧

| ページ | 機能 |
|--------|------|
| ダッシュボード | 重要度分布チャート、最近の傾向とプロジェクト概要 |
| 未完了 | バージョン別にグループ化された全未完了エントリ：検索、ソート、編集、完了、削除 |
| 完了済み | バージョン別の完了エントリ：取消、編集、削除；全完了時にアーカイブ可能 |
| 拡張センター | プラグイン / 拡張管理 |
| ヘルプ | 完全なユーザーガイド：基本操作、ショートカット、フィールド参照、CLI リファレンス |

### ソートオプション

下部ツールバーで 7 種類のソートが可能：

| ソート | 説明 |
|--------|------|
| 重要度昇順 | Fatal → Update |
| 重要度降順 | Update → Fatal |
| 期限昇順 | 早い → 遅い |
| 期限降順 | 遅い → 早い |
| バージョン昇順 | バージョン文字列のアルファベット順 |
| バージョン降順 | バージョン文字列の逆順 |
| お気に入りのみ | お気に入りエントリのみ、重要度順 |

### 重要度レベル

| 値 | 意味 |
|----|------|
| `Fatal` | 致命的 — 最優先で即時対応が必要 |
| `Severe` | 深刻 — 高優先度 |
| `General` | 一般 — デフォルト |
| `Patch` | パッチ — 軽微な修正 |
| `Update` | 更新 — 機能アップデート |

### データ保存

すべてのデータは実行ファイルと同じ場所の `Projects/` に保存されます：

```
Projects/
└── <プロジェクト名>/
    ├── project.json              # プロジェクトメタデータ
    └── versions/
        ├── 0.1.0-alpha.0.json    # バージョンデータファイル
        ├── 0.2.0-alpha.0.json
        └── archive/              # アーカイブ済みバージョン
```

エントリ ID は `PPPEEEEEE`（9 桁）形式：先頭 3 桁がプロジェクト番号、後続 6 桁が自動採番。

### デュアルモード

`Main` で起動引数を検出：引数なし → **GUI モード**（WPF ウィンドウ）；引数あり → **CLI モード**（コンソール、JSON 出力）。

---

# CLI コマンド

CLI モードは AI / スクリプト / CI 向けに設計されています。通常出力は JSON（stdout）、エラーは stderr へ。

```
OCCMissionGoals.exe [-p <プロジェクト>] [-v <バージョン>] <コマンド> [引数]
```

### エントリコマンド

| コマンド | 短縮 | 長形式 | 引数 | 説明 |
|----------|------|--------|------|------|
| 追加 | `-a` | `--add` | `{Title="...", Severity="Fatal", ...}` | JSON または簡易 `Key="Value"` 構文でエントリを追加 |
| 確認 | `-c` | `--check` | `<id>` | エントリ詳細を表示（JSON） |
| 完了 | `-d` | `--done` | `<id>` | エントリを完了にする |
| 取消 | `-u` | `--undone` | `<id>` | 完了を取消し |
| 削除 | `-D` | `--delete` | `<id>` | エントリを削除（復元不可） |
| お気に入り | `-f` | `--favorited` | `<id> true\|false` | お気に入り状態を設定 |
| 一覧 | `-l` | `--list` | — | 全エントリを表示（JSON 配列） |

### バージョンコマンド (`-v`)

| 使用法 | 説明 |
|--------|------|
| `-v <バージョン>` | 指定バージョンに切り替え |
| `-v Iterate` | イテレーション番号を増加（例: `alpha.0` → `alpha.1`） |
| `-v Delete <バージョン>` | バージョンファイルを削除（現在のバージョンは不可） |
| `-v Archive <バージョン>` | バージョンを `versions/archive/` にアーカイブ（全エントリ完了必須、現在のバージョンは不可） |

### グローバルオプション

| フラグ | 説明 |
|--------|------|
| `-p <名前>` / `--project <名前>` | 対象プロジェクトを指定 |
| `-v <バージョン>` | 対象バージョンを指定（エントリコマンドと併用） |
| `help` / `-h` / `--help` | ヘルプを表示 |

### エントリ追加フォーマット

```
-a {Title="バグ修正", Severity="Fatal", Brief="概要", Detail="詳細",
    IsFavorited=false, Version="0.1.0", Type=["Bug"],
    RelatedFiles={"C:\\src\\App.cs"=[25,10,"App.Init"]}}
```

必須フィールドは `Title` のみ。`Severity` のデフォルトは `General`。`Type` は文字列配列、`RelatedFiles` はパス → `[行, 列, 関数]` マップ。

### 使用例

```bash
# プロジェクト "ONC" の全エントリを一覧
OCCMissionGoals.exe -p ONC -l

# 致命的なバグを追加
OCCMissionGoals.exe -a {Title="NullRefクラッシュ", Severity="Fatal", Brief="起動時にクラッシュ", Version="0.1.0-alpha.0", Type=["Bug"], RelatedFiles={"C:\\src\\App.cs"=[25,10,"App.Init"]}}

# 完了にマーク
OCCMissionGoals.exe -d 001000001

# バージョンを切り替えてエントリを追加
OCCMissionGoals.exe -v 0.2.0-alpha.0 -a {Title="ログイン追加", Severity="Update"}
```

---

# アーキテクチャ

```
OCC-s-Mission-Goals/
├── App.xaml / .cs              # エントリポイント：引数検出 → GUI または CLI
├── MainWindow.xaml / .cs       # メインウィンドウ、フレームレス + ブラーオーバーレイ
├── CliCommand.cs               # CLI パースと実行
├── ConfigManager.cs            # config.ini 読み書き
├── ThemeManager.cs             # ライト / ダークテーマ切替
├── FolderPicker.cs             # フォルダ選択ラッパー
├── AssemblyInfo.cs             # アセンブリ情報
│
├── Models/                     # データモデル
│   ├── GoalEntry.cs            # エントリエンティティ + SortMode 列挙
│   ├── DataFile.cs             # JSON データファイル構造
│   ├── ProjectConfig.cs        # プロジェクト設定
│   ├── PageRegistration.cs     # ページ登録
│   └── SeverityHelper.cs       # 重要度 → 表示テキスト
│
├── Services/                   # サービス層
│   ├── DataService.cs          # JSON 読み書き + クロスバージョン CRUD
│   ├── ProjectService.cs       # マルチプロジェクト & バージョン管理
│   └── TipService.cs           # 通知メッセージ生成
│
├── Pages/                      # メインページ
│   ├── LogPage.xaml            # ダッシュボード
│   ├── UnDonePage.xaml         # 未完了エントリ
│   ├── DonePage.xaml           # 完了エントリ
│   ├── ExpandPage.xaml         # 拡張センター
│   └── HelpPage.xaml           # ヘルプ（CLI 完全リファレンス付き）
│
├── ToolPages/                  # 下部ツールバーページ
│   ├── SwitchPage.xaml         # タブ切替
│   ├── SortPage.xaml           # ソート選択
│   ├── ControlButtonPage.xaml  # クイック操作ボタン
│   └── MenuPage.xaml           # メニューバー
│
├── Dialogs/                    # ダイアログ
│   ├── NewEntryDialog.xaml     # 新規 / 編集エントリ
│   ├── NewProjectDialog.xaml   # 新規 / 編集プロジェクト
│   └── VersionDialog.xaml      # バージョン管理
│
├── Styles.xaml                 # グローバル WPF スタイル
└── ThemeBrushes.xaml           # テーマカラーブラシ
```

### 技術スタック

- **ランタイム**: .NET 8
- **UI**: WPF (Windows Presentation Foundation)
- **データ形式**: JSON (System.Text.Json)
- **設定形式**: INI
- **サードパーティ依存**: なし

### データモデル

```
project.json          →  ProjectConfig (Name, Description, CurrentVersion, ProjectNumber)
versions/*.json       →  DataFile (User, Unfinished[], Finished[])
各エントリ             →  GoalEntry
```

エントリフィールド：

| フィールド | JSON 型 | 説明 |
|------------|---------|------|
| `Id` | `string` | 隠し ID `PPPEEEEEE`（9 桁） |
| `Title` | `string` | タイトル（必須） |
| `Severity` | `string` | Fatal / Severe / General / Patch / Update |
| `Brief` | `string` | 概要 |
| `Detail` | `string` | 詳細 |
| `Deadline` | `[年,月,日]` | 期限 |
| `CompletedAt` | `[年,月,日]` | 完了日 |
| `ChangeDemand` | `int` | 変更要求カウンター |
| `IsFavorited` | `bool` | お気に入りフラグ |
| `Version` | `string` | バージョン文字列 |
| `Type` | `string[]` | タイプタグ（Bug, UI, Feature など） |
| `RelatedFiles` | `{パス: [行,列,関数]}` | 関連ファイル参照 |

---

# 貢献者

| 名前 | 役割 | 貢献 |
|------|------|------|
| [OCCO](https://github.com/OCCOCCO) | 作者 | 設計、開発、保守 |
| [Reasonix](https://github.com/Reasonix) | AI アシスタント | CLI リファクタリング、バグ修正、ドキュメント |

---

> MIT License.
> リポジトリ: [https://github.com/CialloForMyCode/OCC-s-Mission-Goals](https://github.com/CialloForMyCode/OCC-s-Mission-Goals)
