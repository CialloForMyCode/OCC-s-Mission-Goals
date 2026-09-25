<div align="center">

# OCC's Mission & Goals

一款为 ONC Compiler Collection 开发的更新 / 修复管理工具，为了帮助患有健忘症的我更快速地开发 ONC Compiler Collection。
它把「在 MD 里记下问题、再一个个翻找哪个没做完」的流程收进一个程序里，提高效率，顺便治治我的健忘症。
双模式：日常使用 WPF 图形界面，另有 CLI 输出标准 JSON，供 AI / 脚本 / CI 集成。

[![Platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-0078D6)](https://github.com/CialloForMyCode/OCC-s-Mission-Goals/releases/latest)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![UI](https://img.shields.io/badge/UI-WPF-5C2D91)](https://learn.microsoft.com/dotnet/desktop/wpf/)
[![License](https://img.shields.io/badge/license-GPL--2.0-blue)](LICENSE.txt)
[![Release](https://img.shields.io/github/v/release/CialloForMyCode/OCC-s-Mission-Goals?label=release&color=brightgreen)](https://github.com/CialloForMyCode/OCC-s-Mission-Goals/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/CialloForMyCode/OCC-s-Mission-Goals/total?label=downloads&color=blue)](https://github.com/CialloForMyCode/OCC-s-Mission-Goals/releases)

**语言：** [中文](README_ZH.md) | [English](README.md) | [Русский](README_RU.md) | [日本語](README_JP.md) | [한국어](README_KR.md) | [Français](README_FR.md)

</div>

---

# 目录

- [安装](#安装)
- [使用](#使用)
- [CLI 命令行](#cli-命令行)
- [程序架构](#程序架构)
- [贡献者](#贡献者)
- [许可协议](#许可协议)

---

# 安装

### 下载

预编译的安装包发布在 [Releases](https://github.com/CialloForMyCode/OCC-s-Mission-Goals/releases/latest) 页面，均为自包含版本，无需安装 .NET 运行时：

| 架构 | 安装包 |
|------|--------|
| x64（推荐） | `OCC-Mission-Goals-<版本号>-x64-setup.exe` |
| x86 | `OCC-Mission-Goals-<版本号>-x86-setup.exe` |

### 环境要求

| 项目 | 要求 |
|------|------|
| 操作系统 | Windows 10 / 11 |
| 运行安装包 | 无（自包含） |
| 从源码构建 | [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) |

### 编译

```bash
git clone https://github.com/CialloForMyCode/OCC-s-Mission-Goals.git
cd "OCC-s-Mission-Goals"
dotnet build
```

### 运行

```bash
# GUI 模式
dotnet run

# CLI 模式（查看帮助）
dotnet run -- -h
```

### 打包安装程序

```bash
installer\build-installer.cmd
```

需要 [Inno Setup 6](https://jrsoftware.org/isinfo.php)，产物输出到 `output/`。

无需任何第三方 NuGet 依赖，纯 .NET 8 + WPF 即开即用。

---

# 使用

### 基本工作流

1. **创建项目** — 菜单 → 新建项目（`Ctrl+N`）：设置名称、描述、初始版本号
2. **创建版本** — 版本对话框迭代版本号（如 `0.1.0-alpha.1` → `0.1.0-alpha.2`）
3. **添加条目** — 工具栏 → 新建条目：填写标题、严重程度、类型标签与内容区块
4. **跟踪进度** — 在「未完成的条目」页浏览和操作条目
5. **完成归档** — 标记完成后进入「已完成的条目」页，版本内全部完成后可一键归档
6. **管理项目** — 设置 → 项目信息：可修改名称与描述，或删除当前项目（永久删除所有版本与条目，不可恢复）

### 页面说明

| 页面 | 功能 |
|------|------|
| 仪表盘 | 严重程度分布、30 天严重程度趋势图、完成情况统计与项目状态总览 |
| 未完成的条目 | 按版本分组展示所有待办条目，支持搜索、排序、编辑、完成、删除 |
| 已完成的条目 | 按版本分组展示已完成条目，支持撤销完成、编辑、删除，全部完成后可归档 |
| 扩展中心 | 安装 / 卸载语言包、主题与扩展并显示进度条；已安装项自动比对仓库版本，可一键升级 |
| 帮助 | 完整的使用说明：基本操作、快捷键、字段含义、命令行参考 |

### 条目内容

条目的正文不再是一整段 Markdown，而是由若干可拖拽排序的区块组成。
拖拽左侧手柄排序，按 `+` 添加子块：

| 区块 | 说明 |
|------|------|
| 文本 | `#` 标题、粗体、斜体、删除线、编号列表与列表 |
| 表格 | 表头 + 任意行数据 |
| 分割线 | 横向分隔 |
| 代码块 | 可选语言标识与行号 |
| 文件引用 | 路径 + 行 / 列 / 函数 |
| 多级列表 | 可嵌套的列表项 |
| 子任务 | 可勾选项，决定条目的完成度 |

保存条目时，关联文件与完成度都由内容区自动汇总。

### 排序方式

| 排序 | 说明 |
|------|------|
| 严重程度升序 | Fatal → Update |
| 严重程度降序 | Update → Fatal |
| 版本升序 | 按版本号字母序 |
| 版本降序 | 按版本号倒序 |
| 类别升序 | 按类型标签（首个标签）字母序 |
| 仅收藏 | 只显示已收藏条目，按严重程度排序 |

### 严重程度

| 值 | 中文 | 标记 | 说明 |
|----|------|------|------|
| `Fatal` | 致命 | 红 | 最高优先级，需立即处理 |
| `Severe` | 严重 | 橙 | 高优先级 |
| `General` | 一般 | 黄 | 默认等级 |
| `Patch` | 补丁 | 绿 | 小修复 |
| `Update` | 更新 | 蓝 | 功能更新 |

### 搜索

顶部搜索框输入关键词即可实时过滤当前列表，加前缀可切换匹配范围：

| 前缀 | 匹配范围 |
|------|----------|
| `Text:` | 标题与简介（不加前缀时也按此匹配） |
| `Tag:` | 类型标签 |
| `Setting:` | 设置快捷项（项目信息 / 主题 / 数据统计），按回车直接执行选中项 |
| `Function:` | 功能命令，如新建条目、新建项目、打开项目 |
| `File:` | 关联文件路径 |
| `Date:` | 日期 |
| `Plugins:` | 扩展中心全部插件（全局搜索） |
| `Expand:` | 只搜索已安装的插件（全局搜索） |

### 数据存储

所有数据保存在可执行文件同级目录的 `Projects/` 下：

```
Projects/
└── <项目名称>/
    ├── project.json              # 项目元数据
    └── versions/
        ├── 0.1.0-alpha.0.json    # 版本数据文件
        ├── 0.2.0-alpha.0.json
        └── archive/              # 归档版本
```

条目编号格式为 `PPPEEEEEE`（9 位），前 3 位为项目编号，后 6 位为自增条目编号。

### 扩展存储

从扩展中心安装的语言包、主题与扩展都保存在可执行文件同级目录：

```
Languages/            # 语言包（*.xaml）
Themes/               # 主题（*.xaml）
Expand/<名称>/        # 扩展：expand.json 清单 + 可选的 XAML
```

扩展可以覆盖资源键（颜色、圆角、边框粗细等），也可以提供布局片段替换主内容的一部分。

### 双模式

程序在 `Main` 入口处检测启动参数：无参数启动进入 **GUI 模式**（WPF 窗口），带参数启动进入 **CLI 模式**（控制台输出）。
GUI 与 CLI 是两个独立进程，通过命名互斥锁串行化数据读写，因此可以同时运行而不会互相覆盖。

---

# CLI 命令行

CLI 模式专为 AI / 脚本 / CI 设计。默认输出人类可读文本，加 `--json` 输出标准 JSON，错误输出至 stderr。

```
OCCMissionGoals.exe [全局选项] <命令> [子命令] [参数]
```

### 全局选项

| 标志 | 说明 |
|------|------|
| `-p`, `--project <名称>` | 指定目标项目（文件夹名或项目名） |
| `--json` | 输出 JSON，而非人类可读文本 |
| `--version <版本号>` | 只读取指定版本，不切换当前版本 |
| `-h`, `--help` | 打印帮助；附加在命令后可查看该命令的详细说明（如 `entry --help`） |

### project — 项目管理

| 用法 | 说明 |
|------|------|
| `project list` | 列出所有项目 |
| `project info [名称]` | 查看项目信息（默认 `-p` 指定的项目） |

### version — 版本管理

| 用法 | 说明 |
|------|------|
| `version list` | 列出所有版本（`*` 表示当前版本） |
| `version current` | 显示当前版本号 |
| `version switch <版本号>` | 切换到指定版本（持久保存） |
| `version iterate` | 迭代当前版本（预发布号 +1，如 `0.1.0-alpha.0` → `0.1.0-alpha.1`） |
| `version delete <版本号>` | 删除版本（不能删除当前版本） |
| `version archive <版本号>` | 归档版本至 `versions/archive/`（须全部条目已完成） |

### entry — 条目管理

| 命令 | 说明 |
|------|------|
| `entry list` | 列出条目 |
| `entry show <编号\|索引\|标题>` | 查看单个条目的完整信息（JSON） |
| `entry add` | 添加条目 |
| `entry edit <编号\|索引\|标题>` | 编辑条目 |
| `entry done <编号\|索引\|标题>` | 标记为已完成 |
| `entry undone <编号\|索引\|标题>` | 取消已完成状态 |
| `entry delete <编号\|索引\|标题>` | 删除条目（不可恢复） |
| `entry favorite <编号\|索引\|标题> <true\|false>` | 设置收藏状态 |

完整语法：

```
entry list     [--type u|f|a] [--search <关键词>] [--tag <标签>] [--favorite] [--all] [--version <版本号>]
entry show     <编号|索引|标题> [--type u|f] [--version <版本号>]
entry add      --title <标题> [--severity <等级>] [--brief <简介>] [--type <标签1,标签2>]
               [--favorite] [--version <版本号>]
entry edit     <编号|索引|标题> [--title ...] [--severity ...] [--brief ...] [--type ...]
               [--favorite|--unfavorite] [--version <版本号>]
```

`<编号|索引|标题>` 可用隐藏编号（如 `001000001`）、列表序号或标题精确匹配。
`--type` 在 `list` / `show` 中表示范围（`u` 未完成、`f` 已完成、`a` 全部），在 `add` / `edit` 中表示类型标签（逗号分隔）。

### tag — 标签管理

管理当前项目的类型标签（类别），更改会同步到所有版本的条目。

| 用法 | 说明 |
|------|------|
| `tag list` | 列出所有标签（含颜色） |
| `tag add <名称> [--color <hex>]` | 新建标签 |
| `tag delete <名称>` | 删除标签，并从所有版本条目中移除 |
| `tag rename <旧名称> <新名称>` | 重命名标签，并同步所有版本条目 |

### 旧写法

旧版的单标志语法仍然兼容：

```
-a/--add   -c/--check   -d/--done   -u/--undone   -D/--delete
-f/--favorited   -l/--list   -v <版本号|Iterate|Delete|Archive>
```

`-a` / `--add` 还支持旧的 JSON 形式：`-a {Title="...", Severity="Fatal", ...}`

### 示例

```bash
# 列出项目"ONC"中的所有条目
OCCMissionGoals.exe -p ONC entry list

# 添加一条致命 bug
OCCMissionGoals.exe -p ONC entry add --title "启动时空引用崩溃" --severity Fatal --brief "启动时崩溃" --type Bug --version 0.1.0-alpha.0

# 输出 JSON 供脚本消费
OCCMissionGoals.exe -p ONC --json entry list

# 标记完成
OCCMissionGoals.exe -p ONC entry done 001000001

# 切换版本并添加条目
OCCMissionGoals.exe -p ONC version switch 0.2.0-alpha.0
OCCMissionGoals.exe -p ONC entry add --title "新增登录" --severity Update

# 标签管理
OCCMissionGoals.exe -p ONC tag add UI --color "#3D9DE8"
```

---

# 程序架构

```
OCC-s-Mission-Goals/
├── App.xaml / .cs              # 入口：检测参数 → GUI 或 CLI
├── MainWindow.xaml / .cs       # 主窗口，自定义无边框 + 模糊叠加层
├── CliCommand.cs               # CLI 命令解析与执行
├── ConfigManager.cs            # config.ini 读写
├── LocalizationManager.cs      # 语言包查找，T(key)
├── ThemeManager.cs             # 亮 / 暗主题切换
├── FolderPicker.cs             # 文件夹选择器封装
├── AssemblyInfo.cs             # 程序集信息
├── Styles.xaml                 # 全局 WPF 样式
│
├── Models/                     # 数据模型
│   ├── GoalEntry.cs            # 条目实体；GoalSeverity / SortMode / SearchMode
│   ├── ContentBlock.cs         # 内容区块（文本 / 表格 / 代码 / 文件 / 列表 / 子任务）
│   ├── DataFile.cs             # 数据文件根结构：User + Entries
│   ├── ProjectConfig.cs        # project.json
│   ├── PageRegistration.cs     # 页面注册
│   ├── SeverityHelper.cs       # 严重程度 → 显示文字与颜色
│   ├── TypeTag.cs              # 类别标签显示模型（文本 + 颜色）
│   ├── SearchMatcher.cs        # 按搜索模式匹配条目
│   ├── RelativeTime.cs         # "几分钟前"这类相对时间文案
│   ├── ExpandInfo.cs           # 扩展清单（expand.json）
│   ├── PluginInfo.cs           # 扩展卡片状态
│   └── ColorUtil.cs            # 颜色字符串 ↔ 画刷
│
├── Services/                   # 服务层
│   ├── DataService.cs          # 数据文件读写
│   ├── ProjectService.cs       # 多项目 & 版本管理
│   ├── ContentBlocks.cs        # 内容区渲染与派生数据
│   ├── Markdown.cs             # 轻量级 Markdown 渲染器
│   ├── EntryCopyFormatter.cs   # 「复制信息」生成的纯文本
│   ├── FileLock.cs             # 数据文件的跨进程互斥锁
│   ├── FileRefJump.cs          # 跳转到关联文件位置
│   ├── ExpandService.cs        # 扩展的扫描、装载与管理
│   ├── ExpandAnimation.cs      # 展开 / 收起的平滑过渡
│   ├── LanguagePackService.cs  # 语言包下载与安装
│   ├── ThemePackService.cs     # 主题下载与安装
│   ├── PluginCatalog.cs        # 扩展中心与搜索共用的插件目录
│   ├── UpdateService.cs        # 更新检查
│   ├── AutoStartService.cs     # 开机自启动（HKCU Run 注册表项）
│   └── TipService.cs           # 操作提示语生成
│
├── Pages/                      # 主页面
│   ├── LogPage.xaml            # 仪表盘（分布 + 趋势图 + 统计）
│   ├── UnDonePage.xaml         # 未完成条目
│   ├── DonePage.xaml           # 已完成条目
│   ├── ExpandPage.xaml         # 扩展中心
│   ├── SettingsPage.xaml       # 设置
│   └── HelpPage.xaml           # 帮助（含完整 CLI 文档）
│
├── Controls/                   # 自定义控件
│   ├── Marquee.cs              # 单行文本跑马灯
│   └── ProgressBarAnimation.cs # 进度条填充的平滑过渡
│
├── ToolPages/                  # 底部工具栏页面
│   ├── SwitchPage.xaml         # 页签切换
│   ├── SortPage.xaml           # 排序选择
│   ├── ControlButtonPage.xaml  # 快捷操作按钮
│   └── MenuPage.xaml           # 菜单栏
│
├── Dialogs/                    # 对话框
│   ├── NewEntryDialog.xaml     # 新建 / 编辑条目
│   ├── BlockEditor.xaml        # 内容区块编辑器
│   ├── NewProjectDialog.xaml   # 新建 / 编辑项目
│   └── VersionDialog.xaml      # 版本管理
│
├── Languages/                  # 随程序附带的语言包（zh、en、ja、ko、ru）
├── Themes/                     # 随程序附带的主题（Default、ItIsPinkish）
└── installer/                  # Inno Setup 脚本与打包脚本
```

### 技术栈

| 层 | 选择 |
|----|------|
| 运行时 | .NET 8 |
| UI 框架 | WPF (Windows Presentation Foundation) |
| 数据格式 | JSON (System.Text.Json) |
| 配置格式 | INI |
| 安装程序 | Inno Setup 6 |
| 第三方依赖 | 无 |

### 数据模型

```
project.json        → ProjectConfig (Name, Description, CurrentVersion, ProjectNumber,
                                      TypeOptions, TypeColors, NextEntryId, StatsVersions, CreatedAt)
versions/*.json     → DataFile (User, Entries[])      # 未完成 / 已完成由 Entry.Status 区分
每个条目            → GoalEntry
```

条目字段：

| 字段 | JSON 类型 | 说明 |
|------|-----------|------|
| `Id` | `string` | 隐藏编号 `PPPEEEEEE`（9 位），条目的稳定引用 |
| `Title` | `string` | 标题（唯一必填） |
| `Severity` | `string` | 严重程度：`Fatal` / `Severe` / `General` / `Patch` / `Update` |
| `Status` | `string` | 状态：`Unfinished` / `Finished` |
| `Brief` | `string` | 简要描述 |
| `Contents` | `ContentBlock[]` | 条目正文：有序区块（文本 / 表格 / 分割线 / 代码块 / 文件引用 / 多级列表 / 子任务） |
| `Progress` | `int` | 完成度 0–100，由「子任务」区块的勾选情况推导（列表条目不计入），保存时刷新，加载时按当前规则重算 |
| `CompletedAt` | `string` (ISO 8601) | 完成时间，未完成时为空 |
| `CreatedAt` | `string` (ISO 8601) | 创建时间 |
| `UpdatedAt` | `string` (ISO 8601) | 最后修改时间 |
| `IsFavorited` | `bool` | 是否收藏 |
| `Type` | `string[]` | 类型标签（Bug, UI, Feature 等） |
| `RelatedFiles` | `{路径: [行,列,函数]}` | 关联文件引用，由内容区自动汇总 |

---

# 贡献者

| 昵称 | 角色 | 贡献 |
|------|------|------|
| [I-AM-SOLO](https://github.com/CialloForMyCode) | 作者 | 设计、开发、维护 |

---

# 许可协议

本项目采用 [GPL-2.0 License](LICENSE.txt)。

仓库地址：[https://github.com/CialloForMyCode/OCC-s-Mission-Goals](https://github.com/CialloForMyCode/OCC-s-Mission-Goals)
