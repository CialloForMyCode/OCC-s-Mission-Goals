using System.Windows.Media;

namespace OCCMissionGoals.Models;

/// <summary>
/// 颜色字符串（hex / 颜色名）与画刷之间的转换工具。
/// </summary>
public static class ColorUtil
{
    /// <summary>把 "#RRGGBB"、颜色名等解析为画刷；空或非法时返回 null。</summary>
    public static Brush? ParseBrush(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        try
        {
            var color = (Color)ColorConverter.ConvertFromString(value.Trim());
            return new SolidColorBrush(color);
        }
        catch
        {
            return null;
        }
    }
}
