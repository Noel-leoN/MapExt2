using System.Text;
using MapExTools.Core.Coc;
using MapExTools.Core.Localization;
using MapExtPDX;

namespace MapExTools.Core.CommandLine;

/// <summary>命令行用法说明（<c>--help</c>）。设定类选项按 <see cref="CommandLineParser.SettingOptions"/> 生成，说明取自设定页的显示名。</summary>
public static class CommandLineText
{
    public static string Usage(string language)
    {
        var rows = new List<(string Syntax, string Text)?>();
        foreach (SettingOption option in CommandLineParser.SettingOptions)
        {
            string values = option.Key.Kind == StartupKeyKind.Mode ? string.Join('|', StartupKeys.ModeNames) : "on|off";
            rows.Add(($"{option.Name} <{values}>", StartupSettingsText.KeyLabel(option.Key, language)));
        }

        rows.Add(("--reset", Get(language, "Cli.Usage.Reset")));
        rows.Add(null);
        rows.Add(("--launch", Get(language, "Cli.Usage.Launch")));
        rows.Add(("--show", Get(language, "Cli.Usage.Show")));
        rows.Add(("--lang <" + string.Join('|', UiLanguage.All) + ">", Get(language, "Cli.Usage.Lang")));
        rows.Add(("--help", Get(language, "Cli.Usage.Help")));
        rows.Add(null);
        rows.Add(("--user-data " + Get(language, "Cli.Usage.Folder"), Get(language, "Cli.Usage.UserData")));
        rows.Add(("--game-process " + Get(language, "Cli.Usage.Name"), Get(language, "Cli.Usage.GameProcess")));

        int width = rows.Max(r => r == null ? 0 : DisplayWidth(r.Value.Syntax)) + 2;
        string[] headings = [Get(language, "Cli.Usage.Settings"), Get(language, "Cli.Usage.Other"), Get(language, "Cli.Usage.Testing")];

        var sb = new StringBuilder();
        sb.AppendLine(Get(language, "Cli.Usage.Header"));
        sb.AppendLine();
        int section = 0;
        sb.AppendLine(headings[section]);
        foreach ((string Syntax, string Text)? row in rows)
        {
            if (row == null)
            {
                sb.AppendLine();
                sb.AppendLine(headings[++section]);
                continue;
            }

            string syntax = row.Value.Syntax;
            sb.Append("  ").Append(syntax).Append(' ', width - DisplayWidth(syntax)).AppendLine(row.Value.Text);
        }

        sb.AppendLine();
        sb.Append(Get(language, "Cli.Usage.Footer"));
        return sb.ToString();
    }

    /// <summary>控制台里的显示宽度：中日韩文字与全角符号占两格，用来对齐 <c>&lt;目录&gt;</c> 这类中文占位。</summary>
    public static int DisplayWidth(string text)
    {
        int width = 0;
        foreach (int code in text)
        {
            // 韩文字母、中日韩部首到彝文、韩文音节、兼容汉字、竖排与兼容标点、全角字符
            bool isWide = code is (>= 0x1100 and <= 0x115F) or (>= 0x2E80 and <= 0xA4CF) or (>= 0xAC00 and <= 0xD7A3)
                or (>= 0xF900 and <= 0xFAFF) or (>= 0xFE30 and <= 0xFE4F) or (>= 0xFF00 and <= 0xFF60)
                or (>= 0xFFE0 and <= 0xFFE6);
            width += isWide ? 2 : 1;
        }

        return width;
    }

    private static string Get(string language, string key) => Strings.Get(language, key);
}
