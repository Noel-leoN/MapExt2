// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

// --- LOCALE ---

using Colossal;
using System.Collections.Generic;

namespace MapExtPDX
{
    public class LocaleHANS : IDictionarySource
    {
        private readonly ModSettings m_Setting;

        public LocaleHANS(ModSettings setting)
        {
            m_Setting = setting;
        }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors,
            Dictionary<string, int> indexCounts)
        {
            var entries = new Dictionary<string, string>
            {
                { m_Setting.GetSettingsLocaleID(), "#大地图" }, // Main mod title
                { m_Setting.GetOptionTabLocaleID(ModSettings.kMapSizeModeTab), "地图尺寸" },
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kMainModeGroup), "地图尺寸模式" },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.PatchModeChoice)), "► 选择地图尺寸模式" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.PatchModeChoice)),
                    "⚠️ 改变模式后必须点击「应用设置」按钮生效！\n\n模式详情:\n - ModeA: 57km (4x4) DEM:14m\n - ModeB: 28km (2x2) DEM:7m\n - ModeC: 114km (8x8) DEM:28m\n - 纯净模式: 14km 原版(1x1) DEM:3.5m\n\n注意:\n1. 随着地图尺寸的增加，DEM地形分辨率会相应降低，导致部分山地、水岸与坡道显得粗糙。如果对地形平滑度要求较高，建议使用较为平坦的地图或使用模组工具进行修饰。\n2. 由于游戏底层的浮点精度限制，在地图边缘区域可能会出现模拟数据计算偏差（产生虚假的视觉效果），使用 114km 模式时尤为明显。建议将城市活动中心（住/商/工）尽量建设在地图中心区域。\n\n⚠️ 【重要警告】：在更改地图尺寸模式后，【必须重启游戏】才能安全加载存档，否则可能导致坏档！"
                },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ApplyPatchChanges)), "► 应用设置" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ApplyPatchChanges)),
                    "点击以应用所选的地图尺寸模式。\n\n⚠️ 【重要】：本 Mod 核心逻辑不支持热切换。在应用新设置后，【必须重启游戏】才能安全读取存档，否则系统逻辑将会错乱并导致坏档风险！"
                },
                {
                    m_Setting.GetOptionWarningLocaleID(nameof(ModSettings.ApplyPatchChanges)),
                    "正在应用地图尺寸模式，请耐心等待完成。\n\n⚠️ 完成后，请务必【重启游戏】以确保设置完全生效，切勿直接读取存档！"
                },

                //{ m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.WarningInfo)), "警告: 强烈建议在使用本Mod加载游戏存档前，请备份好您的所有游戏存档(推荐Skyve)，以防游戏崩溃或各种奇特问题坏档！大地图制作不易，且行且珍惜。" },

                // Display names for enum values in the dropdown (if not using GetPatchModeDisplayName directly)
                // This uses the default enum value localization mechanism.

                //{ m_Setting.GetEnumValueLocaleID(PatchModeSetting.ModeA), "Mode 57km (4x4) DEM:14m" },
                //{ m_Setting.GetEnumValueLocaleID(PatchModeSetting.ModeB), "Mode 28km (2x2) DEM:7m" },
                //{ m_Setting.GetEnumValueLocaleID(PatchModeSetting.ModeC), "Mode 114km (8x8) DEM:28m" },
                //{ m_Setting.GetEnumValueLocaleID(PatchModeSetting.ModeD), "Mode 229km (16x16) DEM:56m" },
                //{ m_Setting.GetEnumValueLocaleID(PatchModeSetting.None), "None (No Patches)" },

                // Add back your original localization entries for other settings
                // { m_Setting.GetOptionGroupLocaleID(Setting.kButtonGroup), "Buttons" },
                // ...
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kInfoGroup), "▍地图尺寸信息" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.kInfoGroup)), "▍地图尺寸信息" },

                { m_Setting.GetOptionGroupLocaleID(ModSettings.kEcoGroup), "▍经济系统修复" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.kEcoGroup)), "• 经济逻辑和性能优化" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.isEnableEconomyFix)), "• 经济逻辑修复 & 性能优化" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.isEnableEconomyFix)),
                    "优化并修复以下系统，以适配百万人口规模的巨型城市：\n - 住宅/商业/工业需求系统\n - 家庭找房系统\n - 家庭行为系统 (消费行为修正)\n - 市民寻找工作系统\n - 租金计算系统\n\n⚠️ 【重要】：更改此项设置后，【必须重启游戏】，否则不会生效并且会引发不可预知的 Bug！"
                },

                { m_Setting.GetOptionGroupLocaleID(ModSettings.kNoteGroup), "▍警告" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.kNoteGroup)), "▍警告" },
                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ModeChangeWarningMessage)),
                    "⚠️ 更改上述【任一】选项后，请务必【重启游戏】再读取存档！"
                },

                //{ m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.LoadedSaveCoreValue)), "Loaded Save's MapSize" },
                //{ m_Setting.GetOptionDescLocaleID(nameof(ModSettings.LoadedSaveCoreValue)), "Loaded Save's Map Size" },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ModSettingCoreValue)), "• 当前已应用地图尺寸" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ModSettingCoreValue)),
                    "当前已选择并成功应用的地图尺寸。该尺寸指地图边长。单位为米。\r\n  ⚠️ 注意: 虽然本mod具有存档验证以防错误加载不同尺寸地图存档，但仍然强烈建议在使用本Mod加载游戏存档前，请备份好您的所有游戏存档(推荐Skyve)，以防游戏崩溃或各种奇特问题坏档！大地图制作不易，且行且珍惜。"
                },

                // { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.IsModSettingCoreValueMatch)), "Loaded Save MapSize Match with ModSetting" },
                //{ m_Setting.GetOptionDescLocaleID(nameof(ModSettings.IsModSettingCoreValueMatch)), "Loaded Save MapSize Match with ModSetting" },

                { m_Setting.GetOptionTabLocaleID(ModSettings.kPerformanceToolTab), "▍性能小工具" },
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kPerformanceToolGroup), "▍性能小工具" },
                {
                    m_Setting.GetOptionLabelLocaleID(ModSettings.kPerformanceToolTab),
                    "※ 一些性能工具，可能稍微降低一点CPU/显卡压力. (需要运行一段时间生效)"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.NoThroughTraffic)), "× 禁止过境交通" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.NoThroughTraffic)),
                    "禁止所有过境交通工具出现，降低寻路计算量和交通拥堵. (可能需要运行一段时间生效)"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.NoDogs)), "× 不遛狗" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.NoDogs)),
                    "让宠物都待在家里或去外地旅游，街上不再出现宠物，降低计算量。 (在已建城市中需要等待宠物们回家或去往外地)"
                },

                { m_Setting.GetOptionTabLocaleID(ModSettings.kMiscTab), "▍特色工具" },
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kMiscTab), "▍特色工具" },
                { m_Setting.GetOptionLabelLocaleID(ModSettings.kMiscTab), "• 特色工具" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.LandValueRemake)), "• 现实地价重制版 (当前尚不可用)" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.LandValueRemake)),
                    "重新制作地价系统，恢复到较早版本的深度模拟，修复原始地价数值过高错误，并参考现实经济模型，实现住工商/高中低密度/人口/地段/学区/景观/财富差异化地价因子。"
                },

                { m_Setting.GetOptionTabLocaleID(ModSettings.kDebugTab), "▍开发者选项" },
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kDebugGroup), "▍开发者选项" },
                { m_Setting.GetOptionLabelLocaleID(ModSettings.kDebugTab), "▍开发者选项" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.DisableLoadGameValidation)), "× 禁止游戏读取存档验证" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.DisableLoadGameValidation)),
                    "⚠️ 警告！默认(不勾选)为启用游戏读取存档验证，以防止错误设置地图尺寸模式而读取不同尺寸的存档造成坏档！\r\n  该选项勾选后将取消验证，仅用于使用旧版MapExt mod特殊尺寸模式而无法正确识别的情况。使用旧版存档请务必确认'地图尺寸模式'是否设置正确，否则可能坏档！ \r\n 务必在使用该功能前备份您的存档"
                },

                //{ m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ApplyAirwayRegenerate)), "应用飞行航道重建" },
                //{ m_Setting.GetOptionGroupLocaleID(ModSettings.kAirwayGroup), "飞行航道重建工具" },
                //{ m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ApplyAirwayRegenerate)), "可以在添加飞行航道外部连接点后，点击此处使之生效." },
                //{ m_Setting.GetOptionWarningLocaleID(nameof(ModSettings.ApplyAirwayRegenerate)), "所有飞行航道将立即重建。" },
            };
            return entries;
        }

        public void Unload()
        {
        }
    }
}