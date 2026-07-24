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
	/// <para>尋路 Setup 統一分派路由器 —— 全 Mod 唯一掛在 <c>PathfindSetupSystem.FindTargets</c> 上的 Harmony Prefix。</para>
	/// <para>
	/// 依 <see cref="SetupTargetType"/> 查註冊表：命中且 handler 啟用 → 接管排程並 <c>return false</c> 跳過原版；
	/// 否則 <c>return true</c> 放行原版。P1（ResourceSeller）等優化皆掛此框架，避免多 Prefix 疊掛衝突。
	/// </para>
	/// <para>
	/// <b>與 C1 找房 Prefix 並存說明</b>：ModeA/B/C/D/E 的 <c>PathfindSetupSystem_FindTargets_Patch</c>
	/// 只接管 <see cref="SetupTargetType.FindHome"/>（對其餘類型 <c>return true</c>）；本 dispatcher 只註冊
	/// <see cref="SetupTargetType.ResourceSeller"/> 等非 FindHome 類型。兩者接管類型互斥，
	/// 無論 Harmony 執行順序如何都不會雙重 <c>return false</c> 搶奪 <c>__result</c>。
	/// </para>
	/// </summary>
	[HarmonyPatch]
	public static class PathfindSetupDispatcher
	{
		#region Registry

		// --- SetupTargetType → handler 註冊表 ---
		private static readonly Dictionary<SetupTargetType, IFindTargetsHandler> s_Handlers =
			new Dictionary<SetupTargetType, IFindTargetsHandler>();

		/// <summary>
		/// 註冊一個 handler。應在 <c>SystemReplacer.Apply()</c> 依 settings 呼叫。
		/// 重複註冊同一 type 會覆蓋（World 重建後重新 Apply 的冪等行為）。
		/// </summary>
		public static void Register(SetupTargetType type, IFindTargetsHandler handler)
		{
			s_Handlers[type] = handler;
			ModLog.Ok("PathfindSetupDispatcher", $"已註冊 handler: {type} -> {handler.GetType().Name}");
		}

		/// <summary>清空所有 handler（World 重建 / 模式切換時避免懸掛舊 World 的靜態快取）。</summary>
		public static void Clear() => s_Handlers.Clear();

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
