using System.Globalization;
using MapExTools.Core.Coc;
using MapExtPDX;

namespace MapExTools.Core.Packages;

/// <summary>存档 <c>modsEnabled</c> 里 MapExt 标签的情况，对应 <c>SaveEntryGate.ValidateSave</c> 的四个分支。</summary>
public enum SaveTagKind
{
    /// <summary>没有以 <c>MapExt</c> 开头的条目：原版存档，判定值 −1。</summary>
    Vanilla,

    /// <summary>用过 MapExt 但没有模式值标签：旧版存档，按 CV=4 处理。</summary>
    Legacy,

    /// <summary>带 <c>MapEXT_CoreValue=</c> 标签，取标签后的整数。</summary>
    Tagged,

    /// <summary>有标签但解析不出整数：判定值 0，一律不符。</summary>
    Unparsable,
}

/// <summary>存档的模式判定结果。</summary>
/// <param name="CoreValue">判定值：原版为 −1，旧版为 4，标签解析失败为 0，其余为标签里的整数。</param>
public sealed record SaveModeTag(SaveTagKind Kind, int CoreValue);

/// <summary>按地图面积推断的制作模式。</summary>
/// <param name="Ratio">面积与原版分块面积之比，约等于 CV²；没有面积时为 NaN。</param>
/// <param name="CoreValue">比值开方后取整；不可信时也照填，供界面显示。</param>
/// <param name="Trusted">比值接近某个现行模式 CV 的平方（5% 容差）。</param>
public sealed record MapModeInference(float? Area, double Ratio, int CoreValue, bool Trusted);

/// <summary>存档或地图与设定模式的关系，按游戏里 MapExt 闸门的实际处理归类。</summary>
public enum PackageRelation
{
    /// <summary>模式一致，直接载入。</summary>
    Match,

    /// <summary>载入时弹出转换确认。</summary>
    Convertible,

    /// <summary>开启"地图扩展转换"后可以转换；现在载入会被拦下。</summary>
    ConvertibleWhenEnabled,

    /// <summary>原版地图在扩展模式下开新城：地形会被拉伸，游戏先弹警告。</summary>
    Stretched,

    /// <summary>模式不符，载入会被拦下。</summary>
    Mismatch,

    /// <summary>模式不符，但开了"禁止读档校验"，游戏不拦、直接载入。</summary>
    Unchecked,

    /// <summary>地图面积推断不可信，载入后按地形实际尺寸判定；或设定模式读不出来。</summary>
    Unknown,
}

/// <summary>判定所需的 MapExt 设定：都取自 <c>MapExtPDX.coc</c>，游戏下次启动时生效。</summary>
/// <param name="CurrentCoreValue">设定模式的 CoreValue。</param>
/// <param name="ConversionEnabled">"地图扩展转换"是否开启（<see cref="SaveGateRules.ConversionKey"/>）。</param>
/// <param name="ValidationDisabled">"禁止读档校验"是否开启。</param>
public sealed record GateSettings(string ModeName, int CurrentCoreValue, bool ConversionEnabled, bool ValidationDisabled)
{
    /// <summary>从 .coc 的读取结果取出判定所需的设定；模式值游戏读不懂时按 ModeA，与游戏一致。</summary>
    public static GateSettings FromCoc(StartupSettingsState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        string mode = (string)state[StartupKeys.ModeKey].Value;
        bool validationDisabled = state[SaveGateRules.ValidationKey].Value is true;
        return new GateSettings(mode, StartupKeys.GetCoreValue(mode), SaveGateRules.ReadConversionEnabled(state), validationDisabled);
    }
}

/// <summary>
/// 存档、地图与设定模式的关系判定，照 MapExt 的 <c>SaveEntryGate</c>（v4.12.0）一一对应：
/// <c>ValidateSave</c>（存档标签）、<c>InferMapCoreValue</c>（地图面积）、<c>EvaluateSave</c>／<c>EvaluateNewGame</c>（是否转换、拦下或拉伸）。
/// 本会话降级、转换补丁有没有挂上只能在游戏里知道，这里按正常情况判断。
///
/// <para><b>同步</b>：Mod 端改了这几处的规则，这里要一起改。判定用到的常量取自共用的 <see cref="StartupKeys"/>；
/// 转换开关键名与可转换的模式值不在其中，定义在本类，出处见各成员的注释。</para>
/// </summary>
public static class SaveGateRules
{
    /// <summary>
    /// "地图扩展转换"的设定键，即 <c>ModSettings.EnableVanillaConversion</c>，默认 false。
    /// 不是 9 个受管键之一，只读不写。
    /// </summary>
    public const string ConversionKey = "EnableVanillaConversion";

    /// <summary>"禁止读档校验"的设定键，是 9 个受管键之一（<see cref="StartupKeys.Keys"/>）。</summary>
    public const string ValidationKey = "DisableLoadGameValidation";

    /// <summary>None 模式（原版 14 km）的 CoreValue。</summary>
    public const int NoneCoreValue = 1;

    /// <summary>28→57 扩展的源模式与目标模式（<c>SaveExpansionTypeMapPatch.SourceCoreValue</c>／<c>TargetCoreValue</c>）。</summary>
    public const int ExpansionSourceCoreValue = 2;

    /// <inheritdoc cref="ExpansionSourceCoreValue"/>
    public const int ExpansionTargetCoreValue = 4;

    /// <summary>转换只支持转到 57 km（CV=4）与 28 km（CV=2）（<c>SaveEntryGate.CanConvertTo</c>）。</summary>
    public static bool CanConvertTo(int coreValue) => coreValue is 4 or 2;

    /// <summary>.coc 里 <see cref="ConversionKey"/> 的值：只认 <c>true</c>，其余（含缺失、读不懂）按默认 false。</summary>
    public static bool ReadConversionEnabled(StartupSettingsState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.Entries.FirstOrDefault(e => e.Key == ConversionKey)?.Raw == "true";
    }

    /// <summary>
    /// 按存档的 <c>modsEnabled</c> 判定模式值（<c>SaveEntryGate.ValidateSave</c>）：
    /// 没有以 <c>MapExt</c> 开头的条目（不分大小写）为原版；有但没有 <c>MapEXT_CoreValue=</c> 条目（区分大小写）为旧版；
    /// 取第一个标签后面的整数，解析不出为 0。
    /// </summary>
    public static SaveModeTag ClassifySave(IReadOnlyList<string?>? modsEnabled)
    {
        if (modsEnabled == null || !modsEnabled.Any(m => m != null && m.StartsWith(StartupKeys.ModIdentifierPrefix, StringComparison.OrdinalIgnoreCase)))
            return new SaveModeTag(SaveTagKind.Vanilla, StartupKeys.UntaggedVanillaCoreValue);

        string? tag = modsEnabled.FirstOrDefault(m => m != null && m.StartsWith(StartupKeys.CoreValueTagPrefix, StringComparison.Ordinal));
        if (tag == null)
            return new SaveModeTag(SaveTagKind.Legacy, StartupKeys.LegacySaveCoreValue);

        // Mod 用 int.TryParse(string)：允许首尾空白与正负号
        return int.TryParse(tag.AsSpan(StartupKeys.CoreValueTagPrefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out int coreValue)
            ? new SaveModeTag(SaveTagKind.Tagged, coreValue)
            : new SaveModeTag(SaveTagKind.Unparsable, StartupKeys.UnparsableCoreValue);
    }

    /// <summary>
    /// 按 <c>MapInfo.area</c> 推断地图的制作模式（<c>SaveEntryGate.InferMapCoreValue</c>）：
    /// 面积 ÷ 14336² ≈ CV²，相对容差 5%，且 CV 须为现行模式之一。面积反映的是分块而不是地形，分块与地形不一致的旧图可能误判。
    /// </summary>
    public static MapModeInference InferMap(float? area)
    {
        if (area is not float value)
            return new MapModeInference(null, double.NaN, 0, false);

        double ratio = value / StartupKeys.VanillaTileArea;
        if (!double.IsFinite(ratio))
            return new MapModeInference(area, ratio, 0, false);

        int coreValue = (int)Math.Round(Math.Sqrt(Math.Max(ratio, 0d)));
        double square = (double)coreValue * coreValue;
        bool trusted = coreValue >= 1
                       && Math.Abs(ratio - square) <= StartupKeys.MapAreaRatioTolerance * square
                       && StartupKeys.IsKnownCoreValue(coreValue);
        return new MapModeInference(area, ratio, coreValue, trusted);
    }

    /// <summary>
    /// 读存档时闸门的处理（<c>SaveEntryGate.EvaluateSave</c>）。"禁止读档校验"开启时整段跳过：不拦，也不提供转换。
    /// <list type="bullet">
    /// <item>原版存档：None 模式直接读；57／28 km 下可转换；</item>
    /// <item>CV=1 存档：57／28 km 下可转换；CV=2 存档：57 km 下可走 28→57 扩展；</item>
    /// <item>旧版存档只在 57 km 下直接读；其余不符。</item>
    /// </list>
    /// </summary>
    public static PackageRelation EvaluateSave(SaveModeTag tag, GateSettings settings)
    {
        ArgumentNullException.ThrowIfNull(tag);
        ArgumentNullException.ThrowIfNull(settings);

        int current = settings.CurrentCoreValue;
        bool match = tag.Kind == SaveTagKind.Vanilla ? current == NoneCoreValue : tag.CoreValue == current;
        if (match)
            return PackageRelation.Match;
        if (settings.ValidationDisabled)
            return PackageRelation.Unchecked;

        bool convertible = tag.Kind switch
        {
            SaveTagKind.Vanilla => CanConvertTo(current),
            SaveTagKind.Tagged => (tag.CoreValue == NoneCoreValue && CanConvertTo(current))
                                  || (tag.CoreValue == ExpansionSourceCoreValue && current == ExpansionTargetCoreValue),
            _ => false,
        };

        if (!convertible)
            return PackageRelation.Mismatch;
        return settings.ConversionEnabled ? PackageRelation.Convertible : PackageRelation.ConvertibleWhenEnabled;
    }

    /// <summary>
    /// 用地图开新城时闸门的处理（<c>SaveEntryGate.EvaluateNewGame</c>）。面积不可信时放行、载入后按地形判定；
    /// 原版地图在扩展模式下可转换时弹转换确认，否则弹拉伸警告，都不受"禁止读档校验"影响；
    /// 其余模式不符的地图被拦下，开了"禁止读档校验"时放行。
    /// </summary>
    public static PackageRelation EvaluateMap(MapModeInference inference, GateSettings settings)
    {
        ArgumentNullException.ThrowIfNull(inference);
        ArgumentNullException.ThrowIfNull(settings);

        int current = settings.CurrentCoreValue;
        if (!inference.Trusted)
            return PackageRelation.Unknown;
        if (inference.CoreValue == current)
            return PackageRelation.Match;

        if (inference.CoreValue == NoneCoreValue)
        {
            return CanConvertTo(current) && settings.ConversionEnabled
                ? PackageRelation.Convertible
                : PackageRelation.Stretched;
        }

        return settings.ValidationDisabled ? PackageRelation.Unchecked : PackageRelation.Mismatch;
    }

    /// <summary>
    /// 一个包与设定模式的关系：读不出来的包为 null；设定模式未知（<paramref name="settings"/> 为 null）时为 <see cref="PackageRelation.Unknown"/>。
    /// </summary>
    public static PackageRelation? Evaluate(CokPackageInfo package, GateSettings? settings)
    {
        ArgumentNullException.ThrowIfNull(package);

        if (package.Problem != PackageProblem.None)
            return null;
        if (settings == null)
            return PackageRelation.Unknown;
        if (package.Save != null)
            return EvaluateSave(package.Save.Mode, settings);
        return package.Map != null ? EvaluateMap(package.Map.Mode, settings) : null;
    }

    /// <summary>一个包"切到该模式"的目标模式；读不出来或没有对应的现行模式时为 null。</summary>
    public static StartupModeInfo? GetTargetMode(CokPackageInfo package)
    {
        ArgumentNullException.ThrowIfNull(package);

        if (package.Problem != PackageProblem.None)
            return null;
        if (package.Save != null)
            return GetTargetMode(package.Save.Mode);
        return package.Map != null ? GetTargetMode(package.Map.Mode) : null;
    }

    /// <summary>"切到该模式"的目标：原版存档为 None，旧版为 ModeA，带标签的取对应模式；不是现行模式时为 null。</summary>
    public static StartupModeInfo? GetTargetMode(SaveModeTag tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        return tag.Kind switch
        {
            SaveTagKind.Vanilla => StartupKeys.FindModeByCoreValue(NoneCoreValue),
            SaveTagKind.Legacy or SaveTagKind.Tagged => StartupKeys.FindModeByCoreValue(tag.CoreValue),
            _ => null,
        };
    }

    /// <summary>地图"切到该模式"的目标：面积推断可信时为推断出的模式，否则为 null。</summary>
    public static StartupModeInfo? GetTargetMode(MapModeInference inference)
    {
        ArgumentNullException.ThrowIfNull(inference);
        return inference.Trusted ? StartupKeys.FindModeByCoreValue(inference.CoreValue) : null;
    }
}
