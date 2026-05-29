using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;
using SimpleBrush.Core;
using Unity.Entities;

namespace SimpleBrush.Settings
{
    /// <summary>
    /// SimpleBrush 设置面板。
    /// 提供 5 个独立的资源恢复按钮（带确认弹窗），以及 4 个"无限资源"持久化开关。
    /// </summary>
    [FileLocation("ModsSettings/" + Mod.ModName + "/" + Mod.ModName)]
    [SettingsUIGroupOrder(kGroupInfinite, kGroupRestore)]
    [SettingsUIShowGroupName(kGroupInfinite, kGroupRestore)]
    public class ModSettings : ModSetting
    {
        // === Section/Group 常量 ===
        public const string kGroupInfinite = "InfiniteResources";
        public const string kGroupRestore = "RestoreResources";

        public ModSettings(IMod mod) : base(mod) { }

        #region 无限资源开关

        // === 无限肥沃土地 ===
        [SettingsUISection(kGroupInfinite)]
        public bool InfiniteFertility { get; set; }

        // === 无限矿石 ===
        [SettingsUISection(kGroupInfinite)]
        public bool InfiniteOre { get; set; }

        // === 无限石油 ===
        [SettingsUISection(kGroupInfinite)]
        public bool InfiniteOil { get; set; }

        // === 无限鱼类 ===
        [SettingsUISection(kGroupInfinite)]
        public bool InfiniteFish { get; set; }

        #endregion

        #region 一键恢复按钮

        // === 恢复肥沃土地 ===
        [SettingsUIButton]
        [SettingsUISection(kGroupRestore)]
        [SettingsUIConfirmation]
        public bool RestoreFertility
        {
            // ReSharper disable once ValueParameterNotUsed
            set => ResourceCleaner.ClearUsed(
                World.DefaultGameObjectInjectionWorld, ResourceCleaner.ResourceType.Fertility);
        }

        // === 恢复矿石 ===
        [SettingsUIButton]
        [SettingsUISection(kGroupRestore)]
        [SettingsUIConfirmation]
        public bool RestoreOre
        {
            // ReSharper disable once ValueParameterNotUsed
            set => ResourceCleaner.ClearUsed(
                World.DefaultGameObjectInjectionWorld, ResourceCleaner.ResourceType.Ore);
        }

        // === 恢复石油 ===
        [SettingsUIButton]
        [SettingsUISection(kGroupRestore)]
        [SettingsUIConfirmation]
        public bool RestoreOil
        {
            // ReSharper disable once ValueParameterNotUsed
            set => ResourceCleaner.ClearUsed(
                World.DefaultGameObjectInjectionWorld, ResourceCleaner.ResourceType.Oil);
        }

        // === 恢复鱼类 ===
        [SettingsUIButton]
        [SettingsUISection(kGroupRestore)]
        [SettingsUIConfirmation]
        public bool RestoreFish
        {
            // ReSharper disable once ValueParameterNotUsed
            set => ResourceCleaner.ClearUsed(
                World.DefaultGameObjectInjectionWorld, ResourceCleaner.ResourceType.Fish);
        }

        // === 一键全部恢复 ===
        [SettingsUIButton]
        [SettingsUISection(kGroupRestore)]
        [SettingsUIConfirmation]
        public bool RestoreAll
        {
            // ReSharper disable once ValueParameterNotUsed
            set => ResourceCleaner.ClearUsed(
                World.DefaultGameObjectInjectionWorld, ResourceCleaner.ResourceType.All);
        }

        #endregion

        public override void SetDefaults()
        {
            InfiniteFertility = false;
            InfiniteOre = false;
            InfiniteOil = false;
            InfiniteFish = false;
        }
    }
}
