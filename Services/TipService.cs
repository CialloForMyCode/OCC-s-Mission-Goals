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


    private static string Fill(string tip, GoalEntry entry)
        => tip.Replace("{file name}", entry.Title)
              .Replace("{Program Name}", entry.Title);

    public static string GetCompleteTip(GoalEntry entry)
    {
        if (_complete.TryGetValue(entry.Severity, out var tips) && tips.Length > 0)
            return Fill(Pick(tips), entry);
        return string.Empty;
    }

    public static string GetUndoCompleteTip(GoalEntry entry)
    {
        if (_undo.TryGetValue(entry.Severity, out var tips) && tips.Length > 0)
            return Fill(Pick(tips), entry);
        return string.Empty;
    }

    private static T Pick<T>(T[] arr) => arr[_rng.Next(arr.Length)];
}
