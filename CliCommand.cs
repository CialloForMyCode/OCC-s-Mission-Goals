using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using OCCMissionGoals.Models;
using OCCMissionGoals.Services;
using SysPath = System.IO.Path;

namespace OCCMissionGoals.Cli;

/// <summary>
/// CLI — 命令行接口。用法：OCCMissionGoals.exe [-p <项目>] <命令> [参数]
/// </summary>
public static class CliCommand
{
    private static readonly JsonSerializerOptions _json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly JsonSerializerOptions _jsonInsensitive = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // ========================================================
    // ENTRY
    // ========================================================
    public static int Run(string[] args)
    {
        if (args.Length == 0) { PrintHelp(); return 0; }

        string? project = null;
        string? versionSwitch = null;
        string? mainCmd = null;
        var cmdArgs = new List<string>();
        bool versionIsCommand = false;

        int i = 0;
        while (i < args.Length)
        {
            var a = args[i];

            if (a == "-p" || a == "--project")
            {
                if (++i >= args.Length) { Err("缺少 -p 参数"); return 1; }
                project = args[i]; i++; continue;
            }
            if (a.StartsWith("--project="))
            {
                project = a.Split('=', 2)[1]; i++; continue;
            }

            if (a == "-v" || a == "--version")
            {
                if (++i >= args.Length) { Err("缺少 -v 参数"); return 1; }
                var val = args[i];
                var kw = val.ToLowerInvariant();
                if (kw is "iterate" or "delete" or "archive")
                {
                    if (mainCmd != null) { Err("命令冲突"); return 1; }
                    mainCmd = kw;
                    versionIsCommand = true;
                    i++;
                    while (i < args.Length) { cmdArgs.Add(args[i]); i++; }
                    break;
                }
                else
                {
                    versionSwitch = val;
                    i++; continue;
                }
            }

            if (a is "-a" or "--add")
            {
                if (mainCmd != null) { Err("命令冲突"); return 1; }
                mainCmd = "add"; i++;
                while (i < args.Length) { cmdArgs.Add(args[i]); i++; }
                break;
            }
            if (a is "-D" or "--delete")
            {
                if (mainCmd != null) { Err("命令冲突"); return 1; }
                mainCmd = "delete"; i++;
                while (i < args.Length) { cmdArgs.Add(args[i]); i++; }
                break;
            }
            if (a is "-c" or "--check")
            {
                if (mainCmd != null) { Err("命令冲突"); return 1; }
                mainCmd = "check"; i++;
                while (i < args.Length) { cmdArgs.Add(args[i]); i++; }
                break;
            }
            if (a is "-d" or "--done")
            {
                if (mainCmd != null) { Err("命令冲突"); return 1; }
                mainCmd = "done"; i++;
                while (i < args.Length) { cmdArgs.Add(args[i]); i++; }
                break;
            }
            if (a is "-u" or "--undone")
            {
                if (mainCmd != null) { Err("命令冲突"); return 1; }
                mainCmd = "undone"; i++;
                while (i < args.Length) { cmdArgs.Add(args[i]); i++; }
                break;
            }
            if (a is "-f" or "--favorited")
            {
                if (mainCmd != null) { Err("命令冲突"); return 1; }
                mainCmd = "favorited"; i++;
                while (i < args.Length) { cmdArgs.Add(args[i]); i++; }
                break;
            }
            if (a is "-l" or "--list")
            {
                if (mainCmd != null) { Err("命令冲突"); return 1; }
                mainCmd = "list"; i++;
                while (i < args.Length) { cmdArgs.Add(args[i]); i++; }
                break;
            }

            if (a is "help" or "--help" or "-h") { PrintHelp(); return 0; }

            if (mainCmd == null) { cmdArgs.Add(a); }
            i++;
        }

        try
        {
            EnsureProject(ref project);

            if (versionSwitch != null && !versionIsCommand)
                EnsureVersion(ref versionSwitch);

            if (mainCmd == null)
            {
                if (versionSwitch != null)
                {
                    OutJson(new { ok = true, version = ProjectService.CurrentProject?.CurrentVersion ?? "" });
                    return 0;
                }
                if (cmdArgs.Count > 0)
                    return DispatchLegacy(cmdArgs, project, versionSwitch);
                PrintHelp();
                return 0;
            }

            return mainCmd switch
            {
                "add"        => EntryAdd(cmdArgs),
                "delete"     => EntryDelete(cmdArgs),
                "check"      => EntryCheck(cmdArgs),
                "done"       => EntryDone(cmdArgs),
                "undone"     => EntryUndone(cmdArgs),
                "favorited"  => EntryFavorite(cmdArgs),
                "list"       => EntryList(cmdArgs),
                "iterate"    => VersionIterate(),
                "delete-ver" => VersionDelete(cmdArgs),
                "archive"    => VersionArchive(cmdArgs),
                _            => 0
            };
        }
        catch (Exception ex)
        {
            Err($"错误: {ex.Message}");
            return 1;
        }
    }

    // ========================================================
    // NEW COMMANDS
    // ========================================================

    static int EntryAdd(List<string> args)
    {
        if (args.Count == 0) { Err("用法: -a {Title=\"...\", Severity=\"...\", ...}"); return 1; }
        var raw = string.Join(" ", args);
        var json = ParseCustomDataFormat(raw);
        GoalEntry? entry;
        try { entry = JsonSerializer.Deserialize<GoalEntry>(json, _jsonInsensitive); }
        catch (Exception ex) { Err($"解析失败: {ex.Message}"); return 1; }
        if (entry == null || string.IsNullOrWhiteSpace(entry.Title)) { Err("缺少 Title"); return 1; }

        DataService.AddEntryAtomic(entry);
        OutJson(EntryToDict(entry, 0));
        return 0;
    }

    static int EntryDelete(List<string> args)
    {
        using (FileLock.Acquire())
        {
            var (entry, data, file) = FindEntryById(args);
            if (entry == null) return 1;
            if (data.Unfinished.Contains(entry)) data.Unfinished.Remove(entry);
            else data.Finished.Remove(entry);
            SaveVersionFileCore(file, data);
            OutJson(new { ok = true, id = entry.Id, title = entry.Title, deleted = true });
            return 0;
        }
    }

    static int EntryCheck(List<string> args)
    {
        var (entry, data, file) = FindEntryById(args);
        if (entry == null) return 1;
        bool isFinished = data.Finished.Contains(entry);
        var dict = EntryToDict(entry, 0);
        dict["status"] = isFinished ? "finished" : "unfinished";
        dict["versionFile"] = SysPath.GetFileNameWithoutExtension(file);
        OutJson(dict);
        return 0;
    }

    static int EntryDone(List<string> args)
    {
        using (FileLock.Acquire())
        {
            var (entry, data, file) = FindEntryById(args);
            if (entry == null) return 1;
            if (data.Finished.Contains(entry)) { Err("条目已完成。"); return 1; }
            data.Unfinished.Remove(entry);
            entry.CompletedAt = DateTime.Today;
            data.Finished.Add(entry);
            SaveVersionFileCore(file, data);
            OutJson(new { ok = true, id = entry.Id, title = entry.Title, status = "finished" });
            return 0;
        }
    }

    static int EntryUndone(List<string> args)
    {
        using (FileLock.Acquire())
        {
            var (entry, data, file) = FindEntryById(args);
            if (entry == null) return 1;
            if (data.Unfinished.Contains(entry)) { Err("条目未完成。"); return 1; }
            data.Finished.Remove(entry);
            data.Unfinished.Insert(0, entry);
            SaveVersionFileCore(file, data);
            OutJson(new { ok = true, id = entry.Id, title = entry.Title, status = "unfinished" });
            return 0;
        }
    }

    static int EntryFavorite(List<string> args)
    {
        if (args.Count < 2) { Err("用法: -f <编号> true|false"); return 1; }
        var id = args[0];
        if (!bool.TryParse(args[1], out var fav)) { Err("第二个参数必须是 true 或 false"); return 1; }
        using (FileLock.Acquire())
        {
            var (entry, data, file) = FindEntryByIdRaw(id);
            if (entry == null) return 1;
            entry.IsFavorited = fav;
            SaveVersionFileCore(file, data);
            OutJson(new { ok = true, id = entry.Id, title = entry.Title, isFavorited = entry.IsFavorited });
            return 0;
        }
    }

    static int EntryList(List<string> args)
    {
        var data = DataService.ReadAllVersions(ProjectService.CurrentProjectDir!);
        var all = data.Unfinished.Select(e => (e, status: "unfinished"))
            .Concat(data.Finished.Select(e => (e, status: "finished")))
            .Select((x, i) =>
            {
                var dict = EntryToDict(x.e, i + 1);
                dict["status"] = x.status;
                return dict;
            });
        OutJson(all);
        return 0;
    }

    // ========================================================
    // VERSION COMMANDS
    // ========================================================

    static int VersionIterate()
    {
        var cur = ProjectService.CurrentProject?.CurrentVersion ?? "0.1.0-alpha.0";
        var dashIdx = cur.LastIndexOf('-');
        string newVer;
        if (dashIdx >= 0 && int.TryParse(cur[(dashIdx + 1)..], out int n))
            newVer = cur[..(dashIdx + 1)] + (n + 1);
        else
            newVer = cur + "-1";
        ProjectService.UpdateVersion(newVer);
        OutJson(new { ok = true, previous = cur, current = newVer });
        return 0;
    }

    static int VersionDelete(List<string> args)
    {
        if (args.Count == 0) { Err("用法: -v Delete <版本号>"); return 1; }
        var ver = args[0];
        if (ver.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) ver = ver[..^5];
        var file = SysPath.Combine(ProjectService.GetVersionsDir(ProjectService.CurrentProjectDir!), ver + ".json");
        if (!File.Exists(file)) { Err($"版本文件不存在: {ver}.json"); return 1; }
        if (ver == (ProjectService.CurrentProject?.CurrentVersion ?? "")) { Err("不能删除当前版本。"); return 1; }
        ProjectService.DeleteVersion(ver + ".json");
        OutJson(new { ok = true, version = ver, deleted = true });
        return 0;
    }

    static int VersionArchive(List<string> args)
    {
        if (args.Count == 0) { Err("用法: -v Archive <版本号>"); return 1; }
        var ver = args[0];
        if (ver.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) ver = ver[..^5];
        var versionsDir = ProjectService.GetVersionsDir(ProjectService.CurrentProjectDir!);
        var src = SysPath.Combine(versionsDir, ver + ".json");
        if (!File.Exists(src)) { Err($"版本文件不存在: {ver}.json"); return 1; }
        if (ver == (ProjectService.CurrentProject?.CurrentVersion ?? "")) { Err("不能归档当前版本。"); return 1; }

        // 检查版本内是否全部条目均已完成
        try
        {
            var json = File.ReadAllText(src);
            var data = JsonSerializer.Deserialize<DataFile>(json);
            if (data != null && data.Unfinished.Count > 0)
            {
                Err($"版本 {ver} 中仍有 {data.Unfinished.Count} 条未完成条目，无法归档。");
                return 1;
            }
            if (data == null || data.Finished.Count == 0)
            {
                Err($"版本 {ver} 中无已完成条目，无法归档。");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Err($"读取版本文件失败: {ex.Message}");
            return 1;
        }
        var archiveDir = SysPath.Combine(versionsDir, "archive");
        Directory.CreateDirectory(archiveDir);
        var dest = SysPath.Combine(archiveDir, ver + ".json");
        if (File.Exists(dest)) { Err($"归档目录中已存在 {ver}.json"); return 1; }
        File.Move(src, dest);
        OutJson(new { ok = true, version = ver, archived = true });
        return 0;
    }

    // ========================================================
    // LEGACY DISPATCH
    // ========================================================
    static int DispatchLegacy(List<string> args, string? project, string? version)
    {
        var cmd = args[0];
        var sub = args.Count > 1 ? args[1] : "";
        switch (cmd)
        {
            case "help": case "--help": case "-h": PrintHelp(); return 0;
            case "project":
                return sub switch
                {
                    "list" => ProjectList(), "info" => ProjectInfo(args, project), _ => Unknown(cmd, sub)
                };
            case "version":
                EnsureProject(ref project);
                return sub switch
                {
                    "list" => VersionList(), "current" => VersionCurrent(), _ => Unknown(cmd, sub)
                };
            case "entry":
                EnsureProject(ref project);
                return sub switch
                {
                    "list" => EntryListOld(args), "show" => EntryShowOld(args),
                    "add" => WrapWriteLegacy(args, version, EntryAddOld),
                    "edit" => WrapWriteLegacy(args, version, EntryEditOld),
                    "done" => WrapWriteLegacy(args, version, EntryDoneOld),
                    "undo" => WrapWriteLegacy(args, version, EntryUndoOld),
                    "delete" => WrapWriteLegacy(args, version, EntryDeleteOld),
                    _ => Unknown(cmd, sub)
                };
            default: Err($"未知命令: {cmd}"); return 1;
        }
    }

    // ========================================================
    // LEGACY HANDLERS
    // ========================================================
    static int ProjectList()
    {
        var dirs = ProjectService.GetProjectDirectories();
        OutJson(dirs.Select(dir =>
        {
            var cfg = ReadProjectConfig(dir);
            return new { name = cfg?.Name ?? SysPath.GetFileName(dir), description = cfg?.Description ?? "", currentVersion = cfg?.CurrentVersion ?? "", path = dir };
        }));
        return 0;
    }
    static int ProjectInfo(List<string> args, string? project)
    {
        EnsureProject(ref project);
        var dir = ProjectService.CurrentProjectDir!;
        var cfg = ProjectService.CurrentProject!;
        var files = ProjectService.GetVersionFiles(dir);
        OutJson(new { name = cfg.Name, description = cfg.Description, currentVersion = cfg.CurrentVersion, projectNumber = cfg.ProjectNumber, nextEntryId = cfg.NextEntryId, createdAt = cfg.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"), path = dir, versions = files.Select(f => f.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? f[..^5] : f) });
        return 0;
    }
    static int VersionList()
    {
        var dir = ProjectService.CurrentProjectDir!;
        var files = ProjectService.GetVersionFiles(dir);
        var cur = ProjectService.CurrentProject?.CurrentVersion ?? "";
        OutJson(files.Select(f => { var v = f.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? f[..^5] : f; return new { version = v, current = v == cur }; }));
        return 0;
    }
    static int VersionCurrent() { OutJson(new { version = ProjectService.CurrentProject?.CurrentVersion ?? "" }); return 0; }

    static int EntryListOld(List<string> args)
    {
        var type = Opt(args, "--type", "a");
        var search = Opt(args, "--search", "");
        var data = DataService.ReadAllVersions(ProjectService.CurrentProjectDir!);
        var list = new List<GoalEntry>();
        if (type == "u" || type == "a") list.AddRange(data.Unfinished);
        if (type == "f" || type == "a") list.AddRange(data.Finished);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.ToLowerInvariant();
            list = list.Where(e => e.Title.Contains(q, StringComparison.OrdinalIgnoreCase) || e.Brief.Contains(q, StringComparison.OrdinalIgnoreCase) || e.Detail.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        OutJson(list.Select((e, idx) => EntryToDict(e, idx + 1)));
        return 0;
    }
    static int EntryShowOld(List<string> args)
    {
        var index = ParseIndex(args); var type = Opt(args, "--type", "u");
        var data = DataService.ReadAllVersions(ProjectService.CurrentProjectDir!);
        var list = type == "f" ? data.Finished : data.Unfinished;
        if (index < 1 || index > list.Count) { Err($"列表里没有索引 {index}（共 {list.Count} 条）"); return 1; }
        var entry = list[index - 1]; var dict = EntryToDict(entry, index);
        dict["type"] = type == "f" ? "finished" : "unfinished"; OutJson(dict); return 0;
    }
    static int WrapWriteLegacy(List<string> args, string? version, Func<List<string>, string, int> fn) { EnsureVersion(ref version); return fn(args, version!); }

    static int EntryAddOld(List<string> args, string version)
    {
        var title = Opt(args, "--title", "");
        if (string.IsNullOrWhiteSpace(title)) { Err("缺少 --title"); return 1; }
        var sevStr = Opt(args, "--severity", "General");
        var brief = Opt(args, "--brief", ""); var detail = Opt(args, "--detail", "");
        var dlStr = Opt(args, "--deadline", "");
        var ver = Opt(args, "--entry-version", version);
        if (!Enum.TryParse<GoalSeverity>(sevStr, true, out var sev)) sev = GoalSeverity.General;
        var deadline = DateTime.Today.AddDays(7);
        if (!string.IsNullOrWhiteSpace(dlStr)) DateTime.TryParse(dlStr, out deadline);
        var entry = new GoalEntry { Title = title, Severity = sev, Brief = brief, Detail = detail, Deadline = deadline, Version = ver };
        DataService.AddEntryAtomic(entry);
        OutJson(EntryToDict(entry, DataService.Current.Unfinished.Count));
        return 0;
    }
    static int EntryEditOld(List<string> args, string version)
    {
        var index = ParseIndex(args); var type = Opt(args, "--type", "u");
        var data = DataService.ReadAllVersions(ProjectService.CurrentProjectDir!);
        var list = type == "f" ? data.Finished : data.Unfinished;
        if (index < 1 || index > list.Count) { Err($"列表里没有索引 {index}（共 {list.Count} 条）"); return 1; }
        var entry = list[index - 1];
        var title = Opt(args, "--title", ""); var sevStr = Opt(args, "--severity", "");
        var brief = Opt(args, "--brief", ""); var detail = Opt(args, "--detail", "");
        var dlStr = Opt(args, "--deadline", ""); var verStr = Opt(args, "--entry-version", "");
        DataService.SaveToEntryVersion(ProjectService.CurrentProjectDir!, entry, (d, target) =>
        {
            if (!string.IsNullOrWhiteSpace(title)) target.Title = title;
            if (!string.IsNullOrWhiteSpace(sevStr) && Enum.TryParse<GoalSeverity>(sevStr, true, out var sev)) target.Severity = sev;
            if (!string.IsNullOrWhiteSpace(brief)) target.Brief = brief;
            if (!string.IsNullOrWhiteSpace(detail)) target.Detail = detail;
            if (!string.IsNullOrWhiteSpace(dlStr) && DateTime.TryParse(dlStr, out var dl)) target.Deadline = dl;
            if (!string.IsNullOrWhiteSpace(verStr)) target.Version = verStr;
        });
        OutJson(EntryToDict(entry, index)); return 0;
    }
    static int EntryDoneOld(List<string> args, string version)
    {
        var index = ParseIndex(args);
        var data = DataService.ReadAllVersions(ProjectService.CurrentProjectDir!);
        if (index < 1 || index > data.Unfinished.Count) { Err($"未完成列表里没有索引 {index}（共 {data.Unfinished.Count} 条）"); return 1; }
        var entry = data.Unfinished[index - 1];
        DataService.SaveToEntryVersion(ProjectService.CurrentProjectDir!, entry, (d, target) => { d.Unfinished.Remove(target); target.CompletedAt = DateTime.Today; d.Finished.Add(target); });
        OutJson(new { ok = true, title = entry.Title }); return 0;
    }
    static int EntryUndoOld(List<string> args, string version)
    {
        var index = ParseIndex(args);
        var data = DataService.ReadAllVersions(ProjectService.CurrentProjectDir!);
        if (index < 1 || index > data.Finished.Count) { Err($"已完成列表里没有索引 {index}（共 {data.Finished.Count} 条）"); return 1; }
        var entry = data.Finished[index - 1];
        DataService.SaveToEntryVersion(ProjectService.CurrentProjectDir!, entry, (d, target) => { d.Finished.Remove(target); d.Unfinished.Insert(0, target); });
        OutJson(new { ok = true, title = entry.Title }); return 0;
    }
    static int EntryDeleteOld(List<string> args, string version)
    {
        var index = ParseIndex(args); var type = Opt(args, "--type", "u");
        var data = DataService.ReadAllVersions(ProjectService.CurrentProjectDir!);
        var list = type == "f" ? data.Finished : data.Unfinished;
        if (index < 1 || index > list.Count) { Err($"列表里没有索引 {index}（共 {list.Count} 条）"); return 1; }
        var entry = list[index - 1];
        DataService.SaveToEntryVersion(ProjectService.CurrentProjectDir!, entry, (d, target) => { if (type == "f") d.Finished.Remove(target); else d.Unfinished.Remove(target); });
        OutJson(new { ok = true, title = entry.Title, deleted = true }); return 0;
    }

    // ========================================================
    // HELPERS
    // ========================================================

    static string ParseCustomDataFormat(string input)
    {
        var sb = new StringBuilder();
        bool str = false;
        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (c == '"' && (i == 0 || input[i - 1] != '\\')) str = !str;
            sb.Append(!str && c == '=' ? ':' : c);
        }
        return Regex.Replace(sb.ToString(), @"([\{,])\s*([a-zA-Z_]\w*)\s*:", @"$1""$2"":");
    }

    static (GoalEntry? entry, DataFile data, string file) FindEntryById(List<string> args)
    {
        if (args.Count == 0) { Err("缺少条目编号。"); return (null, new(), ""); }
        return FindEntryByIdRaw(args[0]);
    }

    static (GoalEntry? entry, DataFile data, string file) FindEntryByIdRaw(string id)
    {
        var versionsDir = ProjectService.GetVersionsDir(ProjectService.CurrentProjectDir!);
        if (!Directory.Exists(versionsDir)) { Err("版本目录不存在。"); return (null, new(), ""); }
        foreach (var file in Directory.GetFiles(versionsDir, "*.json"))
        {
            if (!File.Exists(file)) continue;
            try
            {
                var json = File.ReadAllText(file);
                var data = JsonSerializer.Deserialize<DataFile>(json, _jsonInsensitive);
                if (data == null) continue;
                var entry = data.Unfinished.FirstOrDefault(e => e.Id == id)
                         ?? data.Finished.FirstOrDefault(e => e.Id == id);
                if (entry != null) return (entry, data, file);
            }
            catch { }
        }
        Err($"未找到编号为 {id} 的条目。");
        return (null, new(), "");
    }

    static void PrintHelp()
    {
        Console.WriteLine(@"OCC CLI — 命令行接口

用法:
  OCCMissionGoals.exe [-p <项目>] <命令> [参数]

命令:
  -a, --add  <数据>          添加条目
      格式: {Title=""xxx"", Severity=""Fatal"", Brief=""..."", Detail=""..."",
             IsFavorited=false, Version=""0.1.0"", Type=[""Bug""],
             RelatedFiles={""P:\f.sb""=[10,5,""Class.Func""]}}

  -c, --check    <编号>      查看条目 (如 -c 001000001)
  -d, --done     <编号>      完成条目
  -u, --undone   <编号>      取消完成
  -D, --delete   <编号>      删除条目
  -f, --favorited <编号> <true|false>  收藏/取消收藏
  -l, --list                 列出所有条目

  -v <版本号>                切换到指定版本
  -v Iterate                 版本迭代
  -v Delete  <版本号>        删除版本
  -v Archive <版本号>        归档版本 -> versions/archive/（须全部条目已完成）

全局:
  -p, --project <名称>       选择项目

输出: JSON (stdout)  错误/帮助 (stderr)");
    }

    static int Unknown(string cmd, string sub) { Err($"未知命令: {cmd} {sub}。"); return 1; }
    static void Err(string msg) => Console.Error.WriteLine(msg);
    static void OutJson(object data) => Console.WriteLine(JsonSerializer.Serialize(data, _json));

    static Dictionary<string, object> EntryToDict(GoalEntry e, int index) => new()
    {
        ["index"] = index, ["id"] = e.Id, ["title"] = e.Title,
        ["severity"] = e.Severity.ToString(), ["severityLabel"] = SeverityLabel(e.Severity),
        ["brief"] = e.Brief,
        ["detail"] = e.Detail.Length > 200 ? e.Detail[..200] + "..." : e.Detail,
        ["deadline"] = e.Deadline.Year < 2 ? "" : e.Deadline.ToString("yyyy-MM-dd"),
        ["completedAt"] = e.CompletedAt.Year < 2 ? "" : e.CompletedAt.ToString("yyyy-MM-dd"),
        ["changeDemand"] = e.ChangeDemand, ["isFavorited"] = e.IsFavorited,
        ["version"] = e.Version,
        ["relatedFiles"] = e.RelatedFiles.Select(f => new { path = f.Path, line = f.Line, column = f.Column, function = f.Function }).ToList()
    };

    static string SeverityLabel(GoalSeverity s) => s switch
    {
        GoalSeverity.Fatal => "致命", GoalSeverity.Severe => "严重",
        GoalSeverity.General => "一般", GoalSeverity.Patch => "补丁",
        GoalSeverity.Update => "更新", _ => "未知"
    };

    static string Opt(List<string> args, string name, string defaultValue)
    {
        for (int i = 0; i < args.Count; i++)
        {
            var a = args[i];
            if ((a == name || a.StartsWith(name + "=")) && i + 1 < args.Count)
            {
                if (a.Contains('=')) return a.Split('=', 2)[1];
                return args[i + 1];
            }
        }
        return defaultValue;
    }

    static int ParseIndex(List<string> args)
    {
        for (int i = 0; i < args.Count; i++)
            if (int.TryParse(args[i], out var idx) && idx > 0) return idx;
        Err("缺少索引参数。"); return -1;
    }

    static void EnsureProject(ref string? name)
    {
        if (name != null) { var dir = FindProjectDir(name); if (dir == null) throw new Exception($"未找到项目: {name}"); ProjectService.OpenProject(dir); return; }
        if (ProjectService.TryRestoreLastProject()) return;
        var dirs = ProjectService.GetProjectDirectories();
        if (dirs.Count > 0) { ProjectService.OpenProject(dirs[0]); return; }
        throw new Exception("没有打开的项目。请用 -p 指定。");
    }

    static void EnsureVersion(ref string? version)
    {
        if (version != null) { var cur = ProjectService.CurrentProject?.CurrentVersion ?? ""; if (version != cur) ProjectService.SwitchVersion(version.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? version : version + ".json"); return; }
        version = ProjectService.CurrentProject?.CurrentVersion ?? "";
    }

    static string? FindProjectDir(string name)
    {
        foreach (var dir in ProjectService.GetProjectDirectories())
        {
            if (string.Equals(SysPath.GetFileName(dir), name, StringComparison.OrdinalIgnoreCase)) return dir;
            var cfg = ReadProjectConfig(dir);
            if (cfg != null && string.Equals(cfg.Name, name, StringComparison.OrdinalIgnoreCase)) return dir;
        }
        return null;
    }

    static ProjectConfig? ReadProjectConfig(string dir)
    {
        var path = SysPath.Combine(dir, "project.json");
        if (!File.Exists(path)) return null;
        try { return JsonSerializer.Deserialize<ProjectConfig>(File.ReadAllText(path), _jsonInsensitive); }
        catch { return null; }
    }

    static void SaveVersionFileCore(string file, DataFile data)
    {
        var dir = SysPath.GetDirectoryName(file);
        if (dir != null) Directory.CreateDirectory(dir);
        File.WriteAllText(file, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
    }
}