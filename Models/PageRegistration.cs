using System;
using System.Windows.Controls;

namespace OCCMissionGoals.Models;

/// <summary>
/// 页面注册项。通过 MainWindow.RegisterPage() 注册后，自动在 SwitchPage 生成页签按钮。
/// </summary>
public class PageRegistration
{
    /// <summary>唯一标识，如 "log"、"undone"、"expand"、"help"。</summary>
    public string Key { get; init; } = "";

    /// <summary>页签按钮上显示的文字。</summary>
    public string TabLabel { get; init; } = "";

    /// <summary>页面工厂，每次导航时按需创建（首次后会被缓存）。</summary>
    public Func<Page> PageFactory { get; init; } = () => new Page();

    /// <summary>页面创建后的初始化动作（仅首次创建时执行一次）。</summary>
    public Action<Page>? OnInit { get; init; }

    /// <summary>导航到此页面之前执行的动作（每次切换都执行）。</summary>
    public Action<Page>? OnBeforeNavigate { get; init; }

    /// <summary>全局刷新时执行的动作（例如切换项目/版本后重载数据）。</summary>
    public Action<Page>? OnRefresh { get; init; }

    /// <summary>是否帮助页签（会触发显示/隐藏帮助按钮的特殊行为）。</summary>
    public bool IsHelpTab { get; init; }
}
