# OCC's Mission & Goals

一款为 ONC Compiler Collection 开发的更新/修复管理工具，为了帮助患有健忘症的我更加快速的开发 ONC Compiler Collection。该工具旨在简化在MD中填写错误然后一个个找哪个没完成的流程，提高效率，并治好我的健忘症。

# Language
[中文 README](README_ZH.md) **|**  
[README for English](README.md) **|**  
[README на русском](README_RU.md) **|**  
[日本語の README](README_JP.md) **|**  
[한국어 README](README_KR.md) **|**  
[README en français](README_FR.md) **|**  

# 目录
- [OCC's Mission \& Goals](#occs-mission--goals)
- [Language](#language)
- [目录](#目录)
- [安装](#安装)
- [使用](#使用)
- [为AI设计的控制台命令](#为ai设计的控制台命令)
- [程序架构](#程序架构)
- [贡献者名单](#贡献者名单)

# 安装

### 环境要求

- Windows 10 / 11
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### 编译

```bash
git clone https://github.com/OCCOCCO/ONC.git
cd "ONC/OCC's Mission & Goals"
dotnet build
```

### 运行

```bash
# GUI 模式
dotnet run

# CLI 模式
dotnet run -- -h
```

无需任何第三方 NuGet 依赖，纯 .NET 8 + WPF 即开即用。

---

# 使用

### 基本工作流

1. **创建项目** — 菜单 → 新建项目，或 `Ctrl+N`；设置名称、描述、初始版本号
2. **创建版本** — 版本对话框迭代版本号（如 `0.1.0-alpha.1` → `0.1.0-alpha.2`）
3. **添加条目** — 工具栏 → 新建条目，填写标题、严重程度（致命/严重/一般/补丁/更新）、截止日期、关联文件等
4. **跟踪进度** — 在「未完成的条目」页查看列表，完成或删除条目
5. **切换版本** — 版本对话框切换历史版本，条目数据自动跟随版本文件

### 数据存储

所有数据保存在可执行文件同级目录的 `Projects/` 下：

```
Projects/
└── <项目名称>/
    ├── project.json              # 项目元数据
    └── versions/
        ├── 0.1.0-alpha.0.json    # 当前版本数据
        ├── 0.2.0-alpha.0.json
        └── archive/              # 归档版本
```

条目编号格式为 `PPPEEEEEE`（9 位），前 3 位为项目编号，后 6 位为自增条目编号。

### 双模式

程序在 `Main` 入口处检测启动参数：无参数启动进入 **GUI 模式**（WPF 窗口），带参数启动进入 **CLI 模式**（控制台输出 JSON）。

---

# 为AI设计的控制台命令

CLI 模式专为 AI / 脚本 / CI 设计，所有输出为标准 JSON，错误输出至 stderr。

```
OCCMissionGoals.exe [-p <项目名称>] <命令> [参数]
```

### 条目命令

| 命令 | 短标志 | 长标志 | 参数 | 说明 |
|------|--------|--------|------|------|
| 添加 | `-a` | `--add` | `{Title="...", Severity="Fatal", ...}` | 添加条目，支持自定义 `Key="Value"` 格式 |
| 查看 | `-c` | `--check` | `<编号>` | 查看条目完整信息 |
| 完成 | `-d` | `--done` | `<编号>` | 标记条目为已完成 |
| 取消完成 | `-u` | `--undone` | `<编号>` | 取消已完成状态 |
| 删除 | `-D` | `--delete` | `<编号>` | 删除条目 |
| 收藏 | `-f` | `--favorited` | `<编号> true\|false` | 设置收藏状态 |
| 列表 | `-l` | `--list` | — | 列出所有条目（JSON 数组） |

### 版本命令（`-v`）

| 用法 | 说明 |
|------|------|
| `-v 0.1.0` | 切换到指定版本 |
| `-v Iterate` | 迭代版本号（`0.1.0-alpha.3` → `0.1.0-alpha.4`） |
| `-v Delete 0.1.0` | 删除版本（不可删除当前版本） |
| `-v Archive 0.1.0` | 归档版本至 `versions/archive/` |

### 全局选项

| 标志 | 说明 |
|------|------|
| `-p <名称>` / `--project <名称>` | 选择目标项目 |

### 示例

```bash
# 列出项目"ONC"中的所有条目
OCCMissionGoals.exe -p ONC -l

# 添加一条致命 bug
OCCMissionGoals.exe -a {Title="空引用崩溃", Severity="Fatal", Brief="启动时崩溃", Version="0.1.0-alpha.0", Type=["Bug"], RelatedFiles={"C:\\src\\App.cs"=[25,10,"App.Init"]}}

# 标记完成
OCCMissionGoals.exe -d 001000001

# 切换版本
OCCMissionGoals.exe -v 0.2.0-alpha.0
```

### 严重程度

| 值 | 中文含义 |
|----|----------|
| `Fatal` | 致命 |
| `Severe` | 严重 |
| `General` | 一般 |
| `Patch` | 补丁 |
| `Update` | 更新 |

---

# 程序架构

```
OCC's Mission & Goals/
├── App.xaml / .cs              # 入口：检测参数 → GUI 或 CLI
├── MainWindow.xaml / .cs       # 主窗口，自定义无边框 + 模糊叠加层
├── CliCommand.cs               # CLI 命令解析与执行
├── ConfigManager.cs            # config.ini 读写
├── ThemeManager.cs             # 亮/暗主题切换（28×2 色刷子）
│
├── Models/                     # 数据模型
│   ├── GoalEntry.cs            # 条目实体：Id、Title、Severity 等
│   ├── DataFile.cs             # JSON 数据文件结构
│   ├── ProjectConfig.cs        # 项目配置
│   └── SeverityHelper.cs       # 严重程度 → 显示文字
│
├── Services/                   # 服务层
│   ├── DataService.cs          # JSON 读写 + 跨版本 CRUD
│   ├── ProjectService.cs       # 多项目 & 版本管理
│   └── TipService.cs           # 提示语生成
│
├── Pages/                      # 主页面
│   ├── LogPage.xaml            # 仪表盘
│   ├── UnDonePage.xaml         # 未完成条目
│   ├── DonePage.xaml           # 已完成条目
│   ├── ExpandPage.xaml         # 扩展（预留）
│   └── HelpPage.xaml           # 帮助
│
├── ToolPages/                  # 工具栏
│   ├── SwitchPage.xaml         # 页签切换
│   ├── SortPage.xaml           # 排序
│   ├── ControlButtonPage.xaml  # 快捷操作
│   └── MenuPage.xaml           # 菜单
│
├── Dialogs/                    # 对话框
│   ├── NewEntryDialog.xaml     # 新建/编辑条目
│   ├── NewProjectDialog.xaml   # 新建/编辑项目
│   └── VersionDialog.xaml      # 版本管理
│
├── Styles.xaml                 # 全局 WPF 样式
└── ThemeBrushes.xaml           # 主题刷子
```

### 技术栈

- **运行时**: .NET 8
- **UI 框架**: WPF (Windows Presentation Foundation)
- **数据格式**: JSON (System.Text.Json)
- **配置格式**: INI
- **第三方依赖**: 无

### 数据模型

```
project.json          →  ProjectConfig (Name, Description, CurrentVersion, ProjectNumber)
versions/*.json       →  DataFile (User, Unfinished[], Finished[])
每个 Unfinished/Finished   →  GoalEntry
```

条目字段：

| 字段 | 类型 | 说明 |
|------|------|------|
| `Id` | `string` | 隐藏编号 `PPPEEEEEE` |
| `Title` | `string` | 标题 |
| `Severity` | `enum` | 严重程度 |
| `Brief` | `string` | 简要描述 |
| `Detail` | `string` | 详细描述 |
| `Deadline` | `int[]` | 截止日期 `[年, 月, 日]` |
| `CompletedAt` | `int[]` | 完成日期 |
| `ChangeDemand` | `int` | 变更需求计数 |
| `IsFavorited` | `bool` | 是否收藏 |
| `Version` | `string` | 所属版本 |
| `Type` | `string[]` | 类型标签（Bug, UI, Feature 等） |
| `RelatedFiles` | `dict` | 关联文件引用 |

---

# 贡献者名单

| 昵称 | 角色 | 贡献 |
|------|------|------|
| [OCCO](https://github.com/OCCOCCO) | 作者 | 设计、开发、维护 |
| [Reasonix](https://github.com/Reasonix) | AI 助手 | CLI 重构、Bug 修复、文档 |

---

> 本项目采用 MIT License。  
> 仓库地址：[https://github.com/OCCOCCO/ONC](https://github.com/OCCOCCO/ONC)
