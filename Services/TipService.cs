using System;
using System.Collections.Generic;
using System.Text;
using OCCMissionGoals.Models;

namespace OCCMissionGoals.Services;

public static class TipService
{
    // ── 完成提示（按严重程度） ──
    private static readonly Dictionary<GoalSeverity, string[]> _complete = new()
    {
        [GoalSeverity.Update] = new[]
        {
            "OMG, your {file name} has new stuff again, right?",
            "OMG, is your intelligence starting to show?",
            "OMG, you actually updated it!"
        },
        [GoalSeverity.Patch] = new[]
        {
            "OMG, a new patch for {Program Name}!",
            "OMG, this patch is awesome.",
            "OMG, is this a bug?"
        },
        [GoalSeverity.General] = new[]
        {
            "OMG, you completed an entry.",
            "OMG, your {file name} is more complete now.",
            "OMG, this is so hard!"
        },
        [GoalSeverity.Severe] = new[]
        {
            "OMG, you just fixed a serious bug!",
            "OMG, did you just finish a serious bug?",
            "OMG, your {file name} is no longer bothered by this bug!"
        },
        [GoalSeverity.Fatal] = new[]
        {
            "OMG, you actually completed a critical bug!",
            "OMG, your {file name} just got rid of a big headache.",
            "OMG, wanting to compete with the heavens!"
        }
    };

    // ── 尾缀（时间 × 严重组） ──
    private static readonly Dictionary<(int time, int grp), string[]> _suffix = new()
    {
        // 一天内
        [(0, 0)] = new[] { "so fast!" },
        [(0, 1)] = new[] { "You're in a hurry, right?" },
        [(0, 2)] = new[] { "You are simply a genius!" },

        // 一周内
        [(1, 0)] = new[] { "This update got buried by bugs..." },
        [(1, 1)] = new[] { "It looks like it doesn't matter, right?" },
        [(1, 2)] = new[] { "Is it still too hard?" },

        // 一个月及以后
        [(2, 0)] = new[] { "Such a huge size." },
        [(2, 1)] = new[] { "You better just forget about it, huh?" },
        [(2, 2)] = new[] { "This question is really tough, isn't it?" }
    };

    // ── 撤销完成提示（按严重程度） ──
    private static readonly Dictionary<GoalSeverity, string[]> _undo = new()
    {
        [GoalSeverity.Update] = new[]
        {
            "OMG, you're not ready to release the new version of {file name}, right?",
            "OMG, did you click the wrong thing?",
            "OMG, this must be a work issue."
        },
        [GoalSeverity.Patch] = new[]
        {
            "OMG, does this patch have no effect?",
            "OMG, you didn't submit your patch, right?",
            "OMG, this patch is a bad patch! A bad patch!"
        },
        [GoalSeverity.General] = new[]
        {
            "OMG, this bug hasn't been fixed?",
            "OMG, this isn't a simple bug, right?",
            "OMG, ah, this isn't a good thing, right?"
        },
        [GoalSeverity.Severe] = new[]
        {
            "OMG, honorable BUG, try again.",
            "OMG, haste makes waste, slow down."
        },
        [GoalSeverity.Fatal] = new[]
        {
            "OMG, this is a huge project, isn't it?",
            "OMG, this is really tough, isn't it?",
            "OMG, this won't be too hard for you, right?"
        }
    };

    private static readonly Random _rng = new();

    private static int SeverityGroup(GoalSeverity s) => s switch
    {
        GoalSeverity.Update => 0,
        GoalSeverity.Patch => 1,
        GoalSeverity.General => 1,
        GoalSeverity.Severe => 2,
        GoalSeverity.Fatal => 2,
        _ => 1
    };

    private static int TimeCategory(DateTime deadline)
    {
        var days = (DateTime.Now - deadline).TotalDays;
        if (days <= 1) return 0;
        if (days <= 7) return 1;
        return 2;
    }

    private static string Fill(string tip, GoalEntry entry)
        => tip.Replace("{file name}", entry.Title)
              .Replace("{Program Name}", entry.Title);

    public static string GetCompleteTip(GoalEntry entry)
    {
        var sb = new StringBuilder();

        if (_complete.TryGetValue(entry.Severity, out var tips) && tips.Length > 0)
            sb.Append(Fill(Pick(tips), entry));

        var tc = TimeCategory(entry.Deadline);
        var sg = SeverityGroup(entry.Severity);
        if (_suffix.TryGetValue((tc, sg), out var suffixes) && suffixes.Length > 0)
        {
            if (sb.Length > 0) sb.Append(' ');
            sb.Append(Fill(Pick(suffixes), entry));
        }

        return sb.ToString();
    }

    public static string GetUndoCompleteTip(GoalEntry entry)
    {
        if (_undo.TryGetValue(entry.Severity, out var tips) && tips.Length > 0)
            return Fill(Pick(tips), entry);
        return string.Empty;
    }

    private static T Pick<T>(T[] arr) => arr[_rng.Next(arr.Length)];
}
