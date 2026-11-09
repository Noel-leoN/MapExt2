using System.Windows;
using MapExTools.Core.Config;

namespace MapExTools.Services;

/// <summary>套用 WPF 内置的 Fluent 主题。窗口背景（Win11 Mica、Win10 纯色）交给框架处理。</summary>
internal static class ThemeService
{
    public static void Apply(ThemePreference preference)
    {
        // ThemeMode 标为实验性 API（WPF0001），只在此处局部压掉诊断，见计划 §2 第 2 条
#pragma warning disable WPF0001
        Application.Current.ThemeMode = preference switch
        {
            ThemePreference.Light => ThemeMode.Light,
            ThemePreference.Dark => ThemeMode.Dark,
            _ => ThemeMode.System,
        };
#pragma warning restore WPF0001
    }
}
