<div align="center">

# OCC's Mission & Goals

ONC Compiler Collection 向けの更新・修正管理ツールです。エントリの記録を素早く行い、何が未完了かを一目で把握できるようにします。
デュアルモード：日常作業は WPF の GUI、AI / スクリプト / CI 連携には標準 JSON を出力する CLI を使えます。

[![Platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-0078D6)](https://github.com/CialloForMyCode/OCC-s-Mission-Goals/releases/latest)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![UI](https://img.shields.io/badge/UI-WPF-5C2D91)](https://learn.microsoft.com/dotnet/desktop/wpf/)
[![License](https://img.shields.io/badge/license-GPL--2.0-blue)](LICENSE.txt)
[![Release](https://img.shields.io/github/v/release/CialloForMyCode/OCC-s-Mission-Goals?label=release&color=brightgreen)](https://github.com/CialloForMyCode/OCC-s-Mission-Goals/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/CialloForMyCode/OCC-s-Mission-Goals/total?label=downloads&color=blue)](https://github.com/CialloForMyCode/OCC-s-Mission-Goals/releases)

**言語：** [中文](README_ZH.md) | [English](README.md) | [Русский](README_RU.md) | [日本語](README_JP.md) | [한국어](README_KR.md) | [Français](README_FR.md)

</div>

---

# 目次

- [インストール](#インストール)
- [使い方](#使い方)
- [CLI コマンド](#cli-コマンド)
- [アーキテクチャ](#アーキテクチャ)
- [貢献者](#貢献者)
- [ライセンス](#ライセンス)

---

# インストール

### ダウンロード

ビルド済みインストーラーは [Releases](https://github.com/CialloForMyCode/OCC-s-Mission-Goals/releases/latest) ページで公開しています。
いずれも自己完結型で、.NET ランタイムのインストールは不要です：

| アーキテクチャ | インストーラー |
|----------------|----------------|
| x64（推奨） | `OCC-Mission-Goals-<バージョン>-x64-setup.exe` |
| x86 | `OCC-Mission-Goals-<バージョン>-x86-setup.exe` |

### 要件

| 項目 | 要件 |
|------|------|
| OS | Windows 10 / 11 |
| インストーラーの実行 | 不要（自己完結型） |
| ソースからのビルド | [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) |

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

### インストーラーの作成

```bash
installer\build-installer.cmd
```

[Inno Setup 6](https://jrsoftware.org/isinfo.php) が必要です。出力先は `output/` です。

サードパーティ製 NuGet 依存は一切ありません。純粋な .NET 8 + WPF で、すぐに使えます。

---

# 使い方

### 基本ワークフロー

1. **プロジェクトを作成** — メニュー → 新規プロジェクト（`Ctrl+N`）：名前、説明、初期バージョンを設定
2. **バージョンを作成** — バージョンダイアログで番号を反復（例：`0.1.0-alpha.1` → `0.1.0-alpha.2`）
3. **エントリを追加** — ツールバー → 新規エントリ：タイトル、重要度、タイプタグ、内容ブロックを入力
4. **進捗を確認** — 「未完了」ページでエントリを閲覧・操作
5. **完了とアーカイブ** — 完了したエントリは「完了済み」ページへ移動。バージョン内の全エントリが完了したらワンクリックでアーカイブ
6. **プロジェクト管理** — 設定 → プロジェクト情報：名前と説明の変更、または現在のプロジェクトの削除（全バージョンとエントリが完全に削除され、元に戻せません）

### ページ一覧

| ページ | 機能 |
|--------|------|
| ダッシュボード | 重要度の分布、直近 30 日の重要度推移グラフ、完了状況の統計とプロジェクト全体の概要 |
| 未完了 | バージョンごとに未完了エントリを表示。検索、並べ替え、編集、完了、削除が可能 |
| 完了済み | バージョンごとに完了エントリを表示。完了の取り消し、編集、削除、全完了後のアーカイブが可能 |
| 拡張センター | 言語パック、テーマ、拡張のインストール／アンインストールを進行状況バー付きで実行。インストール済みの項目はリポジトリと比較され、ワンクリックで更新できます |
| ヘルプ | 完全なユーザーガイド：基本操作、ショートカット、フィールドの意味、CLI リファレンス |

### エントリの内容

エントリの本文は 1 つの Markdown ではなく、ドラッグで並べ替えられるブロックの集まりです。
左側のハンドルをドラッグして並べ替え、`+` で子ブロックを追加します：

| ブロック | 説明 |
|----------|------|
| テキスト | `#` 見出し、太字、斜体、取り消し線、番号付き／箇条書きリスト |
| 表 | ヘッダー行 + 任意の行 |
| 区切り線 | 水平線 |
| コード | 言語指定（任意）と行番号 |
| ファイル参照 | パス + 行 / 列 / 関数 |
| 多階層リスト | 入れ子のリスト項目 |
| サブタスク | チェック項目。エントリの完成度を決定します |

エントリの保存時に、関連ファイルと完成度は内容ブロックから自動的に集計されます。

### ソートオプション

| ソート | 説明 |
|--------|------|
| 重要度 昇順 | Fatal → Update |
| 重要度 降順 | Update → Fatal |
| バージョン 昇順 | バージョン文字列の昇順 |
| バージョン 降順 | バージョン文字列の降順 |
| 種別 昇順 | 最初のタイプタグのアルファベット順 |
| お気に入りのみ | お気に入りエントリのみ、重要度順 |

### 重要度レベル

| 値 | マーク | 意味 |
|----|--------|------|
| `Fatal` | 赤 | 致命的 — 最優先で即時対応が必要 |
| `Severe` | 橙 | 深刻 — 高優先度 |
| `General` | 黄 | 一般 — デフォルト |
| `Patch` | 緑 | パッチ — 軽微な修正 |
| `Update` | 青 | 更新 — 機能アップデート |

### 検索

上部の検索ボックスに入力すると現在のリストをリアルタイムで絞り込みます。プレフィックスで一致範囲を切り替えます：

| プレフィックス | 一致範囲 |
|----------------|----------|
| `Text:` | タイトルと概要（プレフィックスなしの場合も同じ） |
| `Tag:` | タイプタグ |
| `Setting:` | 設定ショートカット（プロジェクト情報 / テーマ / 統計）。Enter で選択項目を実行 |
| `Function:` | 機能コマンド（新規エントリ、新規プロジェクト、プロジェクトを開くなど） |
| `File:` | 関連ファイルのパス |
| `Date:` | 日付 |
| `Plugins:` | 拡張センターの全プラグイン（グローバル検索） |
| `Expand:` | インストール済みプラグインのみ（グローバル検索） |

### データ保存

すべてのデータは実行ファイルと同じ階層の `Projects/` に保存されます：

```
Projects/
└── <プロジェクト名>/
    ├── project.json              # プロジェクトのメタデータ
    └── versions/
        ├── 0.1.0-alpha.0.json    # バージョンデータファイル
        ├── 0.2.0-alpha.0.json
        └── archive/              # アーカイブ済みバージョン
```

エントリ番号の形式は `PPPEEEEEE`（9 桁）：先頭 3 桁がプロジェクト番号、後ろ 6 桁が自動採番のエントリ番号です。

### 拡張の保存

拡張センターからインストールした言語パック、テーマ、拡張は実行ファイルと同じ階層に保存されます：

```
Languages/            # 言語パック（*.xaml）
Themes/               # テーマ（*.xaml）
Expand/<名前>/        # 拡張：expand.json マニフェスト + 任意の XAML
```

拡張はリソースキー（色、角丸、枠線の太さなど）を上書きでき、レイアウト断片を提供してメインコンテンツの一部を置き換えることもできます。

### デュアルモード

`Main` エントリポイントが起動引数を判定します。引数なしは **GUI モード**（WPF ウィンドウ）、引数ありは **CLI モード**（コンソール出力）で起動します。
GUI と CLI は別プロセスで、名前付きミューテックスでデータアクセスを直列化するため、同時に動かしても書き込みが失われません。

---

# CLI コマンド

CLI モードは AI / スクリプト / CI 向けです。既定では人間が読めるテキストを出力し、`--json` を付けると標準 JSON を出力します。エラーは stderr に出力します。

```
OCCMissionGoals.exe [グローバルオプション] <コマンド> [サブコマンド] [引数]
```

### グローバルオプション

| フラグ | 説明 |
|--------|------|
| `-p`, `--project <名前>` | 対象プロジェクト（フォルダー名またはプロジェクト名） |
| `--json` | 人間が読めるテキストではなく JSON を出力 |
| `--version <バージョン>` | 切り替えずに指定バージョンを読む |
| `-h`, `--help` | ヘルプを表示。コマンドの後ろに付けると詳細（例：`entry --help`） |

### project — プロジェクト管理

| 用法 | 説明 |
|------|------|
| `project list` | すべてのプロジェクトを一覧 |
| `project info [名前]` | プロジェクト情報を表示（既定は `-p` のプロジェクト） |

### version — バージョン管理

| 用法 | 説明 |
|------|------|
| `version list` | すべてのバージョンを一覧（`*` が現在のバージョン） |
| `version current` | 現在のバージョン番号を表示 |
| `version switch <バージョン>` | 指定バージョンに切り替え（保存されます） |
| `version iterate` | 現在のバージョンを反復（プレリリース番号 +1、例：`0.1.0-alpha.0` → `0.1.0-alpha.1`） |
| `version delete <バージョン>` | バージョンを削除（現在のバージョンは削除不可） |
| `version archive <バージョン>` | `versions/archive/` へアーカイブ（全エントリが完了している必要があります） |

### entry — エントリ管理

| コマンド | 説明 |
|----------|------|
| `entry list` | エントリを一覧 |
| `entry show <番号\|索引\|タイトル>` | 1 件のエントリを表示（JSON） |
| `entry add` | エントリを追加 |
| `entry edit <番号\|索引\|タイトル>` | エントリを編集 |
| `entry done <番号\|索引\|タイトル>` | 完了にする |
| `entry undone <番号\|索引\|タイトル>` | 完了を取り消す |
| `entry delete <番号\|索引\|タイトル>` | エントリを削除（元に戻せません） |
| `entry favorite <番号\|索引\|タイトル> <true\|false>` | お気に入りを設定 |

完全な構文：

```
entry list     [--type u|f|a] [--search <キーワード>] [--tag <タグ>] [--favorite] [--all] [--version <バージョン>]
entry show     <番号|索引|タイトル> [--type u|f] [--version <バージョン>]
entry add      --title <タイトル> [--severity <レベル>] [--brief <概要>] [--type <タグ1,タグ2>]
               [--favorite] [--version <バージョン>]
entry edit     <番号|索引|タイトル> [--title ...] [--severity ...] [--brief ...] [--type ...]
               [--favorite|--unfavorite] [--version <バージョン>]
```

`<番号|索引|タイトル>` には隠し番号（例：`001000001`）、リストの索引、タイトルの完全一致が使えます。
`--type` は `list` / `show` では範囲（`u` 未完了、`f` 完了済み、`a` すべて）、`add` / `edit` ではタイプタグ（カンマ区切り）を指定します。

### tag — タグ管理

現在のプロジェクトのタイプタグを管理します。変更はすべてのバージョンのエントリに同期されます。

| 用法 | 説明 |
|------|------|
| `tag list` | すべてのタグを一覧（色付き） |
| `tag add <名前> [--color <hex>]` | タグを作成 |
| `tag delete <名前>` | タグを削除し、すべてのエントリからも除去 |
| `tag rename <旧名> <新名>` | タグを改名し、すべてのエントリを同期 |

### 旧記法

従来の単一フラグ記法も引き続き使用できます：

```
-a/--add   -c/--check   -d/--done   -u/--undone   -D/--delete
-f/--favorited   -l/--list   -v <バージョン|Iterate|Delete|Archive>
```

`-a` / `--add` は従来の JSON 形式も受け付けます：`-a {Title="...", Severity="Fatal", ...}`

### 使用例

```bash
# プロジェクト "ONC" の全エントリを一覧
OCCMissionGoals.exe -p ONC entry list

# 致命的なバグを追加
OCCMissionGoals.exe -p ONC entry add --title "起動時の NullReferenceException" --severity Fatal --brief "起動時にクラッシュ" --type Bug --version 0.1.0-alpha.0

# スクリプト向けに JSON を出力
OCCMissionGoals.exe -p ONC --json entry list

# 完了にマーク
OCCMissionGoals.exe -p ONC entry done 001000001

# バージョンを切り替えてエントリを追加
OCCMissionGoals.exe -p ONC version switch 0.2.0-alpha.0
OCCMissionGoals.exe -p ONC entry add --title "ログインを追加" --severity Update

# タグ管理
OCCMissionGoals.exe -p ONC tag add UI --color "#3D9DE8"
```

---

# アーキテクチャ

```
OCC-s-Mission-Goals/
├── App.xaml / .cs              # エントリポイント：引数を判定 → GUI または CLI
├── MainWindow.xaml / .cs       # メインウィンドウ（枠なし + ぼかしオーバーレイ）
├── CliCommand.cs               # CLI の解析と実行
├── ConfigManager.cs            # config.ini の読み書き
├── LocalizationManager.cs      # 言語パックの検索、T(key)
├── ThemeManager.cs             # ライト / ダークテーマの切り替え
├── FolderPicker.cs             # フォルダー選択のラッパー
├── AssemblyInfo.cs             # アセンブリ情報
├── Styles.xaml                 # グローバル WPF スタイル
│
├── Models/                     # データモデル
│   ├── GoalEntry.cs            # エントリ実体；GoalSeverity / SortMode / SearchMode
│   ├── ContentBlock.cs         # 内容ブロック（テキスト / 表 / コード / ファイル / リスト / サブタスク）
│   ├── DataFile.cs             # データファイルのルート：User + Entries
│   ├── ProjectConfig.cs        # project.json
│   ├── PageRegistration.cs     # ページ登録
│   ├── SeverityHelper.cs       # 重要度 → 表示文字と色
│   ├── TypeTag.cs              # タイプタグ表示モデル（文字 + 色）
│   ├── SearchMatcher.cs        # 検索モードごとのエントリ照合
│   ├── RelativeTime.cs         # 「数分前」などの相対時間表現
│   ├── ExpandInfo.cs           # 拡張マニフェスト（expand.json）
│   ├── PluginInfo.cs           # 拡張カードの状態
│   └── ColorUtil.cs            # 色文字列 ↔ ブラシ
│
├── Services/                   # サービス層
│   ├── DataService.cs          # データファイルの読み書き
│   ├── ProjectService.cs       # 複数プロジェクトとバージョン管理
│   ├── ContentBlocks.cs        # 内容ブロックの描画と派生データ
│   ├── Markdown.cs             # 軽量 Markdown レンダラー
│   ├── EntryCopyFormatter.cs   # 「情報をコピー」が生成するテキスト
│   ├── FileLock.cs             # データファイル用のプロセス間ミューテックス
│   ├── FileRefJump.cs          # 関連ファイルの位置へジャンプ
│   ├── ExpandService.cs        # 拡張の探索・読み込み・管理
│   ├── ExpandAnimation.cs      # 展開 / 折りたたみのスムーズな遷移
│   ├── LanguagePackService.cs  # 言語パックのダウンロードとインストール
│   ├── ThemePackService.cs     # テーマのダウンロードとインストール
│   ├── PluginCatalog.cs        # 拡張センターと検索が共有するカタログ
│   ├── UpdateService.cs        # 更新チェック
│   ├── AutoStartService.cs     # スタートアップ登録（HKCU Run キー）
│   └── TipService.cs           # 操作ヒントの文言
│
├── Pages/                      # メインページ
│   ├── LogPage.xaml            # ダッシュボード（分布 + 推移グラフ + 統計）
│   ├── UnDonePage.xaml         # 未完了エントリ
│   ├── DonePage.xaml           # 完了済みエントリ
│   ├── ExpandPage.xaml         # 拡張センター
│   ├── SettingsPage.xaml       # 設定
│   └── HelpPage.xaml           # ヘルプ（完全な CLI リファレンス付き）
│
├── Controls/                   # カスタムコントロール
│   ├── Marquee.cs              # 1 行テキストのマーキー
│   └── ProgressBarAnimation.cs # 進行状況バーの滑らかな伸び
│
├── ToolPages/                  # 下部ツールバーのページ
│   ├── SwitchPage.xaml         # ページ切り替え
│   ├── SortPage.xaml           # ソート選択
│   ├── ControlButtonPage.xaml  # クイック操作ボタン
│   └── MenuPage.xaml           # メニューバー
│
├── Dialogs/                    # ダイアログ
│   ├── NewEntryDialog.xaml     # エントリの新規作成 / 編集
│   ├── BlockEditor.xaml        # 内容ブロックエディター
│   ├── NewProjectDialog.xaml   # プロジェクトの新規作成 / 編集
│   └── VersionDialog.xaml      # バージョン管理
│
├── Languages/                  # 同梱の言語パック（zh、en、ja、ko、ru）
├── Themes/                     # 同梱のテーマ（Default、ItIsPinkish）
└── installer/                  # Inno Setup スクリプトとビルドスクリプト
```

### 技術スタック

| 層 | 採用技術 |
|----|----------|
| ランタイム | .NET 8 |
| UI フレームワーク | WPF (Windows Presentation Foundation) |
| データ形式 | JSON (System.Text.Json) |
| 設定形式 | INI |
| インストーラー | Inno Setup 6 |
| サードパーティ依存 | なし |

### データモデル

```
project.json        → ProjectConfig (Name, Description, CurrentVersion, ProjectNumber,
                                      TypeOptions, TypeColors, NextEntryId, StatsVersions, CreatedAt)
versions/*.json     → DataFile (User, Entries[])      # 未完了 / 完了は Entry.Status で区別
各エントリ          → GoalEntry
```

エントリのフィールド：

| フィールド | JSON 型 | 説明 |
|------------|---------|------|
| `Id` | `string` | 隠し番号 `PPPEEEEEE`（9 桁）。エントリの安定した参照 |
| `Title` | `string` | タイトル（唯一の必須項目） |
| `Severity` | `string` | `Fatal` / `Severe` / `General` / `Patch` / `Update` |
| `Status` | `string` | `Unfinished` / `Finished` |
| `Brief` | `string` | 簡単な説明 |
| `Contents` | `ContentBlock[]` | 本文：順序付きブロック（テキスト / 表 / 区切り線 / コード / ファイル参照 / 多階層リスト / サブタスク） |
| `Progress` | `int` | 完成度 0–100。「サブタスク」ブロックのチェック状況から算出（リスト項目は対象外）。保存時に更新し、読み込み時に再計算 |
| `CompletedAt` | `string` (ISO 8601) | 完了時刻。未完了のときは空 |
| `CreatedAt` | `string` (ISO 8601) | 作成時刻 |
| `UpdatedAt` | `string` (ISO 8601) | 最終更新時刻 |
| `IsFavorited` | `bool` | お気に入り |
| `Type` | `string[]` | タイプタグ（Bug、UI、Feature など） |
| `RelatedFiles` | `{パス: [行,列,関数]}` | 関連ファイル参照。内容ブロックから自動集計 |

---

# 貢献者

| 名前 | 役割 | 貢献 |
|------|------|------|
| [I-AM-SOLO](https://github.com/CialloForMyCode) | 作者 | 設計、開発、保守 |

---

# ライセンス

本プロジェクトは [GPL-2.0 License](LICENSE.txt) で公開されています。

リポジトリ：[https://github.com/CialloForMyCode/OCC-s-Mission-Goals](https://github.com/CialloForMyCode/OCC-s-Mission-Goals)
