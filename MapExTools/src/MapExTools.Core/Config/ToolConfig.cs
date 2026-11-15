using System.Text.Json;
using System.Text.Json.Serialization;
using MapExTools.Core.Launch;

namespace MapExTools.Core.Config;

/// <summary>界面主题偏好。</summary>
public enum ThemePreference
{
    /// <summary>跟随系统的浅色／深色设置。</summary>
    System,
    Light,
    Dark,
}

/// <summary>
/// 工具配置，存于 <c>{UserData}/ModsData/MapExt2/MapExTools.json</c>。
/// 新版本加的字段旧版本不认识时，经 <see cref="Extra"/> 原样保留，降级使用不会丢数据。
/// </summary>
public sealed class ToolConfig
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>界面语言 <c>en-US</c>／<c>zh-HANS</c>／<c>zh-HANT</c>；null 表示跟随系统。</summary>
    public string? Language { get; set; }

    public ThemePreference Theme { get; set; } = ThemePreference.System;

    /// <summary>手动指定的 <c>Cities2.exe</c> 路径；null 表示自动定位。</summary>
    public string? GameExePath { get; set; }

    /// <summary>启动方式与参数（计划 2.2）。</summary>
    public LaunchOptions Launch { get; set; } = new();

    /// <summary>启动页"启动方式、游戏路径与参数"折叠栏是否展开；默认收起以把首屏留给 MapExt 启动设定。</summary>
    public bool LaunchOptionsExpanded { get; set; }

    /// <summary>本版本不认识的字段。</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}
