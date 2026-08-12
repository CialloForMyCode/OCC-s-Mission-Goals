using System.IO;
using System.Linq;
using System.Text.Json;
using OCCMissionGoals.Models;

namespace OCCMissionGoals.Services;

/// <summary>
/// 管理多项目和多版本。每个项目一个文件夹，版本为其中的 .json 数据文件。
/// </summary>
public static class ProjectService
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>所有项目存放在 EXE 目录下的 Projects/ 中。</summary>
    public static string ProjectsDir =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Projects");

    /// <summary>当前打开的项目配置，无项目时为 null。</summary>
    public static ProjectConfig? CurrentProject { get; private set; }

    /// <summary>当前项目文件夹路径。</summary>
    public static string? CurrentProjectDir { get; private set; }

    // ======================== 枚举项目 ========================

    /// <summary>列出 Projects/ 下所有包含 project.json 的项目目录。</summary>
    public static List<string> GetProjectDirectories()
    {
        var list = new List<string>();
        if (!Directory.Exists(ProjectsDir)) return list;

        foreach (var dir in Directory.GetDirectories(ProjectsDir))
        {
            if (File.Exists(Path.Combine(dir, "project.json")))
                list.Add(dir);
        }
        return list;
    }

    /// <summary>列出当前项目文件夹下所有版本 .json 文件（排除 project.json）。</summary>
    public static List<string> GetVersionFiles(string projectDir)
    {
        var list = new List<string>();
        if (!Directory.Exists(projectDir)) return list;

        var versionsDir = GetVersionsDir(projectDir);
        if (!Directory.Exists(versionsDir)) return list;

        foreach (var file in Directory.GetFiles(versionsDir, "*.json"))
        {
            var name = Path.GetFileName(file);
            list.Add(name);
        }
        // 按名称排序
        list.Sort();
        return list;
    }

    /// <summary>项目下的版本子目录。</summary>
    public static string GetVersionsDir(string projectDir) =>
        Path.Combine(projectDir, "versions");

    // ======================== 项目操作 ========================

    /// <summary>新建项目：创建文件夹 → 写入 project.json → 创建初始版本。</summary>
    public static ProjectConfig CreateProject(string name, string description = "", string initialVersion = "0.1.0-alpha.0")
    {
        var safeName = SanitizeFolderName(name);
        var dir = Path.Combine(ProjectsDir, safeName);
        Directory.CreateDirectory(dir);

        // CurrentVersion 存储干净版本号（不含 .json），用于 UI 显示和条目版本字段
        var cleanVersion = initialVersion.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? initialVersion[..^5]
            : initialVersion;

        var config = new ProjectConfig
        {
            Name = name,
            Description = description,
            CurrentVersion = cleanVersion,
            CreatedAt = DateTime.Now,
            ProjectNumber = GetNextProjectNumber()
        };

        SaveProjectConfig(dir, config);

        // 创建初始版本数据文件：versions/{cleanVersion}.json
        var versionsDir = GetVersionsDir(dir);
        Directory.CreateDirectory(versionsDir);
        var versionFile = cleanVersion + ".json";
        var dataPath = Path.Combine(versionsDir, versionFile);
        var empty = new DataFile();
        File.WriteAllText(dataPath,
            JsonSerializer.Serialize(empty, _jsonOptions));

        // 切换到新项目
        CurrentProject = config;
        CurrentProjectDir = dir;
        DataService.SetFilePath(dataPath);
        DataService.Load();

        // 为旧数据回填隐藏编号
        BackfillEntryIds(DataService.Current);

        // 保存最后打开的项目路径到 config.ini
        ConfigManager.Set("Project", "LastProject", safeName);

        return config;
    }

    /// <summary>打开已有项目文件夹。</summary>
    public static ProjectConfig? OpenProject(string projectDir)
    {
        var configPath = Path.Combine(projectDir, "project.json");
        if (!File.Exists(configPath)) return null;

        var json = File.ReadAllText(configPath);
        var config = JsonSerializer.Deserialize<ProjectConfig>(json, _jsonOptions);
        if (config == null) return null;

        CurrentProject = config;
        CurrentProjectDir = projectDir;

        // 使用 CurrentVersion 定位数据文件（versions/ 子目录）
        var versionsDir = GetVersionsDir(projectDir);
        var versionFile = string.IsNullOrWhiteSpace(config.CurrentVersion)
            ? "data.json"
            : config.CurrentVersion + ".json";
        var dataPath = Path.Combine(versionsDir, versionFile);
        if (!File.Exists(dataPath))
        {
            // 版本文件不存在时回退到 data.json（兼容旧项目）
            var legacyPath = Path.Combine(versionsDir, "data.json");
            if (File.Exists(legacyPath))
                dataPath = legacyPath;
            else
            {
                Directory.CreateDirectory(versionsDir);
                File.WriteAllText(dataPath,
                    JsonSerializer.Serialize(new DataFile(), _jsonOptions));
            }
        }

        DataService.SetFilePath(dataPath);
        DataService.Load();

        // 保存最后打开的项目路径
        ConfigManager.Set("Project", "LastProject", Path.GetFileName(projectDir));

        return config;
    }

    /// <summary>更新当前项目配置并保存到 project.json。</summary>
    public static void UpdateProjectConfig(ProjectConfig config)
    {
        if (CurrentProjectDir == null) return;
        CurrentProject = config;
        SaveProjectConfig(CurrentProjectDir, config);
    }

    // ======================== 版本操作 ========================

    /// <summary>在当前项目下新建版本文件。</summary>
    public static string CreateVersion(string versionFileName)
    {
        if (CurrentProjectDir == null)
            throw new InvalidOperationException("没有打开的项目。");

        var versionsDir = GetVersionsDir(CurrentProjectDir);
        Directory.CreateDirectory(versionsDir);
        var versionPath = Path.Combine(versionsDir, versionFileName);
        var empty = new DataFile();
        File.WriteAllText(versionPath,
            JsonSerializer.Serialize(empty, _jsonOptions));

        return versionFileName;
    }

    /// <summary>切换到当前项目下的另一个版本文件。</summary>
    public static void SwitchVersion(string versionFileName)
    {
        if (CurrentProjectDir == null)
            throw new InvalidOperationException("没有打开的项目。");
        if (CurrentProject == null)
            throw new InvalidOperationException("没有打开的项目配置。");

        var versionsDir = GetVersionsDir(CurrentProjectDir);
        var versionPath = Path.Combine(versionsDir, versionFileName);
        if (!File.Exists(versionPath))
            throw new FileNotFoundException("版本文件不存在。", versionPath);

        // 先保存当前数据
        DataService.Save();

        // 更新项目配置（存储干净版本号，不含 .json）
        CurrentProject.CurrentVersion = versionFileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? versionFileName[..^5]
            : versionFileName;
        SaveProjectConfig(CurrentProjectDir, CurrentProject);

        // 切换数据文件
        DataService.SetFilePath(versionPath);
        DataService.Load();
    }

    /// <summary>尝试从 config.ini 恢复上次打开的项目。</summary>
    public static bool TryRestoreLastProject()
    {
        var lastName = ConfigManager.Get("Project", "LastProject", "");
        if (string.IsNullOrEmpty(lastName)) return false;

        var lastDir = Path.Combine(ProjectsDir, lastName);
        if (!Directory.Exists(lastDir)) return false;

        return OpenProject(lastDir) != null;
    }

    /// <summary>更新当前项目版本标签，同时创建/切换对应的数据文件。</summary>
    public static void UpdateVersion(string newVersion)
    {
        if (CurrentProject == null || CurrentProjectDir == null)
            throw new InvalidOperationException("没有打开的项目。");

        // 先保存当前数据
        DataService.Save();

        var cleanVersion = newVersion.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? newVersion[..^5]
            : newVersion;
        var versionFileName = cleanVersion + ".json";
        var versionsDir = GetVersionsDir(CurrentProjectDir);
        Directory.CreateDirectory(versionsDir);
        var versionPath = Path.Combine(versionsDir, versionFileName);

        // 如果新版本文件还不存在，把当前数据写过去；否则切换到已有文件
        if (!File.Exists(versionPath))
        {
            var json = JsonSerializer.Serialize(DataService.Current, _jsonOptions);
            File.WriteAllText(versionPath, json);
        }

        // 更新配置
        CurrentProject.CurrentVersion = cleanVersion;
        SaveProjectConfig(CurrentProjectDir, CurrentProject);

        // 切换数据文件
        DataService.SetFilePath(versionPath);
        DataService.Load();
    }

    // ======================== 条目编号 ========================

    /// <summary>为新条目分配隐藏编号（PPPEEEEEE 格式）。</summary>
    public static void AssignEntryId(GoalEntry entry)
    {
        if (CurrentProject == null || CurrentProjectDir == null) return;
        entry.Id = $"{CurrentProject.ProjectNumber:D3}{CurrentProject.NextEntryId:D6}";
        CurrentProject.NextEntryId++;
        SaveProjectConfig(CurrentProjectDir, CurrentProject);
    }

    /// <summary>为缺少 Id 的旧条目回填编号，并同步 NextEntryId。</summary>
    public static void BackfillEntryIds(DataFile data)
    {
        if (CurrentProject == null || CurrentProjectDir == null) return;
        bool changed = false;

        foreach (var entry in data.Unfinished.Concat(data.Finished))
        {
            if (string.IsNullOrEmpty(entry.Id))
            {
                entry.Id = $"{CurrentProject.ProjectNumber:D3}{CurrentProject.NextEntryId:D6}";
                CurrentProject.NextEntryId++;
                changed = true;
            }
            else if (entry.Id.Length == 9 && int.TryParse(entry.Id[3..], out int num))
            {
                if (num >= CurrentProject.NextEntryId)
                    CurrentProject.NextEntryId = num + 1;
            }
        }

        if (changed)
        {
            SaveProjectConfig(CurrentProjectDir, CurrentProject);
            DataService.Save();
        }
    }

    private static int GetNextProjectNumber()
    {
        int max = 0;
        if (!Directory.Exists(ProjectsDir)) return 1;
        foreach (var dir in Directory.GetDirectories(ProjectsDir))
        {
            var cfgPath = Path.Combine(dir, "project.json");
            if (!File.Exists(cfgPath)) continue;
            try
            {
                var json = File.ReadAllText(cfgPath);
                var cfg = JsonSerializer.Deserialize<ProjectConfig>(json, _jsonOptions);
                if (cfg != null && cfg.ProjectNumber > max)
                    max = cfg.ProjectNumber;
            }
            catch { /* 损坏的 project.json 跳过 */ }
        }
        return max + 1;
    }

    // ======================== 内部工具 ========================

    private static void SaveProjectConfig(string dir, ProjectConfig config)
    {
        var path = Path.Combine(dir, "project.json");
        var json = JsonSerializer.Serialize(config, _jsonOptions);
        File.WriteAllText(path, json);
    }

    private static string SanitizeFolderName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "Untitled" : safe.Trim();
    }
}
