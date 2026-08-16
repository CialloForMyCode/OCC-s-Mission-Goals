using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace OCCMissionGoals.Services;

/// <summary>GitHub 当前登录用户的信息（来自 GET /user）。</summary>
public sealed class GitHubUser
{
    /// <summary>登录名（如 I-AM-SOLO）。</summary>
    public string Login { get; set; } = string.Empty;

    /// <summary>显示名（可能为空）。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>个人简介（可能为空）。</summary>
    public string Bio { get; set; } = string.Empty;

    /// <summary>头像 URL。</summary>
    public string AvatarUrl { get; set; } = string.Empty;

    /// <summary>个人主页 URL。</summary>
    public string HtmlUrl { get; set; } = string.Empty;

    /// <summary>所在地（可能为空）。</summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>公司（可能为空）。</summary>
    public string Company { get; set; } = string.Empty;

    /// <summary>公开仓库数量。</summary>
    public int PublicRepos { get; set; }

    /// <summary>关注者数量。</summary>
    public int Followers { get; set; }

    /// <summary>正在关注数量。</summary>
    public int Following { get; set; }
}

/// <summary>GitHub 仓库摘要（来自 GET /user/repos）。</summary>
public sealed class GitHubRepo
{
    /// <summary>完整仓库名（owner/repo）。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>仓库主页 URL。</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>默认分支名。</summary>
    public string DefaultBranch { get; set; } = string.Empty;
}

/// <summary>
/// GitHub 登录 / 用户信息服务。使用 Personal Access Token 调用 GitHub REST API。
/// 令牌持久化到 config.ini 的 [Github] 节。
/// </summary>
public static class GitHubService
{
    private const string Section = "Github";
    private const string UserAgent = "OCCMissionGoals";

    /// <summary>Personal Access Token（空表示未登录）。</summary>
    public static string Token
    {
        get => ConfigManager.Get(Section, "Token", "");
        set => ConfigManager.Set(Section, "Token", value ?? "");
    }

    /// <summary>是否已保存令牌（不一定已验证）。</summary>
    public static bool HasToken => !string.IsNullOrWhiteSpace(Token);

    /// <summary>清除登录状态（删除已保存的令牌）。</summary>
    public static void Logout() => Token = "";

    /// <summary>
    /// 使用令牌获取当前用户信息。令牌无效时抛出异常。
    /// </summary>
    public static async Task<GitHubUser> FetchUserAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("Token is required.", nameof(token));

        using var client = CreateClient(token);
        using var response = await client.GetAsync("https://api.github.com/user", cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        return new GitHubUser
        {
            Login = GetString(root, "login"),
            Name = GetString(root, "name"),
            Bio = GetString(root, "bio"),
            AvatarUrl = GetString(root, "avatar_url"),
            HtmlUrl = GetString(root, "html_url"),
            Location = GetString(root, "location"),
            Company = GetString(root, "company"),
            PublicRepos = GetInt(root, "public_repos"),
            Followers = GetInt(root, "followers"),
            Following = GetInt(root, "following"),
        };
    }

    /// <summary>获取当前登录用户有权限的仓库列表（最多 100 个，按更新时间倒序）。</summary>
    public static async Task<List<GitHubRepo>> FetchRepositoriesAsync(
        CancellationToken cancellationToken = default)
    {
        var token = Token;
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("Not signed in to GitHub.");

        using var client = CreateClient(token);
        using var response = await client.GetAsync(
            "https://api.github.com/user/repos?per_page=100&sort=updated&affiliation=owner,collaborator,organization_member",
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var result = new List<GitHubRepo>();
        foreach (var element in doc.RootElement.EnumerateArray())
        {
            result.Add(new GitHubRepo
            {
                Name = GetString(element, "full_name"),
                Url = GetString(element, "html_url"),
                DefaultBranch = GetString(element, "default_branch"),
            });
        }

        return result;
    }

    /// <summary>获取指定仓库的分支名称列表。</summary>
    public static async Task<List<string>> FetchBranchesAsync(
        string repoUrl,
        CancellationToken cancellationToken = default)
    {
        var (owner, repo) = ParseRepositoryUrl(repoUrl);
        var token = Token;
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("Not signed in to GitHub.");

        using var client = CreateClient(token);
        using var response = await client.GetAsync(
            $"https://api.github.com/repos/{owner}/{repo}/branches?per_page=100",
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var result = new List<string>();
        foreach (var element in doc.RootElement.EnumerateArray())
        {
            var name = GetString(element, "name");
            if (!string.IsNullOrEmpty(name))
                result.Add(name);
        }

        return result;
    }

    /// <summary>下载字节流（用于头像等资源）。失败返回 null。</summary>
    public static async Task<byte[]?> DownloadBytesAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;

        try
        {
            using var client = CreateClient(null);
            using var response = await client.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 从仓库 URL 解析 owner / repo。支持 https://github.com/owner/repo(.git)、
    /// git@github.com:owner/repo.git 以及纯 owner/repo 形式。
    /// </summary>
    public static (string Owner, string Repo) ParseRepositoryUrl(string url)
    {
        var s = (url ?? string.Empty).Trim();
        if (s.Length == 0)
            throw new FormatException("Repository URL is empty.");

        string? path = null;
        if (s.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase))
            path = s["https://github.com/".Length..];
        else if (s.StartsWith("http://github.com/", StringComparison.OrdinalIgnoreCase))
            path = s["http://github.com/".Length..];
        else if (s.StartsWith("git@github.com:", StringComparison.OrdinalIgnoreCase))
            path = s["git@github.com:".Length..];
        else if (!s.Contains("://") && !s.Contains('@'))
            path = s;

        if (string.IsNullOrWhiteSpace(path))
            throw new FormatException("Unsupported repository URL.");

        path = path.Trim().TrimEnd('/');
        if (path.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            path = path[..^4];

        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            throw new FormatException("Repository URL must include owner and repository name.");

        return (parts[0], parts[1]);
    }

    /// <summary>
    /// 通过 GitHub Contents API 将文件推送到指定仓库分支。返回 null 表示成功，
    /// 否则返回错误信息（含 HTTP 状态与 GitHub 返回的 message）。
    /// </summary>
    public static async Task<string?> PushFileAsync(
        string repoUrl,
        string branch,
        string remotePath,
        string content,
        string commitMessage,
        CancellationToken cancellationToken = default)
    {
        var (owner, repo) = ParseRepositoryUrl(repoUrl);

        var token = Token;
        if (string.IsNullOrWhiteSpace(token))
            return "Not signed in to GitHub.";

        var branchName = string.IsNullOrWhiteSpace(branch) ? "main" : branch.Trim();
        remotePath = (remotePath ?? string.Empty).Trim().TrimStart('/');
        if (remotePath.Length == 0)
            return "Remote file path is empty.";

        var encodedPath = string.Join("/", remotePath.Split('/').Select(Uri.EscapeDataString));
        var contentsUrl = $"https://api.github.com/repos/{owner}/{repo}/contents/{encodedPath}";

        using var client = CreateClient(token);

        // Contents API 不会自动创建分支，若目标分支不存在则先从默认分支创建。
        var branchError = await EnsureBranchAsync(client, owner, repo, branchName, cancellationToken);
        if (branchError != null)
            return branchError;

        // 先读取已有文件，获取其 sha（存在则需要带上，否则更新会失败）。
        string? sha = null;
        try
        {
            var getUrl = $"{contentsUrl}?ref={Uri.EscapeDataString(branchName)}";
            using var getResponse = await client.GetAsync(getUrl, cancellationToken);
            if (getResponse.IsSuccessStatusCode)
            {
                var json = await getResponse.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("sha", out var shaElement))
                    sha = shaElement.GetString();
            }
            else if (getResponse.StatusCode != HttpStatusCode.NotFound)
            {
                return $"Failed to read remote file (HTTP {(int)getResponse.StatusCode} {getResponse.ReasonPhrase}).";
            }
        }
        catch (Exception ex)
        {
            return $"Failed to read remote file: {ex.Message}";
        }

        // 组装 PUT 请求体；sha 仅在更新已有文件时携带。
        var body = new Dictionary<string, object?>
        {
            ["message"] = commitMessage,
            ["content"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(content)),
            ["branch"] = branchName,
        };
        if (!string.IsNullOrEmpty(sha))
            body["sha"] = sha;

        try
        {
            var payload = JsonSerializer.Serialize(body);
            using var putContent = new StringContent(payload, Encoding.UTF8, "application/vnd.github+json");
            using var putResponse = await client.PutAsync(contentsUrl, putContent, cancellationToken);
            if (putResponse.IsSuccessStatusCode)
                return null;

            var errorBody = await putResponse.Content.ReadAsStringAsync(cancellationToken);
            return $"Push failed (HTTP {(int)putResponse.StatusCode} {putResponse.ReasonPhrase}): {ExtractGitHubError(errorBody)}";
        }
        catch (Exception ex)
        {
            return $"Push failed: {ex.Message}";
        }
    }

    /// <summary>
    /// 确保目标分支存在：已存在则返回 null；不存在则从仓库默认分支创建。
    /// 返回 null 表示成功，否则返回错误信息。
    /// </summary>
    private static async Task<string?> EnsureBranchAsync(
        HttpClient client,
        string owner,
        string repo,
        string branchName,
        CancellationToken cancellationToken)
    {
        var refUrl = $"https://api.github.com/repos/{owner}/{repo}/git/ref/heads/{Uri.EscapeDataString(branchName)}";

        using var getRef = await client.GetAsync(refUrl, cancellationToken);
        if (getRef.IsSuccessStatusCode)
            return null;
        if (getRef.StatusCode != HttpStatusCode.NotFound)
            return $"Failed to check branch (HTTP {(int)getRef.StatusCode} {getRef.ReasonPhrase}).";

        // 分支不存在 → 获取默认分支的 head sha，用于创建新分支。
        string? baseSha;
        try
        {
            using var repoResponse = await client.GetAsync(
                $"https://api.github.com/repos/{owner}/{repo}", cancellationToken);
            repoResponse.EnsureSuccessStatusCode();

            var repoJson = await repoResponse.Content.ReadAsStringAsync(cancellationToken);
            using var repoDoc = JsonDocument.Parse(repoJson);
            var defaultBranch = GetString(repoDoc.RootElement, "default_branch");
            if (string.IsNullOrEmpty(defaultBranch))
                return "Cannot determine the repository's default branch.";

            using var baseResponse = await client.GetAsync(
                $"https://api.github.com/repos/{owner}/{repo}/git/ref/heads/{Uri.EscapeDataString(defaultBranch)}",
                cancellationToken);
            if (!baseResponse.IsSuccessStatusCode)
                return $"Failed to read default branch (HTTP {(int)baseResponse.StatusCode} {baseResponse.ReasonPhrase}).";

            var baseJson = await baseResponse.Content.ReadAsStringAsync(cancellationToken);
            using var baseDoc = JsonDocument.Parse(baseJson);
            baseSha = baseDoc.RootElement.TryGetProperty("object", out var obj)
                ? GetString(obj, "sha")
                : string.Empty;
            if (string.IsNullOrEmpty(baseSha))
                return "Cannot determine the default branch's head commit.";
        }
        catch (Exception ex)
        {
            return $"Failed to create branch: {ex.Message}";
        }

        try
        {
            var createBody = JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["ref"] = $"refs/heads/{branchName}",
                ["sha"] = baseSha,
            });
            using var createContent = new StringContent(createBody, Encoding.UTF8, "application/vnd.github+json");
            using var createResponse = await client.PostAsync(
                $"https://api.github.com/repos/{owner}/{repo}/git/refs", createContent, cancellationToken);
            if (createResponse.IsSuccessStatusCode)
                return null;

            var errorBody = await createResponse.Content.ReadAsStringAsync(cancellationToken);
            return $"Failed to create branch (HTTP {(int)createResponse.StatusCode} {createResponse.ReasonPhrase}): {ExtractGitHubError(errorBody)}";
        }
        catch (Exception ex)
        {
            return $"Failed to create branch: {ex.Message}";
        }
    }

    /// <summary>从 GitHub 错误响应中提取 message 字段，失败则返回原始片段。</summary>
    private static string ExtractGitHubError(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return string.Empty;

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("message", out var message))
                return message.GetString() ?? body;
        }
        catch (JsonException)
        {
        }

        var trimmed = body.Trim();
        return trimmed.Length > 200 ? trimmed[..200] + "…" : trimmed;
    }

    private static HttpClient CreateClient(string? token)
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        if (!string.IsNullOrWhiteSpace(token))
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static string GetString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }

    private static int GetInt(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : 0;
    }
}
