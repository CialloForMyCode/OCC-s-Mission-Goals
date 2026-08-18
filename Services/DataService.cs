using System.IO;
using System.Text.Json;
using OCCMissionGoals.Models;

namespace OCCMissionGoals.Services;

/// <summary>
/// 管理数据文件的读写。通过 SetFilePath() 指定路径后 Load()/Save()。
/// </summary>
public static class DataService
{
    private static string? _path;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>当前加载的数据文件。</summary>
    public static DataFile Current { get; private set; } = new();

    /// <summary>设置数据文件路径（通常在 Load 之前调用）。</summary>
    public static void SetFilePath(string path)
    {
        _path = path;
    }

    /// <summary>获取当前数据文件路径，可能为 null。</summary>
    public static string? GetFilePath() => _path;

    /// <summary>从当前路径加载数据（加跨进程锁，避免读到写了一半的文件）。</summary>
    public static void Load()
    {
        if (_path == null) return;
        using (FileLock.Acquire())
        {
            LoadCore();
        }
    }

    /// <summary>锁内从当前路径加载数据。</summary>
    private static void LoadCore()
    {
        if (_path == null) return;

        var dir = Path.GetDirectoryName(_path);
        if (dir != null) Directory.CreateDirectory(dir);

        if (!File.Exists(_path))
        {
            Current = new DataFile();
            return;
        }

        var json = File.ReadAllText(_path);
        Current = JsonSerializer.Deserialize<DataFile>(json, _jsonOptions) ?? new DataFile();
    }

    /// <summary>GUI 内部保存时为 true，用于抑制文件监视器。</summary>
    internal static volatile bool IsInternalSave;

    /// <summary>将当前数据写回当前路径（加跨进程锁）。</summary>
    public static void Save()
    {
        if (_path == null) return;
        using (FileLock.Acquire())
        {
            WriteCurrentCore();
        }
    }

    /// <summary>锁内将当前数据写回当前路径。</summary>
    private static void WriteCurrentCore()
    {
        if (_path == null) return;

        var dir = Path.GetDirectoryName(_path);
        if (dir != null) Directory.CreateDirectory(dir);

        IsInternalSave = true;
        var json = JsonSerializer.Serialize(Current, _jsonOptions);
        File.WriteAllText(_path, json);
        IsInternalSave = false;
    }

    /// <summary>
    /// 原子添加一条条目：在跨进程锁内重新读取磁盘最新数据、分配编号、追加后写回。
    /// 防止 GUI 与 CLI 并发添加时互相覆盖或编号重复。
    /// </summary>
    public static void AddEntryAtomic(GoalEntry entry)
    {
        if (_path == null) return;
        using (FileLock.Acquire())
        {
            // 1. 基于磁盘最新 project.json 分配编号
            ProjectService.AssignEntryIdCore(entry);

            // 2. 重新读取磁盘最新数据，再追加，避免覆盖外部进程刚写入的内容
            LoadCore();
            Current.Unfinished.Add(entry);

            // 3. 写回
            WriteCurrentCore();
        }
    }

    /// <summary>
    /// 将当前项目所有版本的数据合并返回，不影响 Current。
    /// </summary>
    public static DataFile ReadAllVersions(string projectDir)
    {
        var merged = new DataFile();
        if (string.IsNullOrEmpty(projectDir)) return merged;

        var versionsDir = ProjectService.GetVersionsDir(projectDir);
        if (!Directory.Exists(versionsDir)) return merged;

        foreach (var file in Directory.GetFiles(versionsDir, "*.json"))
        {
            if (!File.Exists(file)) continue;
            var json = File.ReadAllText(file);
            var data = JsonSerializer.Deserialize<DataFile>(json, _jsonOptions);
            if (data != null)
            {
                merged.Unfinished.AddRange(data.Unfinished);
                merged.Finished.AddRange(data.Finished);
            }
        }
        return merged;
    }

    /// <summary>
    /// 在所有版本文件中查找条目（先按 entry.Version 定位，找不到再扫全部），
    /// 执行修改后保存。如果版本号变更则跨文件搬迁。
    /// </summary>
    public static bool SaveToEntryVersion(string projectDir, GoalEntry entry,
        Action<DataFile, GoalEntry> modify)
    {
        using (FileLock.Acquire())
        {
            return SaveToEntryVersionCore(projectDir, entry, modify);
        }
    }

    private static bool SaveToEntryVersionCore(string projectDir, GoalEntry entry,
        Action<DataFile, GoalEntry> modify)
    {
        var versionsDir = ProjectService.GetVersionsDir(projectDir);
        if (!Directory.Exists(versionsDir)) return false;

        // 1. 在所有版本文件中定位条目
        string? foundFile = null;
        DataFile? foundData = null;
        GoalEntry? target = null;

        // 先按 entry.Version 找（快速路径）
        var hintFile = string.IsNullOrEmpty(entry.Version) ? null
            : Path.Combine(versionsDir, entry.Version + ".json");

        foreach (var file in GetCandidateFiles(versionsDir, hintFile))
        {
            if (!File.Exists(file)) continue;
            var json = File.ReadAllText(file);
            var data = JsonSerializer.Deserialize<DataFile>(json, _jsonOptions);
            if (data == null) continue;

            target = data.Unfinished.FirstOrDefault(e => e.Title == entry.Title)
                  ?? data.Finished.FirstOrDefault(e => e.Title == entry.Title);
            if (target != null)
            {
                foundFile = file;
                foundData = data;
                break;
            }
        }

        if (foundFile == null || foundData == null || target == null) return false;

        var oldVersion = Path.GetFileNameWithoutExtension(foundFile);
        modify(foundData, target);

        // 2. 如果版本变了，跨文件搬迁
        var newVersion = target.Version;
        if (!string.IsNullOrEmpty(newVersion) && newVersion != oldVersion)
        {
            bool inUnfinished = foundData.Unfinished.Contains(target);
            if (inUnfinished) foundData.Unfinished.Remove(target);
            else foundData.Finished.Remove(target);
            WriteVersionFileCore(foundFile, foundData);

            var newFile = Path.Combine(versionsDir, newVersion + ".json");
            DataFile newData;
            if (File.Exists(newFile))
            {
                var newJson = File.ReadAllText(newFile);
                newData = JsonSerializer.Deserialize<DataFile>(newJson, _jsonOptions) ?? new DataFile();
            }
            else { newData = new DataFile(); }

            if (inUnfinished) newData.Unfinished.Add(target);
            else newData.Finished.Add(target);
            WriteVersionFileCore(newFile, newData);
        }
        else
        {
            WriteVersionFileCore(foundFile, foundData);
        }

        // 3. 同步更新调用方持有的 entry 引用
        entry.Title = target.Title;
        entry.Version = target.Version;
        entry.Severity = target.Severity;
        entry.Brief = target.Brief;
        entry.Detail = target.Detail;
        entry.Deadline = target.Deadline;
        entry.CompletedAt = target.CompletedAt;
        entry.ChangeDemand = target.ChangeDemand;
        entry.IsFavorited = target.IsFavorited;
        entry.Type = target.Type;
        entry.RelatedFiles = target.RelatedFiles;

        return true;
    }

    /// <summary>hintFile 排最前面，其余文件按名称排序。</summary>
    private static IEnumerable<string> GetCandidateFiles(string dir, string? hintFile)
    {
        var files = Directory.GetFiles(dir, "*.json");
        if (hintFile != null) yield return hintFile;
        foreach (var f in files)
            if (f != hintFile) yield return f;
    }

    /// <summary>将数据写入指定版本文件（加跨进程锁）。</summary>
    public static void SaveVersionFile(string file, DataFile data)
    {
        using (FileLock.Acquire())
        {
            WriteVersionFileCore(file, data);
        }
    }

    private static void WriteVersionFileCore(string file, DataFile data)
    {
        var dir = Path.GetDirectoryName(file);
        if (dir != null) Directory.CreateDirectory(dir);
        IsInternalSave = true;
        File.WriteAllText(file, JsonSerializer.Serialize(data, _jsonOptions));
        IsInternalSave = false;
    }
}
