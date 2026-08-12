using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OCCMissionGoals;

/// <summary>
/// 标准 INI 文件读写器。路径为 exe 同目录下的 config.ini。
/// </summary>
public static class ConfigManager
{
    private static readonly string _path =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");

    private static readonly object _lock = new();

    /// <summary>
    /// 读取一个键值，若节/键不存在则返回 <paramref name="defaultValue"/>。
    /// </summary>
    public static string Get(string section, string key, string defaultValue = "")
    {
        var dict = ReadAll();
        return dict.TryGetValue((section, key), out var value) ? value : defaultValue;
    }

    /// <summary>
    /// 写入一个键值。保留原有节、键、注释和空行结构。
    /// </summary>
    public static void Set(string section, string key, string value)
    {
        lock (_lock)
        {
            var all = ReadAll();
            all[(section, key)] = value;
            var lines = File.Exists(_path)
                ? File.ReadAllLines(_path, Encoding.UTF8)
                : Array.Empty<string>();
            File.WriteAllText(_path, Format(all, lines, section, key), Encoding.UTF8);
        }
    }

    private static Dictionary<(string Section, string Key), string> ReadAll()
    {
        lock (_lock)
        {
            if (!File.Exists(_path)) return new();
            return Parse(File.ReadAllLines(_path, Encoding.UTF8));
        }
    }

    private static Dictionary<(string, string), string> Parse(string[] lines)
    {
        var dict = new Dictionary<(string, string), string>();
        var section = "";

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#'))
                continue;

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                section = line[1..^1].Trim();
                continue;
            }

            var eq = line.IndexOf('=');
            if (eq < 0) continue;

            var k = line[..eq].Trim();
            var v = line[(eq + 1)..].Trim();
            dict[(section, k)] = v;
        }

        return dict;
    }

    private static string Format(
        Dictionary<(string Section, string Key), string> all,
        string[] original,
        string writeSection,
        string writeKey)
    {
        var sb = new StringBuilder();
        string currentSection = "";
        bool writerPending = true;  // still need to emit the writeKey

        for (int i = 0; i < original.Length; i++)
        {
            var raw = original[i];
            var t = raw.Trim();

            // Blank / comment → pass through
            if (t.Length == 0 || t.StartsWith(';') || t.StartsWith('#'))
            {
                sb.AppendLine(raw);
                continue;
            }

            // Section header
            if (t.StartsWith('[') && t.EndsWith(']'))
            {
                // Leaving the target section? Flush pending write before the next section
                if (writerPending && currentSection == writeSection)
                {
                    sb.AppendLine($"{writeKey} = {all[(writeSection, writeKey)]}");
                    writerPending = false;
                }

                currentSection = t[1..^1].Trim();
                sb.AppendLine(raw);
                continue;
            }

            // Key = Value
            var eq = t.IndexOf('=');
            if (eq >= 0)
            {
                var k = t[..eq].Trim();
                if (writerPending && currentSection == writeSection &&
                    string.Equals(k, writeKey, StringComparison.OrdinalIgnoreCase))
                {
                    sb.AppendLine($"{writeKey} = {all[(writeSection, writeKey)]}");
                    writerPending = false;
                    continue;
                }
            }

            sb.AppendLine(raw);
        }

        // Still pending at EOF
        if (writerPending)
        {
            if (currentSection == writeSection)
            {
                // Section exists, key is new → append at end of section
                sb.AppendLine($"{writeKey} = {all[(writeSection, writeKey)]}");
            }
            else
            {
                // Section doesn't exist → create section + key
                if (sb.Length > 0) sb.AppendLine();
                sb.AppendLine($"[{writeSection}]");
                sb.AppendLine($"{writeKey} = {all[(writeSection, writeKey)]}");
            }
        }

        return sb.ToString();
    }
}
