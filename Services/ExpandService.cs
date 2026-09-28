using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using OCCMissionGoals.Models;

namespace OCCMissionGoals.Services;

/// <summary>
/// 扩展服务：扫描、装载与管理扩展插件。
///
/// 扩展安装在 exe 同目录的 Expand 文件夹，每个扩展一个子目录，内含 expand.json 清单。
/// 清单记录扩展的身份（Id / 显示名 / 版本 / 作者 / 简介）与启用状态，扩展中心据此列出、
/// 启用或禁用它们。
///
/// 界面外观（配色、圆角、边框粗细、间距、字号）一律由主题决定，见 <see cref="ThemeManager"/>
/// 与 Themes 目录；扩展不再覆盖资源键。语言包在 Languages、主题在 Themes、扩展在 Expand，
/// 三者互不影响。
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

    /// <summary>已扫描到的全部扩展（含被禁用与清单有问题的）。</summary>
    public static IReadOnlyList<ExpandInfo> All => _all;

    /// <summary>确保本地扩展插件目录存在（放入扩展前调用）。</summary>
    public static void EnsureDirectory() => Directory.CreateDirectory(LocalExpandDirectory);

    /// <summary>
    /// 重新扫描扩展目录。程序启动时调用一次；启用 / 禁用 / 增删扩展之后也调用。
    /// </summary>
    public static void Reload() => Scan();

    /// <summary>启用 / 禁用扩展：改写它的 expand.json，然后重新扫描。返回错误信息，成功为 null。</summary>
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

        return info;
    }
}
