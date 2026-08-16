using System.Windows.Media;

namespace OCCMissionGoals.Models;

/// <summary>
/// 严重程度等级的统一文字描述和颜色。
/// 五等：致命(红)、严重(橙)、一般(黄)、补丁(蓝)、更新(绿)
/// </summary>
public static class SeverityHelper
{
    public static string GetText(GoalSeverity s) => s switch
    {
        GoalSeverity.Fatal   => LocalizationManager.T("致命", "Fatal", "Критический"),
        GoalSeverity.Severe  => LocalizationManager.T("严重", "Severe", "Серьёзный"),
        GoalSeverity.General => LocalizationManager.T("一般", "General", "Обычный"),
        GoalSeverity.Patch   => LocalizationManager.T("补丁", "Patch", "Исправление"),
        GoalSeverity.Update  => LocalizationManager.T("更新", "Update", "Обновление"),
        _                    => LocalizationManager.T("未知", "Unknown", "Неизвестно")
    };

    public static Color GetColor(GoalSeverity s) => s switch
    {
        GoalSeverity.Fatal   => Color.FromRgb(0xE8, 0x3D, 0x3D), // 红
        GoalSeverity.Severe  => Color.FromRgb(0xE8, 0x8D, 0x3D), // 橙
        GoalSeverity.General => Color.FromRgb(0xE8, 0xD4, 0x3D), // 黄
        GoalSeverity.Patch   => Color.FromRgb(0x3D, 0x9D, 0xE8), // 蓝
        GoalSeverity.Update  => Color.FromRgb(0x4C, 0xAF, 0x50), // 绿
        _                    => Color.FromRgb(0x8D, 0x8D, 0x8D)  // 灰
    };

    public static Brush GetBrush(GoalSeverity s) => new SolidColorBrush(GetColor(s));
}
