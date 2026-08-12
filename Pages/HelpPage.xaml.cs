using System.Diagnostics;
using System.Windows.Controls;

namespace OCCMissionGoals.Pages;

public partial class HelpPage : Page
{
    private const string RepoUrl = "https://github.com/OCCOCCO/ONC";

    public HelpPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        var ver = Services.ProjectService.CurrentProject?.CurrentVersion;
        VersionLabel.Text = string.IsNullOrEmpty(ver)
            ? "版本 0.1.0-alpha.0"
            : $"版本 {ver}";
    }

    private void GitHubRepo_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        OpenUrl(RepoUrl);
    }

    private void GitHubIssue_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        OpenUrl(RepoUrl + "/issues/new");
    }

    private void License_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        OpenUrl(RepoUrl + "/blob/master/LICENSE");
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch
        {
            // 忽略打开失败
        }
    }
}
