using System.Text;
using OCCMissionGoals.Models;

namespace OCCMissionGoals.Services;

/// <summary>生成条目「复制信息」按钮所复制的纯文本内容。</summary>
public static class EntryCopyFormatter
{
    /// <summary>
    /// 按「标题 / 简介 / 详情 / 相关文件（函数 行:列）」的格式输出条目信息。
    /// </summary>
    public static string BuildText(GoalEntry entry)
    {
        var sb = new StringBuilder();

        sb.AppendLine(LocalizationManager.T("标题") + ": " + entry.Title);
        sb.AppendLine(LocalizationManager.T("简介") + ": " + entry.Brief);
        sb.AppendLine(LocalizationManager.T("详情") + ": " + entry.Detail);
        sb.AppendLine(LocalizationManager.T("相关文件") + ":");

        if (entry.RelatedFiles.Count == 0)
        {
            sb.AppendLine("（无）");
        }
        else
        {
            foreach (var f in entry.RelatedFiles)
            {
                var func = string.IsNullOrWhiteSpace(f.Function) ? string.Empty : f.Function + " ";
                sb.AppendLine($"{f.Path}（{func}{f.Line}:{f.Column}）");
            }
        }

        return sb.ToString().TrimEnd();
    }
}
