using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace OCCMissionGoals.Services;

/// <summary>一次更新检查的结果。</summary>
public sealed class UpdateCheckResult
{
    /// <summary>检查是否成功完成（网络可达且解析成功）。</summary>
    public bool Succeeded { get; init; }

    /// <summary>是否存在比当前更新的版本。</summary>
    public bool HasUpdate { get; init; }

    /// <summary>最新版本号（GitHub Release 的 tag_name）。</summary>
    public string LatestVersion { get; init; } = string.Empty;

    /// <summary>Release 标题。</summary>
    public string ReleaseName { get; init; } = string.Empty;

    /// <summary>Release 网页地址。</summary>
    public string HtmlUrl { get; init; } = string.Empty;

    /// <summary>安装程序（setup.exe）的下载地址；不存在时为 null。</summary>
    public string? InstallerDownloadUrl { get; init; }

    /// <summary>面向用户的提示信息（已本地化）。</summary>
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// 自动更新：从 GitHub Releases 检查新版本，并支持下载安装程序与运行。
/// 更新源为 CialloForMyCode/OCC-s-Mission-Goals。
/// </summary>
public static class UpdateService
{
    public const string RepoOwner = "CialloForMyCode";
    public const string RepoName = "OCC-s-Mission-Goals";

    /// <summary>当前应用版本（来自程序集信息版本，去掉 + 提交哈希）。</summary>
    public static string CurrentVersion { get; } = DetectCurrentVersion();

    /// <summary>启动时是否自动检查更新。</summary>
    public static bool AutoCheckOnStartup
    {
        get => ConfigManager.Get("Update", "AutoCheck", "1") == "1";
        set => ConfigManager.Set("Update", "AutoCheck", value ? "1" : "0");
    }

    /// <summary>
    /// 查询最新 GitHub Release，并与当前版本比较。
    /// 无 Release（404）时视为「已是最新版本」。
    /// </summary>
    public static async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = CreateClient();
            var url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";

            using var response = await client.GetAsync(url, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return new UpdateCheckResult
                {
                    Succeeded = true,
                    HasUpdate = false,
                    Message = LocalizationManager.T("已是最新版本。", "You are up to date.", "У вас последняя версия.")
                };
            }

            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tag = GetString(root, "tag_name");
            var name = GetString(root, "name");
            var htmlUrl = GetString(root, "html_url");
            if (string.IsNullOrEmpty(name)) name = tag;

            string? installer = null;
            if (root.TryGetProperty("assets", out var assets) &&
                assets.ValueKind == JsonValueKind.Array)
            {
                installer = FindInstallerAsset(assets);
            }

            if (!IsNewerVersion(tag, CurrentVersion))
            {
                return new UpdateCheckResult
                {
                    Succeeded = true,
                    HasUpdate = false,
                    LatestVersion = tag,
                    HtmlUrl = htmlUrl,
                    Message = LocalizationManager.T(
                        $"已是最新版本（{CurrentVersion}）。",
                        $"You are up to date ({CurrentVersion}).",
                        $"У вас последняя версия ({CurrentVersion}).")
                };
            }

            return new UpdateCheckResult
            {
                Succeeded = true,
                HasUpdate = true,
                LatestVersion = tag,
                ReleaseName = name,
                HtmlUrl = htmlUrl,
                InstallerDownloadUrl = installer
            };
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult
            {
                Succeeded = false,
                Message = LocalizationManager.T(
                    $"检查更新失败：{ex.Message}",
                    $"Update check failed: {ex.Message}",
                    $"Не удалось проверить обновления: {ex.Message}")
            };
        }
    }

    /// <summary>
    /// 下载安装程序到系统临时目录，返回本地路径；失败返回 null。
    /// 通过 <paramref name="status"/> 上报进度文本（已本地化）。
    /// </summary>
    public static async Task<string?> DownloadInstallerAsync(
        string url,
        string fileName,
        IProgress<string>? status = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            status?.Report(LocalizationManager.T("正在下载更新…", "Downloading update…", "Загрузка обновления…"));

            using var client = CreateClient();
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var total = response.Content.Headers.ContentLength;
            var tmp = Path.Combine(Path.GetTempPath(), fileName);

            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var target = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = new byte[81920];
            long read = 0;
            int n;
            while ((n = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, n), cancellationToken);
                read += n;

                if (total is > 0)
                {
                    var percent = read * 100.0 / total.Value;
                    status?.Report(LocalizationManager.T(
                        $"正在下载更新… {percent:F0}%",
                        $"Downloading update… {percent:F0}%",
                        $"Загрузка обновления… {percent:F0}%"));
                }
            }

            return tmp;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            status?.Report(LocalizationManager.T(
                $"下载失败：{ex.Message}",
                $"Download failed: {ex.Message}",
                $"Ошибка загрузки: {ex.Message}"));
            return null;
        }
    }

    /// <summary>启动已下载的安装程序。</summary>
    public static void LaunchInstaller(string path)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }

    /// <summary>用默认浏览器打开网页。</summary>
    public static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch
        {
            // 打开失败时静默忽略。
        }
    }

    // ======================== 内部实现 ========================

    private static HttpClient CreateClient()
    {
        var client = new HttpClient();
        // GitHub API 要求提供 User-Agent。
        client.DefaultRequestHeaders.UserAgent.ParseAdd("OCCMissionGoals-Updater");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    private static string GetString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }

    /// <summary>在 Release 资产中挑出匹配当前架构的安装程序。</summary>
    private static string? FindInstallerAsset(JsonElement assets)
    {
        var arch = RuntimeInformation.ProcessArchitecture == Architecture.X86 ? "x86" : "x64";

        var candidates = new List<(string Name, string Url)>();
        foreach (var asset in assets.EnumerateArray())
        {
            var name = GetString(asset, "name");
            var url = GetString(asset, "browser_download_url");
            if (!string.IsNullOrEmpty(url))
                candidates.Add((name, url));
        }

        // 优先：<arch>-setup.exe，其次任意 -setup.exe，再次任意 .exe。
        var match = candidates.FirstOrDefault(c =>
            c.Name.EndsWith($"{arch}-setup.exe", StringComparison.OrdinalIgnoreCase));
        if (match.Name is not null) return match.Url;

        match = candidates.FirstOrDefault(c =>
            c.Name.EndsWith("-setup.exe", StringComparison.OrdinalIgnoreCase));
        if (match.Name is not null) return match.Url;

        match = candidates.FirstOrDefault(c =>
            c.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
        return match.Name is not null ? match.Url : null;
    }

    private static string DetectCurrentVersion()
    {
        var attr = typeof(UpdateService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        var version = attr?.InformationalVersion ?? string.Empty;

        // 去掉 +<commit hash> 之类的构建元数据。
        var plus = version.IndexOf('+');
        if (plus >= 0) version = version[..plus];

        if (string.IsNullOrWhiteSpace(version))
            version = typeof(UpdateService).Assembly.GetName().Version?.ToString() ?? "0.0.0";

        return version;
    }

    /// <summary>比较两个版本号（支持 v 前缀与 -prerelease 后缀）。</summary>
    private static bool IsNewerVersion(string latest, string current)
    {
        if (TryParseVersion(latest, out var l) && TryParseVersion(current, out var c))
        {
            var cmp = l.Major.CompareTo(c.Major);
            if (cmp != 0) return cmp > 0;
            cmp = l.Minor.CompareTo(c.Minor);
            if (cmp != 0) return cmp > 0;
            cmp = l.Patch.CompareTo(c.Patch);
            if (cmp != 0) return cmp > 0;
            cmp = l.Build.CompareTo(c.Build);
            if (cmp != 0) return cmp > 0;

            // 数字部分相同时：正式版 > 预发布版；两个预发布版按字符串比较。
            if (l.Pre is null && c.Pre is null) return false;
            if (l.Pre is null) return true;
            if (c.Pre is null) return false;
            return string.Compare(l.Pre, c.Pre, StringComparison.OrdinalIgnoreCase) > 0;
        }

        return string.Compare(latest, current, StringComparison.OrdinalIgnoreCase) > 0;
    }

    private static bool TryParseVersion(string value, out VersionParts parts)
    {
        parts = default;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var v = value.Trim().TrimStart('v', 'V');
        var dash = v.IndexOf('-');
        var core = dash >= 0 ? v[..dash] : v;
        string? pre = dash >= 0 ? v[(dash + 1)..] : null;

        var segs = core.Split('.');
        if (segs.Length < 2) return false;

        if (!int.TryParse(segs[0], out var major)) return false;
        if (!int.TryParse(segs[1], out var minor)) return false;

        var patch = 0;
        if (segs.Length > 2 && !int.TryParse(segs[2], out patch)) return false;

        var build = 0;
        if (segs.Length > 3 && !int.TryParse(segs[3], out build)) return false;

        parts = new VersionParts(major, minor, patch, build, pre);
        return true;
    }

    private readonly record struct VersionParts(int Major, int Minor, int Patch, int Build, string? Pre);
}
