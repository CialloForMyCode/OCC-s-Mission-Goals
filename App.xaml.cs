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

        // 读取主题
        var theme = ConfigManager.Get("General", "theme", "light");
        ThemeManager.ApplyTheme(theme == "dark");

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
        return arg is "project" or "version" or "entry" or "help" or "--help" or "-h";
    }
}
