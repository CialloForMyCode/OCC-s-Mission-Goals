using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Windows;
using System.Windows.Markup;
using OCCMissionGoals.Models;

namespace OCCMissionGoals.Services;

/// <summary>
/// 扩展服务：扫描、装载与管理扩展插件。
///
/// 扩展安装在 exe 同目录的 Expand 文件夹，每个扩展一个子目录，内含 expand.json 清单
/// 与可选的 XAML 文件。装载时把扩展提供的资源写入 Application.Resources，覆盖主题里的
/// 同名键（颜色、圆角、边框粗细……），因此「改变板块外观」不需要改动程序本体。
/// 语言包安装在 Languages 文件夹、主题安装在 Themes 文件夹，三者互不影响。
/// </summary>
public static class ExpandService
{
    private const string ExpandDir = "Expand";
    private const string ManifestName = "expand.json";

    private static readonly List<ExpandInfo> _all = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>本地扩展插件目录（exe 同目录下的 Expand）。</summary>
    public static string LocalExpandDirectory => Path.Combine(AppContext.BaseDirectory, ExpandDir);

    /// <summary>已扫描到的全部扩展（含被禁用与装载失败的）。</summary>
    public static IReadOnlyList<ExpandInfo> All => _all;

    /// <summary>已启用且未出错、会被装载的扩展。</summary>
    public static IEnumerable<ExpandInfo> Enabled =>
        _all.Where(e => e.Enabled && e.LoadError is null);

    /// <summary>确保本地扩展插件目录存在（放入扩展前调用）。</summary>
    public static void EnsureDirectory() => Directory.CreateDirectory(LocalExpandDirectory);

    /// <summary>
    /// 重新扫描扩展目录并立即应用覆盖。程序启动时调用一次；
    /// 启用 / 禁用 / 增删扩展之后也调用。
    /// </summary>
    public static void Reload()
    {
        Scan();
        ApplyOverrides();
    }

    /// <summary>
    /// 把已启用扩展的资源写入 Application.Resources，覆盖主题中的同名键。
    /// 必须在主题应用之后调用（见 <see cref="ThemeManager.ApplyTheme"/>），
    /// 否则切主题时会被主题自己的配色覆盖回去。
    /// </summary>
    public static void ApplyOverrides()
    {
        var app = Application.Current;
        if (app is null) return;

        // Enabled 是惰性序列，而下面的装载会给 LoadError 赋值，这里先取快照。
        foreach (var ext in Enabled.ToList())
        {
            ext.AppliedKeyCount = 0;
            if (string.IsNullOrWhiteSpace(ext.Resources)) continue;

            var rd = LoadResourceDictionary(ext, ext.Resources);
            if (rd is null) continue;

            var applied = 0;
            foreach (var keyObj in rd.Keys)
            {
                if (keyObj is not string key || string.IsNullOrWhiteSpace(key)) continue;
                app.Resources[key] = rd[keyObj];
                applied++;
            }
            ext.AppliedKeyCount = applied;
        }
    }

    /// <summary>启用 / 禁用扩展：改写它的 expand.json，然后重新应用覆盖。返回错误信息，成功为 null。</summary>
    public static string? SetEnabled(ExpandInfo ext, bool enabled)
    {
        if (string.IsNullOrWhiteSpace(ext.Directory))
            return "扩展目录未知。";

        var manifest = Path.Combine(ext.Directory, ManifestName);
        try
        {
            ext.Enabled = enabled;
            File.WriteAllText(manifest, JsonSerializer.Serialize(ext, JsonOptions));
        }
        catch (Exception ex)
        {
            return $"写入 expand.json 失败：{ex.Message}";
        }

        // 先让主题把自己的配色重新落回去，再重新套用扩展，避免禁用后残留旧的覆盖值。
        ThemeManager.ApplyTheme(ThemeManager.IsDark);
        Reload();
        return null;
    }

    // ==================== 扫描与读取 ====================

    /// <summary>扫描 Expand 目录：有 expand.json 的子目录才算扩展。</summary>
    private static void Scan()
    {
        _all.Clear();

        var dir = LocalExpandDirectory;
        if (!Directory.Exists(dir)) return;

        string[] subs;
        try
        {
            subs = Directory.GetDirectories(dir);
        }
        catch
        {
            return;
        }

        foreach (var sub in subs.OrderBy(d => d, StringComparer.Ordinal))
        {
            var manifest = Path.Combine(sub, ManifestName);
            if (!File.Exists(manifest)) continue;

            _all.Add(ReadManifest(manifest, sub));
        }
    }

    /// <summary>
    /// 读取一个扩展目录的清单。解析失败时仍返回一个带 <see cref="ExpandInfo.LoadError"/> 的项，
    /// 这样扩展中心能把它显示出来并说明原因，而不是悄悄消失。
    /// </summary>
    private static ExpandInfo ReadManifest(string manifest, string directory)
    {
        ExpandInfo info;
        try
        {
            info = JsonSerializer.Deserialize<ExpandInfo>(File.ReadAllText(manifest), JsonOptions)
                   ?? new ExpandInfo();
        }
        catch (Exception ex)
        {
            return new ExpandInfo
            {
                Directory = directory,
                Name = Path.GetFileName(directory),
                Id = "expand:" + Path.GetFileName(directory),
                LoadError = "expand.json 解析失败：" + ex.Message
            };
        }

        info.Directory = directory;
        if (string.IsNullOrWhiteSpace(info.Id)) info.Id = "expand:" + Path.GetFileName(directory);
        if (string.IsNullOrWhiteSpace(info.Name)) info.Name = Path.GetFileName(directory);

        // 清单里声明的文件必须真实存在，否则装载时静默失效，很难排查。
        if (!string.IsNullOrWhiteSpace(info.Resources) && !File.Exists(ResolveFile(info, info.Resources)))
            info.LoadError = $"资源文件不存在：{info.Resources}";
        else if (!string.IsNullOrWhiteSpace(info.Layout) && !File.Exists(ResolveFile(info, info.Layout)))
            info.LoadError = $"布局文件不存在：{info.Layout}";

        return info;
    }

    /// <summary>把清单里的相对路径解析成扩展目录下的绝对路径；已经是绝对路径时原样返回。</summary>
    private static string ResolveFile(ExpandInfo ext, string relative) =>
        Path.IsPathRooted(relative) ? relative : Path.Combine(ext.Directory, relative);

    /// <summary>读取扩展提供的 ResourceDictionary；未指定、文件缺失或解析失败时返回 null。</summary>
    private static ResourceDictionary? LoadResourceDictionary(ExpandInfo ext, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative)) return null;

        var file = ResolveFile(ext, relative);
        try
        {
            if (!File.Exists(file))
            {
                ext.LoadError = $"资源文件不存在：{relative}";
                return null;
            }

            using var stream = File.OpenRead(file);
            return XamlReader.Load(stream) as ResourceDictionary;
        }
        catch (Exception ex)
        {
            ext.LoadError = $"资源文件加载失败：{ex.Message}";
            return null;
        }
    }
}
