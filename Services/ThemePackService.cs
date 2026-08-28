using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;

namespace OCCMissionGoals.Services;

/// <summary>一个可下载的主题（对应仓库 Themes 目录下的一个 *.xaml 文件）。</summary>
public sealed record ThemePack(string Name, string FileName, string DownloadUrl);

/// <summary>
/// 主题包服务：从 GitHub 仓库的 Themes 目录列出、下载安装与卸载主题。
/// 每个 *.xaml 文件是一套界面配色（含 Light.* / Dark.*），<c>Default.xaml</c> 为内置默认主题，
/// 不列入可安装列表。安装后写入应用目录下的 Themes 文件夹，重启后仍由 <see cref="ThemeManager"/> 自动加载。
/// </summary>
public static class ThemePackService
{
    public const string RepoOwner = UpdateService.RepoOwner;
    public const string RepoName = UpdateService.RepoName;
    private const string Branch = "master";
    private const string ThemesDir = "Themes";

    /// <summary>内置默认主题文件名，不参与安装/卸载。</summary>
    private const string DefaultFileName = "Default.xaml";

    /// <summary>本地主题目录（exe 同目录下的 Themes）。</summary>
    public static string LocalThemesDirectory =>
        Path.Combine(AppContext.BaseDirectory, ThemesDir);

    /// <summary>某个主题是否已安装（本地存在对应主题文件）。</summary>
    public static bool IsInstalled(string fileName) =>
        !string.IsNullOrWhiteSpace(fileName) &&
        File.Exists(Path.Combine(LocalThemesDirectory, fileName));

    /// <summary>
    /// 列出仓库中可用的主题。每个主题包含显示名、文件名与下载地址。
    /// 已安装的主题直接读取本地 <c>__theme_name</c>，未安装的则下载一次文件以读取主题名。
    /// </summary>
    public static async Task<List<ThemePack>> FetchAvailableAsync(
        CancellationToken cancellationToken = default)
    {
        var result = new List<ThemePack>();

        using var client = CreateClient();
        var url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/contents/{ThemesDir}?ref={Uri.EscapeDataString(Branch)}";

        using var response = await client.GetAsync(url, cancellationToken);
        // 目录不存在（404）时视为没有可用的主题。
        if (response.StatusCode == HttpStatusCode.NotFound)
            return result;
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);

        foreach (var element in doc.RootElement.EnumerateArray())
        {
            if (!string.Equals(GetString(element, "type"), "file", StringComparison.OrdinalIgnoreCase))
                continue;

            var fileName = GetString(element, "name");
            if (!fileName.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
                continue;

            // 跳过内置默认主题。
            if (string.Equals(fileName, DefaultFileName, StringComparison.OrdinalIgnoreCase))
                continue;

            var downloadUrl = GetString(element, "download_url");
            var fallbackName = Path.GetFileNameWithoutExtension(fileName);

            // 已安装 → 直接用本地文件解析出的显示名。
            var localName = ReadLocalThemeName(fileName);
            if (!string.IsNullOrWhiteSpace(localName))
            {
                result.Add(new ThemePack(localName, fileName, downloadUrl));
                continue;
            }

            // 未安装 → 下载文件内容以读取 __theme_name。
            var name = await FetchRemoteThemeNameAsync(fileName, client, cancellationToken);
            result.Add(new ThemePack(
                string.IsNullOrWhiteSpace(name) ? fallbackName : name,
                fileName,
                downloadUrl));
        }

        return result;
    }

    /// <summary>
    /// 下载主题并写入本地 Themes 目录。返回 null 表示成功，否则返回错误信息。
    /// </summary>
    public static async Task<string?> InstallAsync(
        ThemePack pack,
        CancellationToken cancellationToken = default)
    {
        var fileName = pack.FileName;
        if (string.IsNullOrWhiteSpace(fileName))
            return LocalizationManager.T("无效的文件名。");

        try
        {
            using var client = CreateClient();
            using var response = await GetRawAsync(client, fileName, cancellationToken);
            response.EnsureSuccessStatusCode();
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);

            Directory.CreateDirectory(LocalThemesDirectory);
            var target = Path.Combine(LocalThemesDirectory, fileName);
            await File.WriteAllBytesAsync(target, bytes, cancellationToken);
            return null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return LocalizationManager.T("下载失败：{0}", ex.Message);
        }
    }

    /// <summary>
    /// 卸载主题：删除本地 Themes 目录中对应的 *.xaml 文件。
    /// 返回 null 表示成功（或本地本就没有该文件），否则返回错误信息。
    /// </summary>
    public static string? Uninstall(ThemePack pack)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(pack.FileName))
            {
                var target = Path.Combine(LocalThemesDirectory, pack.FileName);
                if (File.Exists(target))
                {
                    File.Delete(target);
                    return null;
                }
            }

            // 兜底：按主题显示名定位本地文件（可能文件名与远程不一致）。
            foreach (var file in Directory.GetFiles(LocalThemesDirectory, "*.xaml"))
            {
                if (string.Equals(file, DefaultFileName, StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    var name = ReadThemeName(file);
                    if (string.Equals(name, pack.Name, StringComparison.Ordinal))
                    {
                        File.Delete(file);
                        return null;
                    }
                }
                catch
                {
                    // 忽略无法解析的文件，继续查找下一个。
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            return LocalizationManager.T("卸载失败：{0}", ex.Message);
        }
    }

    // ======================== 内部实现 ========================

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("OCCMissionGoals-ThemePacks");
        return client;
    }

    /// <summary>
    /// 返回某个主题文件的 raw 内容地址。走 <c>api.github.com</c> 的 contents 接口并携带
    /// <c>Accept: application/vnd.github.raw</c>，直接返回文件原文，避免下载卡死。
    /// </summary>
    private static string ApiRawUrl(string fileName) =>
        $"https://api.github.com/repos/{RepoOwner}/{RepoName}/contents/{ThemesDir}/{Uri.EscapeDataString(fileName)}?ref={Uri.EscapeDataString(Branch)}";

    /// <summary>通过 api.github.com 的 raw 接口下载文件原始内容。</summary>
    private static async Task<HttpResponseMessage> GetRawAsync(
        HttpClient client, string fileName, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ApiRawUrl(fileName));
        request.Headers.Accept.ParseAdd("application/vnd.github.raw");
        return await client.SendAsync(request, cancellationToken);
    }

    /// <summary>读取本地主题文件的显示名（__theme_name）；不存在或解析失败返回 null。</summary>
    private static string? ReadLocalThemeName(string fileName)
    {
        var path = Path.Combine(LocalThemesDirectory, fileName);
        if (!File.Exists(path)) return null;
        return ReadThemeName(path);
    }

    private static string? ReadThemeName(string file)
    {
        try
        {
            using var stream = File.OpenRead(file);
            var rd = (ResourceDictionary)XamlReader.Load(stream);
            return rd["__theme_name"] as string;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>下载远程 *.xaml 并解析 __theme_name；失败返回 null。</summary>
    private static async Task<string?> FetchRemoteThemeNameAsync(
        string fileName,
        HttpClient client,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return null;

        try
        {
            using var response = await GetRawAsync(client, fileName, cancellationToken);
            response.EnsureSuccessStatusCode();
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
            var rd = (ResourceDictionary)XamlReader.Load(stream);
            return rd["__theme_name"] as string;
        }
        catch
        {
            return null;
        }
    }

    private static string GetString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }
}
