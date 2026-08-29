using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using OCCMissionGoals.Models;
using OCCMissionGoals.Services;
using SysPath = System.IO.Path;

namespace OCCMissionGoals.Cli;

/// <summary>
/// CLI — 命令行接口。
/// 统一子命令风格：project / version / entry / tag，旧 flag 写法（-a/-l/-c/-v ...）作为别名保留。
/// 默认输出人类可读文本，加 --json 输出机器可读 JSON。
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

    // 数据文件（versions/*.json）读写选项，与 GUI 保持一致（缩进 + 属性大小写不敏感）
    private static readonly JsonSerializerOptions _jsonFile = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private static string? _projectName;
    private static bool _jsonOut;

    // ========================================================
    // 入口
    // ========================================================
    public static int Run(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; Console.InputEncoding = Encoding.UTF8; } catch { }

        if (args.Length == 0) { PrintHelp(); return 0; }

        _projectName = null;
        _jsonOut = false;

        // 1. 解析全局选项（-p/--project、--json），其余作为命令 token
        var tokens = new List<string>();
        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (a == "-p" || a == "--project")
            {
                if (++i >= args.Length) { Err("缺少 -p/--project 的值"); return 2; }
                _projectName = args[i];
            }
            else if (a.StartsWith("--project=", StringComparison.Ordinal))
                _projectName = a["--project=".Length..];
            else if (a == "--json")
                _jsonOut = true;
            else
                tokens.Add(a);
        }

        if (tokens.Count == 0) { PrintHelp(); return 0; }

        var cmdToken = tokens[0];
        var cmdArgs = tokens.Skip(1).ToList();

        if (cmdToken == "help")
        {
            if (cmdArgs.Count > 0) PrintHelpForNoun(cmdArgs[0]);
            else PrintHelp();
            return 0;
        }
        if (cmdToken is "-h" or "--help") { PrintHelp(); return 0; }

        string? scopedVersion = null;

        // 兼容旧 -v / --version 语法：
        //   -v Iterate/Delete/Archive  -> version 子命令
        //   -v <版本号>                -> version switch <版本号>（持久切换）
        //   -v <版本号> <其他命令>      -> 以 <版本号> 为作用域执行后续命令（不持久切换）
        if (cmdToken is "-v" or "--version")
        {
            if (cmdArgs.Count == 0) { Err("缺少 -v 的值"); return 2; }
            var v = cmdArgs[0];
            var kw = v.ToLowerInvariant();
            if (kw is "iterate" or "delete" or "archive")
                return DispatchVersion(new List<string> { kw }.Concat(cmdArgs.Skip(1)).ToList());
            if (cmdArgs.Count == 1)
                return DispatchVersion(new List<string> { "switch", v });
            scopedVersion = v;
            cmdToken = cmdArgs[1];
            cmdArgs = cmdArgs.Skip(2).ToList();
            if (cmdToken is "-h" or "--help" or "help") { PrintHelp(); return 0; }
        }

        // 兼容旧短标志 -> entry 子命令
        string? entrySub = cmdToken switch
        {
            "-a" or "--add"       => "add",
            "-c" or "--check"     => "show",
            "-d" or "--done"      => "done",
            "-u" or "--undone"    => "undone",
            "-D" or "--delete"    => "delete",
            "-f" or "--favorited" => "favorite",
            "-l" or "--list"      => "list",
            _ => null
        };

        try
        {
            if (entrySub != null)
                return DispatchEntry(new List<string> { entrySub }.Concat(cmdArgs).ToList(), scopedVersion);

            return cmdToken switch
            {
                "project" => DispatchProject(cmdArgs),
                "version" => DispatchVersion(cmdArgs),
                "entry"   => DispatchEntry(cmdArgs, scopedVersion),
                "tag"     => DispatchTag(cmdArgs),
                _ => UnknownCommand(cmdToken)
            };
        }
        catch (Exception ex)
        {
            Err($"错误: {ex.Message}");
            return 1;
        }
    }

    static int UnknownCommand(string cmd)
    {
        Err($"未知命令: {cmd}");
        PrintHelp();
        return 1;
    }

    // ========================================================
    // 分发
    // ========================================================
    static int DispatchProject(List<string> args)
    {
        if (args.Count == 0 || WantHelp(args)) { PrintProjectHelp(); return 0; }
        var sub = args[0].ToLowerInvariant();
        var rest = args.Skip(1).ToList();
        switch (sub)
        {
            case "list": return ProjectList();
            case "info": return ProjectInfo(rest);
            default: Err($"未知 project 子命令: {sub}"); PrintProjectHelp(); return 1;
        }
    }

    static int DispatchVersion(List<string> args)
    {
        if (args.Count == 0 || WantHelp(args)) { PrintVersionHelp(); return 0; }
        var sub = args[0].ToLowerInvariant();
        var rest = args.Skip(1).ToList();
        switch (sub)
        {
            case "list":     return VersionList();
            case "current":  return VersionCurrent();
            case "switch":   return VersionSwitch(rest);
            case "iterate":  return VersionIterate();
            case "delete":   return VersionDelete(rest);
            case "archive":  return VersionArchive(rest);
            default: Err($"未知 version 子命令: {sub}"); PrintVersionHelp(); return 1;
        }
    }

    static int DispatchEntry(List<string> args, string? scopedVersion)
    {
        if (args.Count == 0 || WantHelp(args)) { PrintEntryHelp(); return 0; }
        var sub = args[0].ToLowerInvariant();
        var rest = args.Skip(1).ToList();
        switch (sub)
        {
            case "list":     return EntryList(rest, scopedVersion);
            case "show":     return EntryShow(rest, scopedVersion);
            case "add":      return EntryAdd(rest, scopedVersion);
            case "edit":     return EntryEdit(rest, scopedVersion);
            case "done":     return EntryDone(rest, scopedVersion);
            case "undone":
            case "undo":     return EntryUndone(rest, scopedVersion);
            case "delete":
            case "rm":       return EntryDelete(rest, scopedVersion);
            case "favorite":
            case "fav":      return EntryFavorite(rest, scopedVersion);
            default: Err($"未知 entry 子命令: {sub}"); PrintEntryHelp(); return 1;
        }
    }

    static int DispatchTag(List<string> args)
    {
        if (args.Count == 0 || WantHelp(args)) { PrintTagHelp(); return 0; }
        var sub = args[0].ToLowerInvariant();
        var rest = args.Skip(1).ToList();
        switch (sub)
        {
            case "list":                 return TagList();
            case "add":
            case "new":                  return TagAdd(rest);
            case "delete":
            case "remove":
            case "rm":                   return TagDelete(rest);
            case "rename":
            case "mv":                   return TagRename(rest);
            default: Err($"未知 tag 子命令: {sub}"); PrintTagHelp(); return 1;
        }
    }

    // ========================================================
    // project
    // ========================================================
    static int ProjectList()
    {
        var dirs = ProjectService.GetProjectDirectories();
        if (_jsonOut)
        {
            OutJson(dirs.Select(dir =>
            {
                var cfg = ReadProjectConfig(dir);
                return new
                {
                    name = cfg?.Name ?? SysPath.GetFileName(dir),
                    description = cfg?.Description ?? "",
                    currentVersion = cfg?.CurrentVersion ?? "",
                    path = dir
                };
            }));
        }
        else
        {
            if (dirs.Count == 0) { Console.WriteLine("（无项目）"); return 0; }
            foreach (var dir in dirs)
            {
                var cfg = ReadProjectConfig(dir);
                var name = cfg?.Name ?? SysPath.GetFileName(dir);
                Console.WriteLine($"- {name}  (v{cfg?.CurrentVersion})  [{SysPath.GetFileName(dir)}]");
            }
        }
        return 0;
    }

    static int ProjectInfo(List<string> args)
    {
        var s = ParseArgs(args);
        var name = s.Positionals.FirstOrDefault();
        if (!EnsureProject(name)) return 1;

        var dir = ProjectService.CurrentProjectDir!;
        var cfg = ProjectService.CurrentProject!;
        var files = ProjectService.GetVersionFiles(dir);

        if (_jsonOut)
        {
            OutJson(new
            {
                name = cfg.Name,
                description = cfg.Description,
                currentVersion = cfg.CurrentVersion,
                projectNumber = cfg.ProjectNumber,
                nextEntryId = cfg.NextEntryId,
                createdAt = cfg.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                path = dir,
                versions = files.Select(StripJson),
                tags = cfg.TypeOptions
            });
        }
        else
        {
            Console.WriteLine($"项目: {cfg.Name}");
            Console.WriteLine($"描述: {cfg.Description}");
            Console.WriteLine($"当前版本: {cfg.CurrentVersion}");
            Console.WriteLine($"项目编号: {cfg.ProjectNumber:D3}   下一个条目号: {cfg.NextEntryId}");
            Console.WriteLine($"创建时间: {cfg.CreatedAt:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"路径: {dir}");
            Console.WriteLine($"版本: {(files.Count > 0 ? string.Join(", ", files.Select(StripJson)) : "（无）")}");
            Console.WriteLine($"标签: {(cfg.TypeOptions.Count > 0 ? string.Join(", ", cfg.TypeOptions) : "（无）")}");
        }
        return 0;
    }

    // ========================================================
    // version
    // ========================================================
    static int VersionList()
    {
        if (!EnsureProject()) return 1;
        var dir = ProjectService.CurrentProjectDir!;
        var files = ProjectService.GetVersionFiles(dir);
        var cur = ProjectService.CurrentProject?.CurrentVersion ?? "";
        if (_jsonOut)
            OutJson(files.Select(f => { var v = StripJson(f); return new { version = v, current = v == cur }; }));
        else
        {
            if (files.Count == 0) { Console.WriteLine("（无版本）"); return 0; }
            foreach (var f in files)
            {
                var v = StripJson(f);
                Console.WriteLine($"{(v == cur ? "* " : "  ")}{v}");
            }
        }
        return 0;
    }

    static int VersionCurrent()
    {
        if (!EnsureProject()) return 1;
        var cur = ProjectService.CurrentProject?.CurrentVersion ?? "";
        Emit(new { version = cur }, cur);
        return 0;
    }

    static int VersionSwitch(List<string> args)
    {
        if (!EnsureProject()) return 1;
        var s = ParseArgs(args);
        var ver = s.Positionals.FirstOrDefault();
        if (ver == null) { Err("用法: version switch <版本号>"); return 2; }
        var clean = NormalizeVersion(ver);
        var file = SysPath.Combine(ProjectService.GetVersionsDir(ProjectService.CurrentProjectDir!), clean + ".json");
        if (!File.Exists(file)) { Err($"版本文件不存在: {clean}.json"); return 1; }
        ProjectService.SwitchVersion(clean + ".json");
        Emit(new { ok = true, version = clean }, $"已切换到版本 {clean}");
        return 0;
    }

    static int VersionIterate()
    {
        if (!EnsureProject()) return 1;
        var cur = ProjectService.CurrentProject?.CurrentVersion ?? "0.1.0-alpha.0";
        var dashIdx = cur.LastIndexOf('-');
        string newVer;
        if (dashIdx >= 0 && int.TryParse(cur[(dashIdx + 1)..], out int n))
            newVer = cur[..(dashIdx + 1)] + (n + 1);
        else
            newVer = cur + "-1";
        ProjectService.UpdateVersion(newVer);
        Emit(new { ok = true, previous = cur, current = newVer }, $"版本迭代：{cur} → {newVer}");
        return 0;
    }

    static int VersionDelete(List<string> args)
    {
        if (!EnsureProject()) return 1;
        var s = ParseArgs(args);
        var ver = s.Positionals.FirstOrDefault();
        if (ver == null) { Err("用法: version delete <版本号>"); return 2; }
        var clean = NormalizeVersion(ver);
        var file = SysPath.Combine(ProjectService.GetVersionsDir(ProjectService.CurrentProjectDir!), clean + ".json");
        if (!File.Exists(file)) { Err($"版本文件不存在: {clean}.json"); return 1; }
        if (clean == (ProjectService.CurrentProject?.CurrentVersion ?? "")) { Err("不能删除当前版本。"); return 1; }
        ProjectService.DeleteVersion(clean + ".json");
        Emit(new { ok = true, version = clean, deleted = true }, $"已删除版本 {clean}");
        return 0;
    }

    static int VersionArchive(List<string> args)
    {
        if (!EnsureProject()) return 1;
        var s = ParseArgs(args);
        var ver = s.Positionals.FirstOrDefault();
        if (ver == null) { Err("用法: version archive <版本号>"); return 2; }
        var clean = NormalizeVersion(ver);
        var versionsDir = ProjectService.GetVersionsDir(ProjectService.CurrentProjectDir!);
        var src = SysPath.Combine(versionsDir, clean + ".json");
        if (!File.Exists(src)) { Err($"版本文件不存在: {clean}.json"); return 1; }
        if (clean == (ProjectService.CurrentProject?.CurrentVersion ?? "")) { Err("不能归档当前版本。"); return 1; }

        try
        {
            var data = JsonSerializer.Deserialize<DataFile>(File.ReadAllText(src), _jsonInsensitive);
            if (data == null || (data.Unfinished.Count == 0 && data.Finished.Count == 0))
            { Err($"版本 {clean} 中无条目，无法归档。"); return 1; }
            if (data.Unfinished.Count > 0)
            { Err($"版本 {clean} 中仍有 {data.Unfinished.Count} 条未完成条目，无法归档。"); return 1; }
        }
        catch (Exception ex) { Err($"读取版本文件失败: {ex.Message}"); return 1; }

        var archiveDir = SysPath.Combine(versionsDir, "archive");
        Directory.CreateDirectory(archiveDir);
        var dest = SysPath.Combine(archiveDir, clean + ".json");
        if (File.Exists(dest)) { Err($"归档目录中已存在 {clean}.json"); return 1; }
        File.Move(src, dest);
        Emit(new { ok = true, version = clean, archived = true }, $"已归档版本 {clean} → versions/archive/");
        return 0;
    }

    // ========================================================
    // entry
    // ========================================================
    static int EntryList(List<string> args, string? scopedVersion)
    {
        if (!EnsureProject()) return 1;
        var s = ParseArgs(args);
        var type = NormalizeScope(s.Val("--type"));
        var search = s.Val("--search") ?? "";
        var tag = s.Val("--tag") ?? "";
        var favOnly = s.Has("--favorite") || s.Has("--fav");
        var all = s.Has("--all");
        var ver = s.Val("--version") ?? scopedVersion;

        var items = all ? ReadAllVersionsList() : ReadSingleVersionList(ver);

        if (type == "u") items = items.Where(x => x.status == "unfinished").ToList();
        else if (type == "f") items = items.Where(x => x.status == "finished").ToList();
        if (favOnly) items = items.Where(x => x.e.IsFavorited).ToList();
        if (!string.IsNullOrWhiteSpace(tag))
            items = items.Where(x => x.e.Type.Any(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase))).ToList();
        if (!string.IsNullOrWhiteSpace(search))
        {
            items = items.Where(x =>
                x.e.Title.Contains(search, StringComparison.OrdinalIgnoreCase)
                || x.e.Brief.Contains(search, StringComparison.OrdinalIgnoreCase)
                || x.e.Detail.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (_jsonOut)
            OutJson(items.Select((x, i) => { var d = EntryToDict(x.e, i + 1); d["status"] = x.status; return d; }));
        else
            PrintEntryTable(items);
        return 0;
    }

    static int EntryShow(List<string> args, string? scopedVersion)
    {
        if (!EnsureProject()) return 1;
        var s = ParseArgs(args);
        var scope = NormalizeScope(s.Val("--type"));
        var refStr = s.Positionals.FirstOrDefault();
        if (refStr == null) { Err("用法: entry show <编号|索引|标题> [--type u|f]"); return 2; }
        var ver = s.Val("--version") ?? scopedVersion;

        var found = FindEntry(refStr, ver, scope);
        if (found == null) return 1;
        var (e, _, f, list) = found.Value;

        if (_jsonOut)
        {
            var d = EntryToDict(e, 0);
            d["status"] = list == "f" ? "finished" : "unfinished";
            d["versionFile"] = SysPath.GetFileNameWithoutExtension(f);
            OutJson(d);
        }
        else
            PrintEntryDetail(e, list, SysPath.GetFileNameWithoutExtension(f));
        return 0;
    }

    static int EntryAdd(List<string> args, string? scopedVersion)
    {
        if (!EnsureProject()) return 1;
        var s = ParseArgs(args);

        var version = NormalizeVersion(s.Val("--version") ?? scopedVersion ?? ProjectService.CurrentProject?.CurrentVersion ?? "");
        var file = ResolveVersionFileForWrite(version);
        if (file == null) return 1;

        GoalEntry entry;
        if (s.Positionals.Count > 0)
        {
            // 旧 JSON 形式：-a {Title="...", Severity="Fatal", ...}
            var raw = string.Join(" ", s.Positionals);
            var json = ParseCustomDataFormat(raw);
            try { entry = JsonSerializer.Deserialize<GoalEntry>(json, _jsonInsensitive) ?? new GoalEntry(); }
            catch (Exception ex) { Err($"解析失败: {ex.Message}"); return 1; }
            if (string.IsNullOrWhiteSpace(entry.Title)) { Err("缺少 Title"); return 1; }
            entry.CompletedAt = default;
        }
        else
        {
            var title = s.Val("--title");
            if (string.IsNullOrWhiteSpace(title)) { Err("缺少 --title（或使用 -a {Title=\"...\"} 形式）"); return 2; }
            var sevStr = s.Val("--severity") ?? "";
            var sev = GoalSeverity.General;
            if (!string.IsNullOrWhiteSpace(sevStr) && !Enum.TryParse<GoalSeverity>(sevStr, true, out sev))
            { Err($"无效的严重程度: {sevStr}（可选 Fatal/Severe/General/Patch/Update）"); return 2; }
            var brief = s.Val("--brief") ?? "";
            var detail = s.Val("--detail") ?? "";
            var dlStr = s.Val("--deadline") ?? "";
            var deadline = DateTime.Today.AddDays(7);
            if (!string.IsNullOrWhiteSpace(dlStr) && !DateTime.TryParse(dlStr, out deadline))
            { Err($"无效的截止日期: {dlStr}"); return 2; }
            var typeStr = s.Val("--type") ?? s.Val("--tag") ?? "";
            var tags = typeStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            var fav = s.Has("--favorite") || s.Has("--fav");
            entry = new GoalEntry
            {
                Title = title,
                Severity = sev,
                Brief = brief,
                Detail = detail,
                Deadline = deadline,
                CompletedAt = default,
                Type = tags,
                IsFavorited = fav
            };
        }

        entry.Version = version;
        AddEntryToFile(file, entry);
        Emit(new { ok = true, id = entry.Id, title = entry.Title, version = entry.Version },
             $"已添加 #{entry.Id}「{entry.Title}」");
        return 0;
    }

    static int EntryEdit(List<string> args, string? scopedVersion)
    {
        if (!EnsureProject()) return 1;
        var s = ParseArgs(args);
        var refStr = s.Positionals.FirstOrDefault();
        if (refStr == null) { Err("用法: entry edit <编号|索引|标题> [--title ...] [--severity ...] [--brief ...] [--detail ...] [--deadline ...] [--type ...] [--favorite|--unfavorite]"); return 2; }
        var ver = s.Val("--version") ?? scopedVersion;

        using (FileLock.Acquire())
        {
            var found = FindEntry(refStr, ver, "a");
            if (found == null) return 1;
            var (e, d, f, _) = found.Value;

            var title = s.Val("--title");
            if (!string.IsNullOrWhiteSpace(title)) e.Title = title;
            var sevStr = s.Val("--severity");
            if (!string.IsNullOrWhiteSpace(sevStr))
            {
                if (!Enum.TryParse<GoalSeverity>(sevStr, true, out var sev)) { Err($"无效的严重程度: {sevStr}"); return 2; }
                e.Severity = sev;
            }
            var brief = s.Val("--brief"); if (brief != null) e.Brief = brief;
            var detail = s.Val("--detail"); if (detail != null) e.Detail = detail;
            var dlStr = s.Val("--deadline");
            if (!string.IsNullOrWhiteSpace(dlStr))
            {
                if (!DateTime.TryParse(dlStr, out var dl)) { Err($"无效的截止日期: {dlStr}"); return 2; }
                e.Deadline = dl;
            }
            var typeStr = s.Val("--type");
            if (typeStr != null) e.Type = typeStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            if (s.Has("--favorite") || s.Has("--fav")) e.IsFavorited = true;
            if (s.Has("--unfavorite") || s.Has("--unfav")) e.IsFavorited = false;

            WriteDataFile(f, d);
            Emit(new { ok = true, id = e.Id, title = e.Title }, $"已更新 #{e.Id}「{e.Title}」");
            return 0;
        }
    }

    static int EntryDone(List<string> args, string? scopedVersion)
    {
        if (!EnsureProject()) return 1;
        var s = ParseArgs(args);
        var refStr = s.Positionals.FirstOrDefault();
        if (refStr == null) { Err("用法: entry done <编号|索引|标题>"); return 2; }
        var ver = s.Val("--version") ?? scopedVersion;

        using (FileLock.Acquire())
        {
            var found = FindEntry(refStr, ver, "u");
            if (found == null) return 1;
            var (e, d, f, list) = found.Value;
            if (list == "f") { Err("条目已完成。"); return 1; }
            d.Unfinished.Remove(e);
            e.CompletedAt = DateTime.Now;
            d.Finished.Add(e);
            WriteDataFile(f, d);
            Emit(new { ok = true, id = e.Id, title = e.Title, status = "finished" }, $"已完成 #{e.Id}「{e.Title}」");
            return 0;
        }
    }

    static int EntryUndone(List<string> args, string? scopedVersion)
    {
        if (!EnsureProject()) return 1;
        var s = ParseArgs(args);
        var refStr = s.Positionals.FirstOrDefault();
        if (refStr == null) { Err("用法: entry undone <编号|索引|标题>"); return 2; }
        var ver = s.Val("--version") ?? scopedVersion;

        using (FileLock.Acquire())
        {
            var found = FindEntry(refStr, ver, "f");
            if (found == null) return 1;
            var (e, d, f, list) = found.Value;
            if (list == "u") { Err("条目未完成。"); return 1; }
            d.Finished.Remove(e);
            e.CompletedAt = default;
            d.Unfinished.Insert(0, e);
            WriteDataFile(f, d);
            Emit(new { ok = true, id = e.Id, title = e.Title, status = "unfinished" }, $"已取消完成 #{e.Id}「{e.Title}」");
            return 0;
        }
    }

    static int EntryDelete(List<string> args, string? scopedVersion)
    {
        if (!EnsureProject()) return 1;
        var s = ParseArgs(args);
        var refStr = s.Positionals.FirstOrDefault();
        if (refStr == null) { Err("用法: entry delete <编号|索引|标题>"); return 2; }
        var ver = s.Val("--version") ?? scopedVersion;

        using (FileLock.Acquire())
        {
            var found = FindEntry(refStr, ver, "a");
            if (found == null) return 1;
            var (e, d, f, list) = found.Value;
            if (list == "f") d.Finished.Remove(e);
            else d.Unfinished.Remove(e);
            WriteDataFile(f, d);
            Emit(new { ok = true, id = e.Id, title = e.Title, deleted = true }, $"已删除 #{e.Id}「{e.Title}」");
            return 0;
        }
    }

    static int EntryFavorite(List<string> args, string? scopedVersion)
    {
        if (!EnsureProject()) return 1;
        var s = ParseArgs(args);
        if (s.Positionals.Count < 2) { Err("用法: entry favorite <编号|索引|标题> <true|false>"); return 2; }
        var refStr = s.Positionals[0];
        if (!bool.TryParse(s.Positionals[1], out var fav)) { Err("第二个参数必须是 true 或 false"); return 2; }
        var ver = s.Val("--version") ?? scopedVersion;

        using (FileLock.Acquire())
        {
            var found = FindEntry(refStr, ver, "a");
            if (found == null) return 1;
            var (e, d, f, _) = found.Value;
            e.IsFavorited = fav;
            WriteDataFile(f, d);
            Emit(new { ok = true, id = e.Id, title = e.Title, isFavorited = e.IsFavorited },
                 $"{(fav ? "★ 已收藏" : "☆ 已取消收藏")} #{e.Id}「{e.Title}」");
            return 0;
        }
    }

    // ========================================================
    // tag
    // ========================================================
    static int TagList()
    {
        if (!EnsureProject()) return 1;
        var cfg = ProjectService.CurrentProject!;
        ProjectService.EnsureTypeColorsAligned();
        if (_jsonOut)
            OutJson(cfg.TypeOptions.Select((t, i) => new { name = t, color = i < cfg.TypeColors.Count ? cfg.TypeColors[i] : "" }));
        else
        {
            if (cfg.TypeOptions.Count == 0) { Console.WriteLine("（无标签）"); return 0; }
            for (int i = 0; i < cfg.TypeOptions.Count; i++)
            {
                var color = i < cfg.TypeColors.Count ? cfg.TypeColors[i] : "";
                var colorStr = string.IsNullOrWhiteSpace(color) ? ""
                    : (color.StartsWith("#", StringComparison.Ordinal) ? color : "#" + color);
                Console.WriteLine($"- {cfg.TypeOptions[i]}{(colorStr == "" ? "" : "  " + colorStr)}");
            }
        }
        return 0;
    }

    static int TagAdd(List<string> args)
    {
        if (!EnsureProject()) return 1;
        var s = ParseArgs(args);
        var name = s.Positionals.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(name)) { Err("用法: tag add <名称> [--color <hex>]"); return 2; }
        var color = s.Val("--color") ?? "";

        var cfg = ProjectService.CurrentProject!;
        if (cfg.TypeOptions.Any(t => string.Equals(t, name, StringComparison.OrdinalIgnoreCase))) { Err($"标签已存在: {name}"); return 1; }
        cfg.TypeOptions.Add(name);
        ProjectService.EnsureTypeColorsAligned();
        cfg.TypeColors[cfg.TypeOptions.Count - 1] = color;
        ProjectService.UpdateProjectConfig(cfg);

        Emit(new { ok = true, name = name, color = color }, $"已添加标签「{name}」");
        return 0;
    }

    static int TagDelete(List<string> args)
    {
        if (!EnsureProject()) return 1;
        var s = ParseArgs(args);
        var name = s.Positionals.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(name)) { Err("用法: tag delete <名称>"); return 2; }

        var cfg = ProjectService.CurrentProject!;
        var idx = cfg.TypeOptions.FindIndex(t => string.Equals(t, name, StringComparison.OrdinalIgnoreCase));
        if (idx < 0) { Err($"标签不存在: {name}"); return 1; }
        cfg.TypeOptions.RemoveAt(idx);
        if (idx < cfg.TypeColors.Count) cfg.TypeColors.RemoveAt(idx);
        ProjectService.UpdateProjectConfig(cfg);

        DataService.UpdateTypeTagAcrossVersions(ProjectService.CurrentProjectDir!, name, null);
        Emit(new { ok = true, name = name, deleted = true }, $"已删除标签「{name}」（已从所有版本条目中移除）");
        return 0;
    }

    static int TagRename(List<string> args)
    {
        if (!EnsureProject()) return 1;
        var s = ParseArgs(args);
        if (s.Positionals.Count < 2) { Err("用法: tag rename <旧名称> <新名称>"); return 2; }
        var oldName = s.Positionals[0];
        var newName = s.Positionals[1];

        var cfg = ProjectService.CurrentProject!;
        var idx = cfg.TypeOptions.FindIndex(t => string.Equals(t, oldName, StringComparison.OrdinalIgnoreCase));
        if (idx < 0) { Err($"标签不存在: {oldName}"); return 1; }
        if (cfg.TypeOptions.Any(t => string.Equals(t, newName, StringComparison.OrdinalIgnoreCase) && !string.Equals(t, oldName, StringComparison.OrdinalIgnoreCase)))
        { Err($"标签已存在: {newName}"); return 1; }

        cfg.TypeOptions[idx] = newName;
        ProjectService.UpdateProjectConfig(cfg);

        DataService.UpdateTypeTagAcrossVersions(ProjectService.CurrentProjectDir!, oldName, newName);
        Emit(new { ok = true, old = oldName, name = newName }, $"已将标签「{oldName}」重命名为「{newName}」");
        return 0;
    }

    // ========================================================
    // 帮助
    // ========================================================
    static void PrintHelp()
    {
        Console.WriteLine(
@"OCC Mission & Goals — CLI

用法:
  OCCMissionGoals.exe [全局选项] <命令> [子命令] [参数]

全局选项:
  -p, --project <名称>    选择项目（文件夹名或项目名）
      --json              输出 JSON（默认输出人类可读文本）
  -h, --help              帮助

命令:
  project   管理项目   (project list | project info [名称])
  version   管理版本   (version list | current | switch | iterate | delete | archive)
  entry     管理条目   (entry list | show | add | edit | done | undone | delete | favorite)
  tag       管理标签   (tag list | add | delete | rename)

兼容旧写法:
  -a/--add  -c/--check  -d/--done  -u/--undone  -D/--delete
  -f/--favorited  -l/--list  -v <版本号|Iterate|Delete|Archive>

示例:
  OCCMissionGoals.exe -p 我的项目 entry list
  OCCMissionGoals.exe -p 我的项目 entry add --title ""修复登录"" --severity Severe --type Bug
  OCCMissionGoals.exe -p 我的项目 entry done 001000001
  OCCMissionGoals.exe -p 我的项目 tag add 前端 --color ""#3D9DE8""
  OCCMissionGoals.exe -p 我的项目 version iterate

  更多帮助: <命令> --help   (如 entry --help)");
    }

    static void PrintProjectHelp()
    {
        Console.WriteLine(
@"project — 项目管理

用法:
  project list              列出所有项目
  project info [名称]       查看项目信息（默认 -p 指定的项目）");
    }

    static void PrintVersionHelp()
    {
        Console.WriteLine(
@"version — 版本管理

用法:
  version list               列出所有版本（* 表示当前版本）
  version current            显示当前版本号
  version switch <版本号>    切换到指定版本（持久保存）
  version iterate            迭代当前版本（预发布号 +1，如 0.1.0-alpha.0 -> -alpha.1）
  version delete <版本号>    删除版本（不能删除当前版本）
  version archive <版本号>   归档版本到 versions/archive/（须全部条目已完成）

提示:
  只想读取某个版本而不切换，用 --version <版本号>，例如:
    entry list --version 0.1.0");
    }

    static void PrintEntryHelp()
    {
        Console.WriteLine(
@"entry — 条目管理

用法:
  entry list     [--type u|f|a] [--search <关键词>] [--tag <标签>] [--favorite] [--all] [--version <版本号>]
  entry show     <编号|索引|标题> [--type u|f] [--version <版本号>]
  entry add      --title <标题> [--severity <等级>] [--brief <简介>] [--detail <详情>]
                 [--deadline <日期>] [--type <标签1,标签2>] [--favorite] [--version <版本号>]
  entry edit     <编号|索引|标题> [--title ...] [--severity ...] [--brief ...] [--detail ...]
                 [--deadline ...] [--type ...] [--favorite|--unfavorite] [--version <版本号>]
  entry done     <编号|索引|标题>
  entry undone   <编号|索引|标题>
  entry delete   <编号|索引|标题>
  entry favorite <编号|索引|标题> <true|false>

说明:
  <编号|索引|标题> 可用隐藏编号(如 001000001)、列表序号、或标题精确匹配。
  --type 在 list/show 中为 u(未完成)/f(已完成)/a(全部)；在 add/edit 中为标签(逗号分隔)。
  严重程度: Fatal/Severe/General/Patch/Update（对应 致命/严重/一般/补丁/更新）。
  添加也支持旧 JSON 形式: -a {Title=""...""}");
    }

    static void PrintTagHelp()
    {
        Console.WriteLine(
@"tag — 标签管理

用法:
  tag list                    列出所有标签
  tag add <名称> [--color <hex>]        新建标签
  tag delete <名称>           删除标签（并移除所有版本条目中的该标签）
  tag rename <旧名称> <新名称> 重命名标签（同步更新所有版本条目）");
    }

    static void PrintHelpForNoun(string noun)
    {
        switch (noun)
        {
            case "project": PrintProjectHelp(); break;
            case "version": PrintVersionHelp(); break;
            case "entry": PrintEntryHelp(); break;
            case "tag": PrintTagHelp(); break;
            default: PrintHelp(); break;
        }
    }

    // ========================================================
    // 工具
    // ========================================================
    static bool WantHelp(List<string> args) => args.Any(a => a is "-h" or "--help" or "help");

    static void Emit(object json, string human)
    {
        if (_jsonOut) OutJson(json);
        else Console.WriteLine(human);
    }

    static void OutJson(object data) => Console.WriteLine(JsonSerializer.Serialize(data, _json));
    static void Err(string msg) => Console.Error.WriteLine(msg);

    static string NormalizeVersion(string v) =>
        v.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? v[..^5] : v;

    static string StripJson(string f) =>
        f.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? f[..^5] : f;

    static string NormalizeScope(string? s) => (s ?? "a").ToLowerInvariant() switch
    {
        "u" or "unfinished" or "todo" => "u",
        "f" or "finished" or "done" => "f",
        _ => "a"
    };

    static string Sev(GoalSeverity s) => s switch
    {
        GoalSeverity.Fatal => "致命", GoalSeverity.Severe => "严重",
        GoalSeverity.General => "一般", GoalSeverity.Patch => "补丁",
        GoalSeverity.Update => "更新", _ => "未知"
    };

    static string SevMark(GoalSeverity s) => s switch
    {
        GoalSeverity.Fatal => "🔴", GoalSeverity.Severe => "🟠",
        GoalSeverity.General => "🟡", GoalSeverity.Patch => "🔵",
        GoalSeverity.Update => "🟢", _ => "⚪"
    };

    static bool EnsureProject(string? name = null)
    {
        var n = name ?? _projectName;
        if (!string.IsNullOrEmpty(n))
        {
            var dir = FindProjectDir(n);
            if (dir == null) { Err($"未找到项目: {n}"); return false; }
            if (ProjectService.OpenProject(dir) == null) { Err($"打开项目失败: {n}"); return false; }
            return true;
        }
        if (ProjectService.TryRestoreLastProject()) return true;
        var dirs = ProjectService.GetProjectDirectories();
        if (dirs.Count == 0) { Err("没有可用项目。请先在 GUI 中新建项目，或用 -p <项目> 指定。"); return false; }
        Err("未指定项目（-p），且没有记录上次打开的项目。可用项目：");
        foreach (var d in dirs)
        {
            var cfg = ReadProjectConfig(d);
            Err($"  - {cfg?.Name ?? SysPath.GetFileName(d)}");
        }
        return false;
    }

    static string? FindProjectDir(string name)
    {
        foreach (var dir in ProjectService.GetProjectDirectories())
        {
            if (string.Equals(SysPath.GetFileName(dir), name, StringComparison.OrdinalIgnoreCase)) return dir;
            var cfg = ReadProjectConfig(dir);
            if (cfg != null && string.Equals(cfg.Name, name, StringComparison.OrdinalIgnoreCase)) return dir;
        }
        if (Directory.Exists(name) && File.Exists(SysPath.Combine(name, "project.json"))) return name;
        return null;
    }

    static ProjectConfig? ReadProjectConfig(string dir)
    {
        var path = SysPath.Combine(dir, "project.json");
        if (!File.Exists(path)) return null;
        try { return JsonSerializer.Deserialize<ProjectConfig>(File.ReadAllText(path), _jsonInsensitive); }
        catch { return null; }
    }

    static DataFile ReadDataFile(string file)
    {
        if (!File.Exists(file)) return new DataFile();
        try { return JsonSerializer.Deserialize<DataFile>(File.ReadAllText(file), _jsonInsensitive) ?? new DataFile(); }
        catch { return new DataFile(); }
    }

    static void WriteDataFile(string file, DataFile data)
    {
        var dir = SysPath.GetDirectoryName(file);
        if (dir != null) Directory.CreateDirectory(dir);
        File.WriteAllText(file, JsonSerializer.Serialize(data, _jsonFile));
    }

    static void AddEntryToFile(string file, GoalEntry entry)
    {
        using (FileLock.Acquire())
        {
            ProjectService.AssignEntryIdCore(entry);
            var data = ReadDataFile(file);
            data.Unfinished.Add(entry);
            WriteDataFile(file, data);
        }
    }

    static string? ResolveVersionFile(string? version)
    {
        var dir = ProjectService.GetVersionsDir(ProjectService.CurrentProjectDir!);
        if (version != null)
        {
            var clean = NormalizeVersion(version);
            var f = SysPath.Combine(dir, clean + ".json");
            if (!File.Exists(f)) { Err($"版本文件不存在: {clean}.json"); return null; }
            return f;
        }
        var cur = ProjectService.CurrentProject?.CurrentVersion ?? "";
        var cf = SysPath.Combine(dir, cur + ".json");
        if (File.Exists(cf)) return cf;
        var any = Directory.GetFiles(dir, "*.json").OrderBy(x => x, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
        if (any == null) { Err("当前项目没有版本文件。"); return null; }
        return any;
    }

    static string? ResolveVersionFileForWrite(string version)
    {
        var clean = NormalizeVersion(version);
        var file = SysPath.Combine(ProjectService.GetVersionsDir(ProjectService.CurrentProjectDir!), clean + ".json");
        if (!File.Exists(file)) { Err($"版本文件不存在: {clean}.json（可先 version switch <版本号>）"); return null; }
        return file;
    }

    static List<string> GetCandidateVersionFiles(string? versionSwitch)
    {
        var dir = ProjectService.GetVersionsDir(ProjectService.CurrentProjectDir!);
        if (!Directory.Exists(dir)) return new List<string>();
        if (versionSwitch != null)
        {
            var f = SysPath.Combine(dir, NormalizeVersion(versionSwitch) + ".json");
            return File.Exists(f) ? new List<string> { f } : new List<string>();
        }
        return Directory.GetFiles(dir, "*.json").OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
    }

    static List<(GoalEntry e, string status, string version)> ReadSingleVersionList(string? version)
    {
        var list = new List<(GoalEntry e, string status, string version)>();
        var file = ResolveVersionFile(version);
        if (file == null) return list;
        var data = ReadDataFile(file);
        var ver = SysPath.GetFileNameWithoutExtension(file);
        list.AddRange(data.Unfinished.Select(e => (e, "unfinished", ver)));
        list.AddRange(data.Finished.Select(e => (e, "finished", ver)));
        return list;
    }

    static List<(GoalEntry e, string status, string version)> ReadAllVersionsList()
    {
        var list = new List<(GoalEntry e, string status, string version)>();
        var dir = ProjectService.GetVersionsDir(ProjectService.CurrentProjectDir!);
        if (!Directory.Exists(dir)) return list;
        foreach (var f in Directory.GetFiles(dir, "*.json").OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            var d = ReadDataFile(f);
            var ver = SysPath.GetFileNameWithoutExtension(f);
            list.AddRange(d.Unfinished.Select(e => (e, "unfinished", ver)));
            list.AddRange(d.Finished.Select(e => (e, "finished", ver)));
        }
        return list;
    }

    /// <summary>
    /// 查找条目：先按编号，再按 1-based 索引（scope: u=未完成/f=已完成/a=未完成+已完成），最后按标题精确匹配。
    /// 返回条目及其所在文件、所在列表。
    /// </summary>
    static (GoalEntry e, DataFile d, string f, string list)? FindEntry(string refStr, string? versionSwitch, string scope)
    {
        var files = GetCandidateVersionFiles(versionSwitch);
        if (files.Count == 0) { Err("当前项目没有可用的版本文件。"); return null; }

        // 1) 按编号
        foreach (var f in files)
        {
            var d = ReadDataFile(f);
            var e = d.Unfinished.FirstOrDefault(x => x.Id == refStr)
                 ?? d.Finished.FirstOrDefault(x => x.Id == refStr);
            if (e != null) return (e, d, f, d.Finished.Contains(e) ? "f" : "u");
        }

        // 2) 按 1-based 索引
        if (int.TryParse(refStr, out var idx) && idx > 0)
        {
            int seen = 0;
            foreach (var f in files)
            {
                var d = ReadDataFile(f);
                if (scope is "u" or "a")
                {
                    if (idx - seen <= d.Unfinished.Count)
                        return (d.Unfinished[idx - seen - 1], d, f, "u");
                    seen += d.Unfinished.Count;
                }
                if (scope is "f" or "a")
                {
                    if (idx - seen <= d.Finished.Count)
                        return (d.Finished[idx - seen - 1], d, f, "f");
                    seen += d.Finished.Count;
                }
            }
            Err($"索引 {idx} 超出范围（共 {seen} 条）。");
            return null;
        }

        // 3) 按标题精确匹配（忽略大小写）
        foreach (var f in files)
        {
            var d = ReadDataFile(f);
            var e = d.Unfinished.FirstOrDefault(x => string.Equals(x.Title, refStr, StringComparison.OrdinalIgnoreCase))
                 ?? d.Finished.FirstOrDefault(x => string.Equals(x.Title, refStr, StringComparison.OrdinalIgnoreCase));
            if (e != null) return (e, d, f, d.Finished.Contains(e) ? "f" : "u");
        }

        Err($"未找到条目「{refStr}」（可用编号 / 索引 / 标题查找）。");
        return null;
    }

    static void PrintEntryTable(List<(GoalEntry e, string status, string version)> items)
    {
        if (items.Count == 0) { Console.WriteLine("（无条目）"); return; }
        for (int i = 0; i < items.Count; i++)
        {
            var (e, status, ver) = items[i];
            var st = status == "finished" ? "已完成" : "未完成";
            var fav = e.IsFavorited ? " ♥" : "";
            var tags = e.Type.Count > 0 ? "  [" + string.Join(",", e.Type) + "]" : "";
            var dl = e.Deadline.Year < 2 ? "" : "  截止:" + e.Deadline.ToString("yyyy-MM-dd");
            Console.WriteLine($"{i + 1,3}) [{e.Id}] {SevMark(e.Severity)}{Sev(e.Severity)}  {st}  {e.Title}{fav}{tags}  (v{ver}{dl})");
        }
    }

    static void PrintEntryDetail(GoalEntry e, string list, string versionFile)
    {
        Console.WriteLine($"编号: {e.Id}");
        Console.WriteLine($"标题: {e.Title}");
        Console.WriteLine($"严重程度: {Sev(e.Severity)}");
        Console.WriteLine($"状态: {(list == "f" ? "已完成" : "未完成")}");
        Console.WriteLine($"版本: {e.Version}（文件 {versionFile}.json）");
        Console.WriteLine($"收藏: {(e.IsFavorited ? "是" : "否")}");
        Console.WriteLine($"截止: {(e.Deadline.Year < 2 ? "—" : e.Deadline.ToString("yyyy-MM-dd"))}");
        Console.WriteLine($"完成时间: {(e.CompletedAt.Year < 2 ? "—" : e.CompletedAt.ToString("yyyy-MM-dd"))}");
        Console.WriteLine($"需求变更: {e.ChangeDemand}");
        if (e.Type.Count > 0) Console.WriteLine($"标签: {string.Join(", ", e.Type)}");
        if (!string.IsNullOrWhiteSpace(e.Brief)) Console.WriteLine($"简介: {e.Brief}");
        if (!string.IsNullOrWhiteSpace(e.Detail)) Console.WriteLine($"详情: {e.Detail}");
        if (e.RelatedFiles.Count > 0)
        {
            Console.WriteLine("关联文件:");
            foreach (var rf in e.RelatedFiles)
                Console.WriteLine($"  - {rf.Path}:{rf.Line}:{rf.Column}  {rf.Function}");
        }
    }

    static Dictionary<string, object> EntryToDict(GoalEntry e, int index) => new()
    {
        ["index"] = index,
        ["id"] = e.Id,
        ["title"] = e.Title,
        ["severity"] = e.Severity.ToString(),
        ["severityLabel"] = Sev(e.Severity),
        ["brief"] = e.Brief,
        ["detail"] = e.Detail.Length > 200 ? e.Detail[..200] + "..." : e.Detail,
        ["deadline"] = e.Deadline.Year < 2 ? "" : e.Deadline.ToString("yyyy-MM-dd"),
        ["completedAt"] = e.CompletedAt.Year < 2 ? "" : e.CompletedAt.ToString("yyyy-MM-dd"),
        ["changeDemand"] = e.ChangeDemand,
        ["isFavorited"] = e.IsFavorited,
        ["version"] = e.Version,
        ["type"] = e.Type,
        ["relatedFiles"] = e.RelatedFiles.Select(f => new { path = f.Path, line = f.Line, column = f.Column, function = f.Function }).ToList()
    };

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

    // ========================================================
    // 参数解析
    // ========================================================
    sealed class ArgSet
    {
        public readonly List<string> Positionals = new();
        public readonly Dictionary<string, string?> Options = new(StringComparer.OrdinalIgnoreCase);
        public bool Has(string name) => Options.ContainsKey(name);
        public string? Val(string name) => Options.TryGetValue(name, out var v) ? v : null;
    }

    static ArgSet ParseArgs(List<string> args)
    {
        var s = new ArgSet();
        for (int i = 0; i < args.Count; i++)
        {
            var a = args[i];
            if (a.StartsWith("--", StringComparison.Ordinal))
            {
                string name;
                string? val = null;
                var eq = a.IndexOf('=');
                if (eq >= 0) { name = a[..eq]; val = a[(eq + 1)..]; }
                else
                {
                    name = a;
                    if (i + 1 < args.Count && !args[i + 1].StartsWith("-", StringComparison.Ordinal))
                    { val = args[i + 1]; i++; }
                }
                s.Options[name] = val;
            }
            else if (a.Length > 1 && a[0] == '-' && a[1] != '-')
            {
                s.Options[a] = null; // 单破折号标志（如 -h）
            }
            else
            {
                s.Positionals.Add(a);
            }
        }
        return s;
    }
}
