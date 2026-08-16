namespace OCCMissionGoals.Models;

public class RepositoryInfo
{
    public string Name { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    /// <summary>推送目标分支（默认 main）。</summary>
    public string Branch { get; set; } = "main";
}
