namespace OCCMissionGoals.Models;

/// <summary>
/// 按搜索模式对单个条目做匹配判断，供未完成 / 已完成列表共用。
/// </summary>
public static class SearchMatcher
{
    /// <summary>
    /// 判断条目是否命中关键词。
    /// </summary>
    /// <param name="entry">待判断条目。</param>
    /// <param name="keyword">关键词（可为空，空时视为命中）。</param>
    /// <param name="mode">匹配模式。</param>
    /// <param name="useCompletedDate">日期模式使用完成日期（true）还是截止日期（false）。</param>
    public static bool Matches(GoalEntry entry, string keyword, SearchMode mode, bool useCompletedDate)
    {
        var kw = (keyword ?? string.Empty).Trim().ToLowerInvariant();
        if (kw.Length == 0) return true;

        return mode switch
        {
            SearchMode.Text => Contains(entry.Title, kw) ||
                               Contains(entry.Brief, kw) ||
                               Contains(entry.Detail, kw),

            SearchMode.Tag => entry.Type.Any(t => Contains(t, kw)),

            // 设置模式是全局搜索（项目设置 / 主题 / 数据统计等），不按条目字段过滤
            SearchMode.Setting => false,

            SearchMode.File => entry.RelatedFiles.Any(f =>
                Contains(f.Path, kw) || Contains(f.Function, kw)),

            SearchMode.Date => Contains((useCompletedDate ? entry.CompletedAt : entry.Deadline)
                .ToString("yyyy-MM-dd"), kw),

            // 插件 / 已安装插件 / 功能是全局搜索，不按条目字段过滤
            SearchMode.Plugins => false,
            SearchMode.Expand => false,
            SearchMode.Function => false,

            _ => true,
        };
    }

    /// <summary>
    /// 从原始输入解析出「模式 + 关键词」。支持前缀语法，例如：
    /// "Text:崩溃"、"Tag:bug"、"Setting:主题"、"Function:新建"、"File:parser.c"、"Date:2024-01-01"、
    /// "Plugins:备份"、"Expand:已安装插件"。
    /// 无前缀（或前缀无法识别）时，整段按「文字」模式匹配。
    /// </summary>
    public static (SearchMode Mode, string Keyword) Parse(string raw)
    {
        var text = raw ?? string.Empty;
        var colon = text.IndexOf(':');
        if (colon > 0)
        {
            var prefix = text[..colon].Trim();
            var mode = prefix.ToLowerInvariant() switch
            {
                "text" or "文字"                        => SearchMode.Text,
                "tag" or "标签"                         => SearchMode.Tag,
                "setting" or "设置" or "settings" or "配置"     => SearchMode.Setting,
                "function" or "func" or "功能" or "操作"        => SearchMode.Function,
                "file" or "文件"                        => SearchMode.File,
                "date" or "日期"                        => SearchMode.Date,
                "plugins" or "plugin" or "插件"                => SearchMode.Plugins,
                "expand" or "扩展" or "已安装"                  => SearchMode.Expand,
                _                                       => (SearchMode?)null,
            };

            if (mode is not null)
                return (mode.Value, text[(colon + 1)..]);
        }

        return (SearchMode.Text, text);
    }

    private static bool Contains(string? value, string keyword)
        => !string.IsNullOrEmpty(value) && value.ToLowerInvariant().Contains(keyword);
}
