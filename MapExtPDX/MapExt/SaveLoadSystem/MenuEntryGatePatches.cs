// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit MapExt2 and the author.

using System;
using Game.Assets;
using Game.SceneFlow;
using Game.UI.Menu;
using HarmonyLib;
using MapExtPDX.MapExt.Core;
using static Game.UI.Menu.MenuUISystem;

namespace MapExtPDX.SaveLoadSystem
{
    /// <summary>
    /// 存档入口闸门第 1 层：主菜单与启动器的"继续游戏"（<c>MenuUISystem.SafeContinueGame</c>）。
    ///
    /// <para>"继续"读的是最近一次成功存档（<c>userState.lastSaveGameMetadata</c>，跨重启保留），
    /// 切换模式并重启后，它仍指向上一个模式的城市。</para>
    ///
    /// <para>启动器"继续"在启动阶段经界面走到这里，那时还没有主菜单：引擎只触发界面事件就返回，不会再自己加载主菜单，
    /// 所以拦下后要转入主菜单，对话框也推迟到主菜单加载完成后再弹。</para>
    /// </summary>
    [HarmonyPatch(typeof(MenuUISystem), "SafeContinueGame")]
    public static class ContinueGameGatePatch
    {
        private const string Tag = nameof(ContinueGameGatePatch);

        /// <returns>true 放行原方法；false 已拦下。</returns>
        [HarmonyPrefix]
        public static bool BeforeContinueGame(MenuUISystem __instance)
        {
            try
            {
                SaveGameMetadata last = GameManager.instance?.settings?.userState?.lastSaveGameMetadata;
                if (last == null || !last.isValidSaveGame) return true; // 原版这时什么也不做

                SaveInfo save = last.target;
                if (save == null) return true;

                bool booting = SaveEntryGate.IsBooting;
                // 记录调用来源（启动器启动阶段 vs 主菜单），便于诊断入口路径
                ModLog.Info(Tag, $"继续游戏 '{save.displayName}'（{(booting ? "启动阶段，来自启动器" : "主菜单")}）");

                GateDecision decision = SaveEntryGate.EvaluateSave(SaveEntry.MenuContinue, save);
                bool allow = SaveEntryGate.HandleAtMenu(SaveEntry.MenuContinue, decision, save.id, save,
                    () => AccessTools.Method(typeof(MenuUISystem), "SafeContinueGame").Invoke(__instance, null));

                if (!allow && booting) SaveEntryGate.RequestMainMenu();
                return allow;
            }
            catch (Exception ex)
            {
                ModLog.Error(Tag, ex, "继续游戏校验异常，交给汇合点判定");
                return true;
            }
        }
    }

    /// <summary>
    /// 存档入口闸门第 1 层：主菜单开新城（<c>MenuUISystem.SafeNewGame</c>）。
    /// 在原方法写入城市名、主题、游戏模式与开局选项之前判定：按地图面积推断制作模式，再看降级。
    /// 面积不可信的地图放行，载入后按地形实际尺寸判。
    /// </summary>
    [HarmonyPatch(typeof(MenuUISystem), "SafeNewGame")]
    public static class NewGameGatePatch
    {
        private const string Tag = nameof(NewGameGatePatch);

        /// <returns>true 放行原方法；false 已拦下。</returns>
        [HarmonyPrefix]
        public static bool BeforeNewGame(MenuUISystem __instance, NewGameArgs args, bool dismiss)
        {
            try
            {
                MapInfo map = SaveEntryGate.FindMenuMap(__instance, args.mapId);
                if (map == null) return true; // 交给汇合点判定

                GateDecision decision = SaveEntryGate.EvaluateNewGame(SaveEntry.MenuNewGame, map);
                // 转换后的新档按城市名命名，玩家没填城市名时用地图名
                string displayName = string.IsNullOrWhiteSpace(args.cityName) ? map.displayName : args.cityName;
                return SaveEntryGate.HandleAtMenu(SaveEntry.MenuNewGame, decision, map.id, null,
                    () => AccessTools.Method(typeof(MenuUISystem), "SafeNewGame").Invoke(__instance, new object[] { args, dismiss }),
                    displayName);
            }
            catch (Exception ex)
            {
                ModLog.Error(Tag, ex, "开新城校验异常，交给汇合点判定");
                return true;
            }
        }
    }

    /// <summary>
    /// 存档入口闸门第 1 层：游戏内快速读档（<c>MenuUISystem.SafeQuickLoad</c>，读最近一次成功存档）。
    ///
    /// <para><b>为什么不能只靠汇合点</b>：<c>QuickLoad</c> 直接调 <c>ContinueGame</c>，后者在 <c>GameManager.Load</c> 返回后
    /// 不看结果，照样把"上次存档名"改成被拦的那个档；快速存档就用这个名字。只在汇合点拦下时，玩家接着快速存档，
    /// 会把当前城市存成另一座城市的档名，关掉过覆盖确认时直接覆盖。</para>
    ///
    /// <para>不提供转换（要转换请从主菜单读档），降级时仍可选"仍要加载"。</para>
    /// </summary>
    [HarmonyPatch(typeof(MenuUISystem), "SafeQuickLoad")]
    public static class QuickLoadGatePatch
    {
        private const string Tag = nameof(QuickLoadGatePatch);

        /// <returns>true 放行原方法；false 已拦下。</returns>
        [HarmonyPrefix]
        public static bool BeforeQuickLoad(MenuUISystem __instance, bool dismiss)
        {
            try
            {
                if (!MenuHelpers.hasPreviouslySavedGame) return true; // 原版这时什么也不做

                SaveInfo save = GameManager.instance.settings.userState.lastSaveGameMetadata?.target;
                if (save == null) return true;

                GateDecision decision = SaveEntryGate.EvaluateSave(SaveEntry.QuickLoad, save);
                return SaveEntryGate.HandleAtMenu(SaveEntry.QuickLoad, decision, save.id, save,
                    () => AccessTools.Method(typeof(MenuUISystem), "SafeQuickLoad").Invoke(__instance, new object[] { dismiss }));
            }
            catch (Exception ex)
            {
                ModLog.Error(Tag, ex, "快速读档校验异常，交给汇合点判定");
                return true;
            }
        }
    }
}
