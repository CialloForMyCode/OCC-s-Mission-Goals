using System;

namespace OCCMissionGoals.Models;

/// <summary>
/// 把完成时间换算成「几分钟前 / 几小时前 / 几天前」这类相对时间文案。
/// 文案取自 Languages/*.xaml 里预置的 <c>{0}分钟前</c> 等键，语言包缺翻译时回退为键本身（即中文原文）。
/// 阶梯：1 分钟内 → 分钟 → 小时 → 天 → 星期 → 月 → 年，逐级向下取整。
/// </summary>
public static class RelativeTime
{
    /// <summary>以指定时间点为「现在」，返回相对时间文案。</summary>
    public static string Format(DateTime completedAt, DateTime now)
    {
        var span = now - completedAt;
        if (span < TimeSpan.Zero)
            span = TimeSpan.Zero;

        // 刚完成的（不足 1 分钟）
        if (span.TotalMinutes < 1)
            return LocalizationManager.T("刚刚");

        if (span.TotalHours < 1)
            return LocalizationManager.T("{0}分钟前", (int)span.TotalMinutes);

        if (span.TotalDays < 1)
            return LocalizationManager.T("{0}小时前", (int)span.TotalHours);

        if (span.TotalDays < 7)
            return LocalizationManager.T("{0}天前", (int)span.TotalDays);

        // 满 7 天进「星期」档，满 4 周进「月」档
        if (span.TotalDays < 28)
            return LocalizationManager.T("{0}个星期前", (int)(span.TotalDays / 7));

        if (span.TotalDays < 365)
            return LocalizationManager.T("{0}个月前", Math.Max(1, (int)(span.TotalDays / 30)));

        return LocalizationManager.T("{0}年前", Math.Max(1, (int)(span.TotalDays / 365)));
    }

    /// <summary>以当前时间为「现在」，返回相对时间文案。</summary>
    public static string Format(DateTime completedAt) => Format(completedAt, DateTime.Now);
}
