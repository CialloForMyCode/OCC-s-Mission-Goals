using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace OCCMissionGoals.Services;

/// <summary>
/// Git blob 哈希：<c>sha1("blob {字节数}\0" + 内容)</c>，与 GitHub contents API 返回的
/// <c>sha</c> 可直接比较。扩展中心用它判断已安装的语言包 / 主题是否落后于仓库里的版本。
/// </summary>
public static class BlobHash
{
    /// <summary>算出一段内容的 Git blob SHA（小写十六进制）。</summary>
    public static string Compute(byte[] content)
    {
        var header = Encoding.UTF8.GetBytes($"blob {content.Length}\0");

        using var sha1 = SHA1.Create();
        sha1.TransformBlock(header, 0, header.Length, null, 0);
        sha1.TransformFinalBlock(content, 0, content.Length);
        return Convert.ToHexString(sha1.Hash!).ToLowerInvariant();
    }

    /// <summary>
    /// 按 git 的文本规范化把 CRLF 折成 LF。Windows 上安装 / 检出的文件可能是 CRLF，
    /// 而仓库里存的是 LF：不做这一步，内容相同的文件也会被判成「有更新」。
    /// 没有 CRLF 时原样返回（不复制数组）。
    /// </summary>
    public static byte[] NormalizeLineEndings(byte[] content)
    {
        var crlf = 0;
        for (var i = 0; i < content.Length - 1; i++)
        {
            if (content[i] == (byte)'\r' && content[i + 1] == (byte)'\n') crlf++;
        }

        if (crlf == 0) return content;

        var normalized = new byte[content.Length - crlf];
        var j = 0;
        for (var i = 0; i < content.Length; i++)
        {
            if (content[i] == (byte)'\r' && i + 1 < content.Length && content[i + 1] == (byte)'\n')
                continue;

            normalized[j++] = content[i];
        }

        return normalized;
    }

    /// <summary>
    /// 本地文件是否与仓库中的内容一致（按 blob SHA 比较，容忍 CRLF / LF 差异）。
    /// 文件读不出来时抛出异常，由调用方决定如何处理。
    /// </summary>
    public static bool MatchesFile(string path, string expectedSha)
    {
        if (string.IsNullOrWhiteSpace(expectedSha)) return false;

        var content = File.ReadAllBytes(path);
        if (string.Equals(Compute(content), expectedSha, StringComparison.OrdinalIgnoreCase))
            return true;

        var normalized = NormalizeLineEndings(content);
        return !ReferenceEquals(normalized, content) &&
               string.Equals(Compute(normalized), expectedSha, StringComparison.OrdinalIgnoreCase);
    }
}
