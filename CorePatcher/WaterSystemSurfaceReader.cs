// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

using HarmonyLib;
using System.Reflection;
using UnityEngine;
using System;
using Game.Simulation; 
using Game.Rendering;
using Unity.Jobs;
using Unity.Mathematics; 

namespace MapExtPDX
{
    [HarmonyPatch(typeof(WaterSystem))]
    internal static class GetSurfaceDataPatch
    {
        [HarmonyPatch(nameof(WaterSystem.GetSurfaceData))]
        [HarmonyPostfix]
        static void GetSurfaceData_Postfix(ref WaterSurfaceData __result, WaterSystem __instance,ref JobHandle deps) // Prefix runs before original
        {
            Mod.Logger.Info($"WaterSystemj读取GetSurfaceData为 (Expected value: {__result.scale}, {__result.offset})");
            float scalex = __result.scale.x / 4;
            float scaley = __result.scale.y;
            float scalez = __result.scale.z / 4;
            float3 scale = new float3(scalex, scaley, scalez);

            float offsetx = __result.offset.x * 4;
            float offsety = 0;
            float offsetz = __result.offset.z * 4;
            float3 offset = new float3(offsetx, offsety, offsetz);
            __result = new WaterSurfaceData(__result.depths, __result.resolution, scale, offset);
            Mod.Logger.Info($"WaterSystemj修补GetSurfaceData为 (Expected value: {__result.scale}, {__result.offset})");
        }

        [HarmonyPatch(nameof(WaterSystem.GetVelocitiesSurfaceData))]
        [HarmonyPostfix]
        static void GetVelocitiesSurfaceData_Postfix(ref WaterSurfaceData __result, WaterSystem __instance, ref JobHandle deps) // Prefix runs before original
        {
            Mod.Logger.Info($"WaterSystemj读取GetSurfaceData为 (Expected value: {__result.scale}, {__result.offset})");
            float scalex = __result.scale.x / 4;
            float scaley = __result.scale.y;
            float scalez = __result.scale.z / 4;
            float3 scale = new float3(scalex, scaley, scalez);

            float offsetx = __result.offset.x * 4;
            float offsety = 0;
            float offsetz = __result.offset.z * 4;
            float3 offset = new float3(offsetx, offsety, offsetz);
            __result = new WaterSurfaceData(__result.depths, __result.resolution, scale, offset);
            Mod.Logger.Info($"WaterSystemj修补GetSurfaceData为 (Expected value: {__result.scale}, {__result.offset})");
        }
    }
}
