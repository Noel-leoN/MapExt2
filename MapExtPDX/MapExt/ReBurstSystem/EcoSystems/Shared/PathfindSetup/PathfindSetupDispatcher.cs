// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Reflection;
using Game.Pathfind;
using Game.Simulation;
using HarmonyLib;
using MapExtPDX.MapExt.Core;
using Unity.Jobs;

namespace MapExtPDX.EcoShared
{
	/// <summary>
	/// 尋路 Setup 分派處理器介面。
	/// 每個 handler 對應一種 <see cref="SetupTargetType"/>，負責用 Early Exit 等優化手段
	/// 排程一個自定義的 Setup Job，取代原版 <c>PathfindSetupSystem.FindTargets</c> 的對應分支。
	/// </summary>
	public interface IFindTargetsHandler
	{
		/// <summary>是否啟用（對應各 EcoSystem 開關；為 false 時 dispatcher 放行原版）。</summary>
		bool Enabled { get; }

		/// <summary>
		/// 排程自定義 Setup Job，回傳其 JobHandle。
		/// 語義須與原版對應的 <c>Setup*</c> 方法等價（inputDeps 取 <c>system.Dependency</c>，回傳值會被呼叫端合併回 Dependency）。
		/// </summary>
		JobHandle Schedule(PathfindSetupSystem system, ref PathfindSetupSystem.SetupData setupData);
	}

	/// <summary>
	/// <para>寻路 Setup 统一分派路由器 —— 挂在 <c>PathfindSetupSystem.FindTargets</c> 上的 Harmony Prefix（C1 完整版开启时另挂有 <c>PathfindSetupSystem_FindTargets_Patch</c>，两者按 <see cref="SetupTargetType"/> 互斥分工）。</para>
	/// <para>
	/// 依 <see cref="SetupTargetType"/> 查注册表：命中且 handler 启用 → 接管排程并 <c>return false</c> 跳过原版；
	/// 否则 <c>return true</c> 放行原版。<c>ResourceSeller</c>、<c>Leisure</c> 等优化均挂此框架，避免多 Prefix 叠挂冲突。
	/// </para>
	/// <para>
	/// <b>与 C1 找房补丁的分工</b>：C1 完整版（经济总开关与找房组都开启）由各模式的 <c>PathfindSetupSystem_FindTargets_Patch</c>
	/// 接管 <see cref="SetupTargetType.FindHome"/>（其余类型 <c>return true</c>），本 dispatcher 不登记 FindHome；
	/// CellOnly 配置下不挂 C1 补丁，FindHome 由本 dispatcher 上各模式的 <c>FindHomeHandler_CellOnly</c> 接管。
	/// 两条路径按会话配置快照二选一，同一会话里只有一方接管 FindHome，不会双重 <c>return false</c> 抢夺 <c>__result</c>。
	/// </para>
	/// </summary>
	[HarmonyPatch]
	public static class PathfindSetupDispatcher
	{
		private const string Tag = nameof(PathfindSetupDispatcher);

		#region Registry

		// --- SetupTargetType → handler 註冊表 ---
		private static readonly Dictionary<SetupTargetType, IFindTargetsHandler> s_Handlers =
			new Dictionary<SetupTargetType, IFindTargetsHandler>();

		/// <summary>
		/// 注册一个 handler，由 <c>ReplacementRegistrar.FindTargetsHandler</c> 经 <c>SystemReplacer</c> 在 OnLoad 时一次性注册。
		/// 重复注册同一 type 会覆盖（保持幂等）。
		/// </summary>
		public static void Register(SetupTargetType type, IFindTargetsHandler handler)
		{
			s_Handlers[type] = handler;
			ModLog.Ok(Tag, $"已注册 handler: {type} -> {handler.GetType().Name}");
		}

		/// <summary>清空所有 handler（World 重建 / 模式切換時避免懸掛舊 World 的靜態快取）。</summary>
		public static void Clear() => s_Handlers.Clear();

		/// <summary>当前登记的 handler（只读），供注册表导出列出各类型由谁接管。</summary>
		internal static IReadOnlyDictionary<SetupTargetType, IFindTargetsHandler> Handlers => s_Handlers;

		#endregion

		#region Harmony Prefix

		[HarmonyTargetMethod]
		public static MethodBase TargetMethod()
		{
			// FindTargets(SetupTargetType, in SetupData) —— private instance 方法（in 參數為 ByRef）
			return typeof(PathfindSetupSystem).GetMethod("FindTargets",
				BindingFlags.NonPublic | BindingFlags.Instance,
				null,
				new[] { typeof(SetupTargetType), typeof(PathfindSetupSystem.SetupData).MakeByRefType() },
				null);
		}

		public static bool Prefix(
			PathfindSetupSystem __instance,
			SetupTargetType targetType,
			ref PathfindSetupSystem.SetupData setupData,
			ref JobHandle __result)
		{
			if (s_Handlers.TryGetValue(targetType, out var handler) && handler.Enabled)
			{
				__result = handler.Schedule(__instance, ref setupData);
				return false; // 接管，跳過原版分支
			}

			return true; // 放行原版
		}

		#endregion
	}
}
