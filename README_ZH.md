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

- [安装](#安装)
- [使用](#使用)
- [CLI 命令行](#cli-命令行)
- [程序架构](#程序架构)
- [贡献者](#贡献者)

---

# 安装

### 环境要求

- Windows 10 / 11
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

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

无需任何第三方 NuGet 依赖，纯 .NET 8 + WPF 即开即用。

---

# 使用

### 基本工作流

1. **创建项目** — 菜单 → 新建项目（`Ctrl+N`）：设置名称、描述、初始版本号
2. **创建版本** — 版本对话框迭代版本号（如 `0.1.0-alpha.1` → `0.1.0-alpha.2`）
3. **添加条目** — 工具栏 → 新建条目：填写标题、严重程度、截止日期、关联文件等
4. **跟踪进度** — 在「未完成的条目」页浏览和操作条目
5. **完成归档** — 标记完成后进入「已完成的条目」页，版本内全部完成后可一键归档

### 页面说明

| 页面 | 功能 |
|------|------|
| 仪表盘 | 严重程度分布图表、近期趋势与项目状态总览 |
| 未完成的条目 | 按版本分组展示所有待办条目，支持搜索、排序、编辑、完成、删除 |
| 已完成的条目 | 按版本分组展示已完成条目，支持撤销完成、编辑、删除，全部完成后可归档 |
| 扩展中心 | 插件 / 扩展管理页面 |
| 帮助 | 完整的使用说明：基本操作、快捷键、字段含义、命令行参考 |

### 排序方式

底部工具栏提供 7 种排序：

| 排序 | 说明 |
|------|------|
| 严重程度升序 | Fatal → Update |
| 严重程度降序 | Update → Fatal |
| 截止日期升序 | 早 → 晚 |
| 截止日期降序 | 晚 → 早 |
| 版本升序 | 按版本号字母序 |
| 版本降序 | 按版本号倒序 |
| 仅收藏 | 只显示已收藏条目，按严重程度排序 |

### 严重程度

| 值 | 中文 | 说明 |
|----|------|------|
| `Fatal` | 致命 | 最高优先级，需立即处理 |
| `Severe` | 严重 | 高优先级 |
| `General` | 一般 | 默认等级 |
| `Patch` | 补丁 | 小修复 |
| `Update` | 更新 | 功能更新 |

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

### 双模式

程序在 `Main` 入口处检测启动参数：无参数启动进入 **GUI 模式**（WPF 窗口），带参数启动进入 **CLI 模式**（控制台输出 JSON）。

---

# CLI 命令行

CLI 模式专为 AI / 脚本 / CI 设计，所有输出为标准 JSON，错误输出至 stderr。

```
OCCMissionGoals.exe [-p <项目名称>] [-v <版本号>] <命令> [参数]
```

### 条目命令

| 命令 | 短标志 | 长标志 | 参数 | 说明 |
|------|--------|--------|------|------|
| 添加 | `-a` | `--add` | `{Title="...", Severity="Fatal", ...}` | 添加条目，支持 JSON 或简化 `Key="Value"` 语法 |
| 查看 | `-c` | `--check` | `<编号>` | 查看条目完整信息（JSON） |
| 完成 | `-d` | `--done` | `<编号>` | 标记条目为已完成 |
| 取消完成 | `-u` | `--undone` | `<编号>` | 取消已完成状态 |
| 删除 | `-D` | `--delete` | `<编号>` | 删除条目（不可恢复） |
| 收藏 | `-f` | `--favorited` | `<编号> true\|false` | 设置收藏状态 |
| 列表 | `-l` | `--list` | — | 列出所有条目（JSON 数组） |

### 版本命令（`-v`）

| 用法 | 说明 |
|------|------|
| `-v <版本号>` | 切换到指定版本 |
| `-v Iterate` | 迭代版本号（如 `alpha.0` → `alpha.1`） |
| `-v Delete <版本>` | 删除版本数据文件（不能删除当前版本） |
| `-v Archive <版本>` | 归档版本至 `versions/archive/`（须全部条目已完成，不能归档当前版本） |

### 全局选项

| 标志 | 说明 |
|------|------|
| `-p <名称>` / `--project <名称>` | 指定目标项目 |
| `-v <版本号>` | 配合条目命令时，指定操作的目标版本 |
| `help` / `-h` / `--help` | 打印帮助信息 |

### 添加条目数据格式

```
-a {Title="修复Bug", Severity="Fatal", Brief="简要描述", Detail="详细描述",
    IsFavorited=false, Version="0.1.0", Type=["Bug"],
    RelatedFiles={"P:\\auth.cs"=[10,5,"Login"]}}
```

必填字段仅 `Title`。`Severity` 默认 `General`。`Type` 为字符串数组，`RelatedFiles` 为路径 → `[行, 列, 函数]` 映射。

### 示例

```bash
# 列出项目"ONC"中的所有条目
OCCMissionGoals.exe -p ONC -l

# 添加一条致命 bug
OCCMissionGoals.exe -a {Title="空引用崩溃", Severity="Fatal", Brief="启动时崩溃", Version="0.1.0-alpha.0", Type=["Bug"], RelatedFiles={"C:\\src\\App.cs"=[25,10,"App.Init"]}}

# 标记完成
OCCMissionGoals.exe -d 001000001

# 切换版本并添加条目
OCCMissionGoals.exe -v 0.2.0-alpha.0 -a {Title="新增登录", Severity="Update"}
```

---

# 程序架构

```
OCC-s-Mission-Goals/
├── App.xaml / .cs              # 入口：检测参数 → GUI 或 CLI
├── MainWindow.xaml / .cs       # 主窗口，自定义无边框 + 模糊叠加层
├── CliCommand.cs               # CLI 命令解析与执行
├── ConfigManager.cs            # config.ini 读写
├── ThemeManager.cs             # 亮/暗主题切换
├── FolderPicker.cs             # 文件夹选择器封装
├── AssemblyInfo.cs             # 程序集信息
│
├── Models/                     # 数据模型
│   ├── GoalEntry.cs            # 条目实体 + SortMode 枚举
│   ├── DataFile.cs             # JSON 数据文件结构
│   ├── ProjectConfig.cs        # 项目配置
│   ├── PageRegistration.cs     # 页面注册
│   └── SeverityHelper.cs       # 严重程度 → 显示文字
│
├── Services/                   # 服务层
│   ├── DataService.cs          # JSON 读写 + 跨版本 CRUD
│   ├── ProjectService.cs       # 多项目 & 版本管理
│   └── TipService.cs           # 操作提示语生成
│
├── Pages/                      # 主页面
│   ├── LogPage.xaml            # 仪表盘（Dashboard）
│   ├── UnDonePage.xaml         # 未完成条目
│   ├── DonePage.xaml           # 已完成条目
│   ├── ExpandPage.xaml         # 扩展中心
│   └── HelpPage.xaml           # 帮助（含完整 CLI 文档）
│
├── ToolPages/                  # 底部工具栏页面
│   ├── SwitchPage.xaml         # 页签切换
│   ├── SortPage.xaml           # 排序选择
│   ├── ControlButtonPage.xaml  # 快捷操作按钮
│   └── MenuPage.xaml           # 菜单栏
│
├── Dialogs/                    # 对话框
│   ├── NewEntryDialog.xaml     # 新建 / 编辑条目
│   ├── NewProjectDialog.xaml   # 新建 / 编辑项目
│   └── VersionDialog.xaml      # 版本管理
│
├── Styles.xaml                 # 全局 WPF 样式
└── ThemeBrushes.xaml           # 主题色刷子
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
每个条目              →  GoalEntry
```

条目字段：

| 字段 | JSON 类型 | 说明 |
|------|-----------|------|
| `Id` | `string` | 隐藏编号 `PPPEEEEEE`（9 位） |
| `Title` | `string` | 标题（唯一必填） |
| `Severity` | `string` | 严重程度：Fatal / Severe / General / Patch / Update |
| `Brief` | `string` | 简要描述 |
| `Detail` | `string` | 详细描述 |
| `Deadline` | `[年,月,日]` | 截止日期 |
| `CompletedAt` | `[年,月,日]` | 完成日期 |
| `ChangeDemand` | `int` | 变更需求计数 |
| `IsFavorited` | `bool` | 是否收藏 |
| `Version` | `string` | 所属版本号 |
| `Type` | `string[]` | 类型标签（Bug, UI, Feature 等） |
| `RelatedFiles` | `{路径: [行,列,函数]}` | 关联文件引用 |

---

# 贡献者

| 昵称 | 角色 | 贡献 |
|------|------|------|
| [I-AM-SOLO](https://github.com/CialloForMyCode) | 作者 | 设计、开发、维护 |

---

> 本项目采用 MIT License。
> 仓库地址：[https://github.com/CialloForMyCode/OCC-s-Mission-Goals](https://github.com/CialloForMyCode/OCC-s-Mission-Goals)
