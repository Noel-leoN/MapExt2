// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

using System;
using System.Collections.Generic;
using Colossal.Serialization.Entities;
using Game.Serialization;
using HarmonyLib;
using MapExtPDX.MapExt.Core;
using Unity.Entities;

namespace MapExtPDX.SaveLoadSystem
{
    /// <summary>
    /// 28→57 存档扩展的类型路由：把 ModeB 存档里 5 张贴图的区段交给 ModeA 的同名系统读。
    ///
    /// <para><b>为什么要路由</b>：ModeA 会话只建 ModeA 的系统，ModeB 的类型不在 <c>SystemSerializerLibrary</c> 的映射表里，
    /// 这些区段会被 <c>ObsoleteSystemSerializer</c> 整块跳过，ModeA 系统因区段缺席改走 SetDefaults 清零。
    /// 这里给映射表补 5 个键：ModeB 类型 → ModeA 系统的 serializer 序号。只加键，serializer 数不变；
    /// 引擎把目标标为"已出现"，ModeA 系统不再走 SetDefaults，由基类按中心嵌入读入（见 <c>CellMapReplacementSystem</c>）。</para>
    ///
    /// <para><b>映射表只在 isDirty 时重建</b>：<c>Initialize</c> 只由 <c>SerializerSystem.OnUpdate</c> 在 isDirty 时调用，
    /// 原版只在启动加载 Mod 程序集时置 dirty。所以转换请求进入"加载中"时置 dirty，让这次读档重建映射表、
    /// 经过本 Postfix 加键；读档结束（转换系统 OnGameLoaded）或请求清除时再置 dirty，下一次存读档就重建出不带这 5 个键的表。
    /// 每次重建，旧的 <c>m_DataSizes</c>（几 KB 的 Persistent 数组）不释放，与原版启动时那次相同。</para>
    ///
    /// <para><b>类型先于数据解析</b>：存档里所有系统区段的类型在读任何数据之前一次解析完，地形否决发生在那之后，
    /// 改不了这次的路由。已路由的系统一律中心嵌入，本类记下"本次读档已路由的类型"供基类判断。</para>
    ///
    /// <para><b>只在 ModeA 挂</b>：<c>PatchManager</c> 的可选补丁集，非 CV=4 跳过。失败只让 28→57 不可用，不降级；
    /// 闸门按 <see cref="CanExpand"/> 决定给不给转换。</para>
    /// </summary>
    [HarmonyPatch(typeof(SystemSerializerLibrary), nameof(SystemSerializerLibrary.Initialize))]
    public static class SaveExpansionTypeMapPatch
    {
        private const string Tag = nameof(SaveExpansionTypeMapPatch);

        // === Constants ===

        #region Constants

        /// <summary>源模式：ModeB 28 km。</summary>
        public const int SourceCoreValue = 2;

        /// <summary>目标模式：ModeA 57 km。</summary>
        public const int TargetCoreValue = 4;

        /// <summary>白名单：ModeB 类型 → ModeA 同名系统。其余 7 个按模式的贴图是派生数据，由模拟重建。</summary>
        private static readonly (Type Source, Type Target)[] s_Routes =
        {
            (typeof(ModeB.NaturalResourceSystemMod), typeof(ModeA.NaturalResourceSystemMod)),
            (typeof(ModeB.GroundWaterSystemMod), typeof(ModeA.GroundWaterSystemMod)),
            (typeof(ModeB.GroundPollutionSystemMod), typeof(ModeA.GroundPollutionSystemMod)),
            (typeof(ModeB.AirPollutionSystemMod), typeof(ModeA.AirPollutionSystemMod)),
            (typeof(ModeB.SoilWaterSystemMod), typeof(ModeA.SoilWaterSystemMod)),
        };

        #endregion

        // === Fields ===

        #region Fields

        private static readonly AccessTools.FieldRef<SystemSerializerLibrary, Dictionary<Type, int>> s_SerializerMap =
            AccessTools.FieldRefAccess<SystemSerializerLibrary, Dictionary<Type, int>>("m_SerializerMap");

        /// <summary>白名单里的 ModeA 系统类型，转换系统按它们核对接收结果。</summary>
        public static readonly IReadOnlyList<Type> TargetTypes = Array.ConvertAll(s_Routes, r => r.Target);

        /// <summary>本次读档已路由的 ModeA 系统类型。只在主线程读写。</summary>
        private static readonly HashSet<Type> s_RoutedTargets = new HashSet<Type>();

        /// <summary>请求进入加载中时置过 dirty，读档结束要再置一次。</summary>
        private static bool s_DirtyPending;

        /// <summary>补丁已挂上（本会话是 ModeA 且应用成功）。</summary>
        public static bool IsApplied { get; private set; }

        #endregion

        // === Public API ===

        #region Public API

        /// <summary>这个存档能否走 28→57：补丁已挂上、存档是 CV=2、当前是 CV=4。</summary>
        public static bool CanExpand(int savedCoreValue, int currentCoreValue) =>
            IsApplied && savedCoreValue == SourceCoreValue && currentCoreValue == TargetCoreValue;

        /// <summary>本次读档是否把 ModeB 的区段路由给了这个系统类型；基类据此改用中心嵌入。</summary>
        public static bool IsRoutedThisLoad(Type systemType) => s_RoutedTargets.Contains(systemType);

        /// <summary>
        /// 源贴图边长：28→57 的格距不变，源边长 = 目标边长 × 源地图边长 ÷ 目标地图边长（1024 → 512，SoilWater 512 → 256）。
        /// </summary>
        public static int SourceTextureSize(int targetTextureSize, int targetMapSize) =>
            SaveExpansionGeometry.EmbedSize(targetTextureSize, SourceCoreValue * PatchManager.OriginalMapSize, targetMapSize);

        #endregion

        // === Lifecycle ===

        #region Lifecycle

        /// <summary><c>PatchManager</c> 的应用委托：只在 ModeA 挂。</summary>
        internal static void Apply(Harmony harmony)
        {
            if (PatchManager.CurrentCoreValue != TargetCoreValue)
            {
                ModLog.Info(Tag, $"28→57 存档扩展只在 ModeA 可用，本模式（CV={PatchManager.CurrentCoreValue}）不挂类型路由");
                return;
            }

            harmony.CreateClassProcessor(typeof(SaveExpansionTypeMapPatch)).Patch();
            IsApplied = true;
        }

        /// <summary>CV=2 的转换请求进入"加载中"时调用：置 dirty，让这次读档重建映射表并经过 Postfix。</summary>
        internal static void BeginLoad()
        {
            s_DirtyPending = true;
            MarkSerializerDirty("28→57 请求进入加载");
        }

        /// <summary>
        /// 读档结束或请求清除时调用：清掉本次读档的路由记录，并置 dirty 让下一次存读档重建出不带路由的映射表。
        /// 不在地形否决时调用：那时各系统还没读档，已路由的照样要中心嵌入。
        /// </summary>
        internal static void EndLoad(string reason)
        {
            if (!s_DirtyPending && s_RoutedTargets.Count == 0) return;

            s_DirtyPending = false;
            s_RoutedTargets.Clear();
            MarkSerializerDirty(reason);
        }

        #endregion

        // === HarmonyPatch ===

        #region HarmonyPatch

        /// <summary>映射表重建后：只在 CV=2 请求加载中时补 5 个键。不让异常逸出，否则整次存读档失败。</summary>
        [HarmonyPostfix]
        public static void AfterInitialize(SystemSerializerLibrary __instance)
        {
            s_RoutedTargets.Clear();

            ConversionRequest request = VanillaConversionState.Current;
            if (request == null || !request.IsSaveExpansion || request.Phase != ConversionPhase.Loading) return;

            try
            {
                Dictionary<Type, int> map = s_SerializerMap(__instance);
                foreach ((Type source, Type target) in s_Routes)
                {
                    if (map.ContainsKey(source))
                    {
                        ModLog.Warn(Tag, $"{source.FullName} 已在映射表里（本会话建了 ModeB 系统？），不路由");
                        continue;
                    }

                    if (!map.TryGetValue(target, out int index))
                    {
                        ModLog.Warn(Tag, $"{target.FullName} 不在映射表里，{source.Name} 的区段没有去处");
                        continue;
                    }

                    map[source] = index;
                    s_RoutedTargets.Add(target);
                }

                ModLog.Swap(Tag, $"28→57 类型路由：{s_RoutedTargets.Count}/{s_Routes.Length} 个 ModeB 类型改由 ModeA 系统读入（{request}）");
            }
            catch (Exception ex)
            {
                ModLog.Error(Tag, ex, "28→57 类型路由失败，本次读档按原样处理 ModeB 区段");
            }
        }

        #endregion

        // === Helpers ===

        #region Helpers

        /// <summary>置 dirty 只影响系统库（<c>SerializerSystem.SetDirty</c> 会连组件库一起重建）。还没建过时它本来就是 dirty。</summary>
        private static void MarkSerializerDirty(string reason)
        {
            SystemSerializerLibrary library = World.DefaultGameObjectInjectionWorld?
                .GetExistingSystemManaged<SerializerSystem>()?.systemLibrary;
            if (library == null)
            {
                ModLog.Info(Tag, $"映射表还没建立，下一次存读档会初次建立（{reason}）");
                return;
            }

            library.SetDirty();
            ModLog.Info(Tag, $"已置映射表在下一次存读档时重建（{reason}）");
        }

        #endregion
    }
}
