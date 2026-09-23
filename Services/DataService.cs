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

    /// <summary>清空数据服务状态（关闭/删除项目后调用）：清除文件路径与内存数据。</summary>
    public static void Reset()
    {
        _path = null;
        Current = new DataFile();
    }

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

    /// <summary>最近一次由本进程写盘后的文件时间戳（UTC）。文件监视器用它区分"自己写入的回声"和真正的外部改动。</summary>
    internal static DateTime? LastInternalWriteUtc;

    /// <summary>
    /// 判断 path 时间戳是否就是本进程刚写下的那一版：是则说明监视器事件是自身写入的回声
    /// （IsInternalSave 往往在事件到达前就已复位，只能靠时间戳判定）。
    /// </summary>
    internal static bool IsInternalWriteEcho(string path)
    {
        var stamp = LastInternalWriteUtc;
        if (stamp == null || string.IsNullOrEmpty(path)) return false;

        try
        {
            return File.Exists(path) && File.GetLastWriteTimeUtc(path) == stamp.Value;
        }
        catch
        {
            return false;
        }
    }

    private static void MarkInternalWrite(string path)
    {
        try { LastInternalWriteUtc = File.GetLastWriteTimeUtc(path); }
        catch { LastInternalWriteUtc = null; }
    }

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
        MarkInternalWrite(_path);
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
            entry.CreatedAt = entry.UpdatedAt = DateTime.Now;
            if (entry.Status == EntryStatus.Finished && entry.CompletedAt == default)
                entry.CompletedAt = DateTime.Now;
            Current.Entries.Add(entry);

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
                merged.Entries.AddRange(data.Entries);
            }
        }
        return merged;
    }

    /// <summary>
    /// 读取所有「计入统计的版本」的条目，并带上条目所属的版本名（版本文件名去掉 .json）。
    /// 版本归属来自文件本身 —— 条目已不再自带版本字段。
    /// </summary>
    public static List<(GoalEntry Entry, string Version)> ReadStatsVersions(string projectDir)
    {
        var list = new List<(GoalEntry, string)>();
        if (string.IsNullOrEmpty(projectDir)) return list;

        var versionsDir = ProjectService.GetVersionsDir(projectDir);
        if (!Directory.Exists(versionsDir)) return list;

        foreach (var file in Directory.GetFiles(versionsDir, "*.json"))
        {
            var version = Path.GetFileNameWithoutExtension(file);
            if (!ProjectService.IsStatsVersion(version)) continue;

            string json;
            try { json = File.ReadAllText(file); }
            catch { continue; }

            var data = JsonSerializer.Deserialize<DataFile>(json, _jsonOptions);
            if (data == null) continue;

            foreach (var entry in data.Entries)
                list.Add((entry, version));
        }
        return list;
    }

    /// <summary>
    /// 在当前项目所有版本中重命名（newTag 非空）或移除（newTag 为 null）某类型标签。
    /// 对每个条目按大小写不敏感匹配并更新其 Type 列表。
    /// </summary>
    public static void UpdateTypeTagAcrossVersions(string projectDir, string oldTag, string? newTag)
    {
        if (string.IsNullOrEmpty(projectDir)) return;
        var versionsDir = ProjectService.GetVersionsDir(projectDir);
        if (!Directory.Exists(versionsDir)) return;

        using (FileLock.Acquire())
        {
            foreach (var file in Directory.GetFiles(versionsDir, "*.json"))
            {
                string json;
                try { json = File.ReadAllText(file); }
                catch { continue; }

                var data = JsonSerializer.Deserialize<DataFile>(json, _jsonOptions);
                if (data == null) continue;

                var changed = false;
                foreach (var entry in data.Entries)
                {
                    for (var i = entry.Type.Count - 1; i >= 0; i--)
                    {
                        if (!string.Equals(entry.Type[i], oldTag, StringComparison.OrdinalIgnoreCase))
                            continue;

                        changed = true;
                        if (newTag == null) entry.Type.RemoveAt(i);
                        else entry.Type[i] = newTag;
                    }
                }

                if (changed)
                {
                    IsInternalSave = true;
                    try { File.WriteAllText(file, JsonSerializer.Serialize(data, _jsonOptions)); }
                    finally { IsInternalSave = false; }
                }
            }
        }
    }

    /// <summary>
    /// 在所有版本文件中查找条目，执行修改后写回它所在的那个版本文件。
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

        foreach (var file in GetCandidateFiles(versionsDir, null))
        {
            if (!File.Exists(file)) continue;
            var json = File.ReadAllText(file);
            var data = JsonSerializer.Deserialize<DataFile>(json, _jsonOptions);
            if (data == null) continue;

            target = FindEntry(data, entry);
            if (target != null)
            {
                foundFile = file;
                foundData = data;
                break;
            }
        }

        if (foundFile == null || foundData == null || target == null) return false;

        modify(foundData, target);
        target.UpdatedAt = DateTime.Now;

        // 2. 写回
        // 条目不再自带版本字段：版本归属完全由它所在的版本文件决定，所以直接写回原文件。
        WriteVersionFileCore(foundFile, foundData);

        // 3. 同步更新调用方持有的 entry 引用
        entry.Title = target.Title;
        entry.Status = target.Status;
        entry.Severity = target.Severity;
        entry.Brief = target.Brief;
        entry.CompletedAt = target.CompletedAt;
        entry.CreatedAt = target.CreatedAt;
        entry.UpdatedAt = target.UpdatedAt;
        entry.IsFavorited = target.IsFavorited;
        entry.Type = target.Type;
        entry.RelatedFiles = target.RelatedFiles;

        return true;
    }

    /// <summary>
    /// 在数据文件中定位条目：优先按 Id（条目唯一编号），Id 为空或找不到时回退到标题精确匹配。
    /// </summary>
    private static GoalEntry? FindEntry(DataFile data, GoalEntry entry)
    {
        if (!string.IsNullOrEmpty(entry.Id))
        {
            var byId = data.Entries.FirstOrDefault(e => e.Id == entry.Id);
            if (byId != null) return byId;
        }

        return data.Entries.FirstOrDefault(e => string.Equals(e.Title, entry.Title, StringComparison.Ordinal));
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
        MarkInternalWrite(file);
    }
}
