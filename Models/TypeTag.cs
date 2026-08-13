using System.Windows.Media;

namespace OCCMissionGoals.Models;

/// <summary>
/// 类别标签的显示模型：标签文本 + 可选颜色。
/// </summary>
public class TypeTag
{
    public string Text { get; }

    /// <summary>标签颜色画刷；未设置颜色时为 null。</summary>
    public Brush? Background { get; }

    public bool HasColor => Background != null;

    public TypeTag(string text, string? colorHex)
    {
        Text = text;
        Background = ColorUtil.ParseBrush(colorHex);
    }
}
