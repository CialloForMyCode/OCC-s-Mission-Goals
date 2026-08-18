using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;

namespace OCCMissionGoals.Services;

/// <summary>一个可下载的语言包（对应仓库 Languages 目录下的一个 *.xaml 文件）。</summary>
public sealed record LanguagePack(string Code, string Name, string FileName, string DownloadUrl);

/// <summary>
/// 语言包服务：从 GitHub 仓库的 Languages 目录列出、下载安装与卸载语言包。
/// 语言包即扩展中心的「扩展」——每个 *.xaml 文件是界面的一种语言。
/// 安装后写入应用目录下的 Languages 文件夹，重启后仍由 <see cref="LocalizationManager"/> 自动加载。
/// </summary>
public static class LanguagePackService
{
    public const string RepoOwner = UpdateService.RepoOwner;
    public const string RepoName = UpdateService.RepoName;
    private const string Branch = "master";
    private const string LanguagesDir = "Languages";

    /// <summary>本地语言包目录（exe 同目录下的 Languages）。</summary>
    public static string LocalLanguagesDirectory =>
        Path.Combine(AppContext.BaseDirectory, LanguagesDir);

    /// <summary>某个语言代码是否已安装（本地存在对应语言文件）。</summary>
    public static bool IsInstalled(string code) =>
        LocalizationManager.Instance.AvailableLanguages.Any(
            l => string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// 列出仓库中可用的语言包。每个语言包包含语言代码、显示名、文件名与下载地址。
    /// 已安装的语言包直接读取本地元数据（无网络请求），未安装的则下载一次文件以读取语言名。
    /// </summary>
    public static async Task<List<LanguagePack>> FetchAvailableAsync(
        CancellationToken cancellationToken = default)
    {
        var result = new List<LanguagePack>();

        using var client = CreateClient();
        var url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/contents/{LanguagesDir}?ref={Uri.EscapeDataString(Branch)}";

        using var response = await client.GetAsync(url, cancellationToken);
        // 目录不存在（404）时视为没有可用的语言包。
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

            var downloadUrl = GetString(element, "download_url");
            var fallbackCode = Path.GetFileNameWithoutExtension(fileName);

            // 已安装 → 直接用 LocalizationManager 解析出的代码与显示名。
            var installed = LocalizationManager.Instance.AvailableLanguages.FirstOrDefault(
                l => string.Equals(l.Code, fallbackCode, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(installed.Code))
            {
                result.Add(new LanguagePack(installed.Code, installed.Name, fileName, downloadUrl));
                continue;
            }

            // 未安装 → 下载文件内容以读取 __lang_code / __lang_name。
            var meta = await FetchRemoteMetadataAsync(fileName, client, cancellationToken);
            result.Add(new LanguagePack(
                meta?.Code ?? fallbackCode,
                meta?.Name ?? fallbackCode,
                fileName,
                downloadUrl));
        }

        return result;
    }

    /// <summary>
    /// 下载语言包并写入本地 Languages 目录。返回 null 表示成功，否则返回错误信息。
    /// </summary>
    public static async Task<string?> InstallAsync(
        LanguagePack pack,
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

            Directory.CreateDirectory(LocalLanguagesDirectory);
            var target = Path.Combine(LocalLanguagesDirectory, fileName);
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
    /// 卸载语言包：删除本地 Languages 目录中对应的 *.xaml 文件。
    /// 返回 null 表示成功（或本地本就没有该文件），否则返回错误信息。
    /// </summary>
    public static string? Uninstall(LanguagePack pack)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(pack.FileName))
            {
                var target = Path.Combine(LocalLanguagesDirectory, pack.FileName);
                if (File.Exists(target))
                {
                    File.Delete(target);
                    return null;
                }
            }

            // 兜底：按语言代码定位本地文件（可能文件名与远程不一致）。
            foreach (var file in Directory.GetFiles(LocalLanguagesDirectory, "*.xaml"))
            {
                try
                {
                    using var stream = File.OpenRead(file);
                    var rd = (ResourceDictionary)XamlReader.Load(stream);
                    if (string.Equals(rd["__lang_code"] as string, pack.Code, StringComparison.OrdinalIgnoreCase))
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
        client.DefaultRequestHeaders.UserAgent.ParseAdd("OCCMissionGoals-LanguagePacks");
        return client;
    }

    /// <summary>
    /// 返回某个语言文件的 raw 内容地址。走 <c>api.github.com</c> 的 contents 接口并携带
    /// <c>Accept: application/vnd.github.raw</c>，直接返回文件原文；
    /// 相比 <c>download_url</c>（raw.githubusercontent.com），该域名在部分地区更稳定，
    /// 避免下载卡死导致「再次下载无反应」。
    /// </summary>
    private static string ApiRawUrl(string fileName) =>
        $"https://api.github.com/repos/{RepoOwner}/{RepoName}/contents/{LanguagesDir}/{Uri.EscapeDataString(fileName)}?ref={Uri.EscapeDataString(Branch)}";

    /// <summary>通过 api.github.com 的 raw 接口下载文件原始内容。</summary>
    private static async Task<HttpResponseMessage> GetRawAsync(
        HttpClient client, string fileName, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ApiRawUrl(fileName));
        request.Headers.Accept.ParseAdd("application/vnd.github.raw");
        return await client.SendAsync(request, cancellationToken);
    }

    /// <summary>下载远程 *.xaml 并解析 __lang_code / __lang_name；失败返回 null。</summary>
    private static async Task<(string? Code, string? Name)?> FetchRemoteMetadataAsync(
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
            return (rd["__lang_code"] as string, rd["__lang_name"] as string);
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
