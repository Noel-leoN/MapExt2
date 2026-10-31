// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

using System;
using System.Threading.Tasks;
using Colossal.IO.AssetDatabase;
using Colossal.Serialization.Entities;
using Game;
using Game.Assets;
using Game.SceneFlow;
using HarmonyLib;
using MapExtPDX.MapExt.Core;

namespace MapExtPDX.SaveLoadSystem
{
    /// <summary>
    /// 存档入口闸门第 2 层：汇合点 <c>GameManager.Load(GameMode, Purpose, IAssetData)</c>。
    /// 菜单各入口、命令行 <c>startGame=</c>、快速读档与其他 Mod 的直接调用最终都走到这里；
    /// <c>Load(GameMode, Purpose, Hash128)</c> 重载也转进这里。
    ///
    /// <para><b>只拦不放</b>：菜单层放行时发了许可的加载直接放行；没有许可的（命令行、其他 Mod、菜单层失效）在这里判定，
    /// 不符就拦下并说明，不提供"仍要加载"与转换——那两项只在菜单层提供。</para>
    ///
    /// <para><b>拦下时</b>：返回 <c>Task.FromResult(false)</c> 跳过原方法，并复位菜单已写入的城市配置覆盖项，
    /// 否则被取消的开局选项会套到下一次读档上。</para>
    ///
    /// <para><b>已知缺口</b>：资产是 <c>MapData</c>／<c>SaveGameData</c> 时不带元数据，无法判定，记 Warn 放行。</para>
    /// </summary>
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.Load), new[] { typeof(GameMode), typeof(Purpose), typeof(IAssetData) })]
    public static class GameManagerLoadGatePatch
    {
        private const string Tag = nameof(GameManagerLoadGatePatch);

        /// <returns>true 放行原方法；false 已拦下，<paramref name="__result"/> 为 false。</returns>
        [HarmonyPrefix]
        public static bool BeforeLoad(GameMode mode, Purpose purpose, IAssetData asset, ref Task<bool> __result)
        {
            // 只管游戏模式下的读档与开新城；回主菜单（Cleanup）与编辑器照常放行
            if (mode != GameMode.Game || (purpose != Purpose.LoadGame && purpose != Purpose.NewGame)) return true;

            try
            {
                GateDecision decision;
                switch (asset)
                {
                    case SaveGameMetadata saveMeta:
                    {
                        SaveInfo save = saveMeta.target;
                        if (save == null) return true;

                        if (SaveEntryGate.TryConsumePermit(save.id))
                        {
                            VanillaConversionState.MarkLoading(save.id);
                            return true;
                        }

                        decision = SaveEntryGate.EvaluateSave(SaveEntry.Convergence, save);
                        if (decision.Verdict == GateVerdict.Allow)
                        {
                            SaveEntryGate.ApplyAllow(decision, save);
                            VanillaConversionState.MarkLoading(save.id);
                            return true;
                        }

                        ModLog.Warn(Tag, $"汇合点拦下读档 '{save.displayName}'（{decision.Verdict}），没有经过菜单层的判定");
                        break;
                    }

                    case MapMetadata mapMeta:
                    {
                        MapInfo map = mapMeta.target;
                        if (map == null) return true;
                        if (SaveEntryGate.TryConsumePermit(map.id)) return true;

                        decision = SaveEntryGate.EvaluateNewGame(SaveEntry.Convergence, map);
                        if (decision.Verdict == GateVerdict.Allow) return true;

                        ModLog.Warn(Tag, $"汇合点拦下开新城 '{map.displayName}'（{decision.Verdict}），没有经过菜单层的判定");
                        break;
                    }

                    case null:
                        return true;

                    default:
                        ModLog.Warn(Tag, $"加载的资产是 {asset.GetType().Name}，不带存档元数据，无法校验，放行");
                        return true;
                }

                __result = Task.FromResult(false);
                SaveEntryGate.BlockAtConvergence(decision);
                return false;
            }
            catch (Exception ex)
            {
                ModLog.Error(Tag, ex, "汇合点校验异常，放行");
                return true;
            }
        }
    }
}
