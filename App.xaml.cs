using System.Runtime.InteropServices;
using System.Windows;
using OCCMissionGoals.Cli;

namespace OCCMissionGoals;

public partial class App : Application
{
    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int dwProcessId);

    private const int ATTACH_PARENT_PROCESS = -1;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 读取语言
        LocalizationManager.LoadFromConfig();

        // 读取主题
        var theme = ConfigManager.Get("General", "theme", "light");
        ThemeManager.ApplyTheme(theme == "dark");

        // 读取主题色
        var accent = ConfigManager.Get("General", "accent", "#4CAF50");
        ThemeManager.ApplyAccentColor(accent);

        // CLI 模式：如果第一个参数是已知命令，走 CLI 然后退出
        if (e.Args.Length > 0 && IsCliCommand(e.Args[0]))
        {
            AttachConsole(ATTACH_PARENT_PROCESS);
            var exitCode = CliCommand.Run(e.Args);
            Environment.Exit(exitCode);
        }

        // GUI 模式
        new MainWindow().Show();
    }

    private static bool IsCliCommand(string arg)
    {
        // 识别所有 CLI 命令 / 短标志 / 选项，确保 -a、-l、-p 等也能进入 CLI 模式，
        // 而不是被当作 GUI 启动参数忽略后打开主窗口。
        if (arg is "project" or "version" or "entry" or "help" or "--help" or "-h")
            return true;
        if (arg is "-a" or "--add" or "-D" or "--delete" or "-c" or "--check"
            or "-d" or "--done" or "-u" or "--undone" or "-f" or "--favorited"
            or "-l" or "--list" or "-v" or "--version" or "-p" or "--project")
            return true;
        if (arg.StartsWith("--project=", StringComparison.Ordinal)
            || arg.StartsWith("--version=", StringComparison.Ordinal))
            return true;
        return false;
    }
}
