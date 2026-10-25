using System;
using System.IO;
using System.Threading.Tasks;
using Colossal.IO.AssetDatabase;
using HarmonyLib;
using SimpleRadio.Core;
using UnityEngine;

namespace SimpleRadio.Patches
{
    /// <summary>
    /// 拦截 AudioAsset.LoadAsync，根据文件扩展名动态选择正确的 AudioType 解码器。
    ///
    /// 原因：原版 LoadAsync 默认使用 AudioType.OGGVORBIS，
    /// 对 MP3/WAV 文件会导致解码失败或无声。
    ///
    /// 安全策略：
    /// - 仅拦截 SimpleRadio 注册的 AudioAsset（通过 tags 识别）
    /// - 非 SimpleRadio 的资源放行，不影响原版和其他 mod
    /// - 由 <c>Mod.OnLoad</c> <b>无条件</b>注册，不依赖其他 Mod 是否存在
    ///
    /// 其他 Mod 也 Prefix <c>LoadAsync</c> 时：都没设 <c>HarmonyPriority</c> 的 Prefix
    /// 先后顺序不保证，先 return false 的一方会让后面的 Prefix 全部跳过，
    /// 这时本 Mod 资产用哪个解码器取决于对方的实现。
    ///
    /// 刻意<b>不</b>标 <c>Priority.First</c>：抢先接管会改变别的 Mod 对其自身资产的
    /// 解码时序，而本 Mod 并无收益。
    /// </summary>
    [HarmonyPatch(typeof(AudioAsset), nameof(AudioAsset.LoadAsync))]
    public static class AudioLoadPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(AudioAsset __instance, ref Task<AudioClip> __result,
            bool useCached = true, bool streamAudio = true, AudioType audioType = AudioType.OGGVORBIS)
        {
            try
            {
                // --- 仅拦截 SimpleRadio 的 AudioAsset ---
                if (!IsSimpleRadioAsset(__instance)) return true;

                // --- 根据扩展名选择解码器 ---
                string ext = Path.GetExtension(__instance.path);
                AudioType correctType = AudioFormatHelper.GetAudioType(ext);

                // 如果已经是正确的类型，无需拦截
                if (correctType == audioType) return true;

                __result = __instance.LoadAsyncFile(useCached, streamAudio, correctType);
                return false;
            }
            catch (Exception e)
            {
                Mod.Logger.Error(e, "AudioLoadPatch.Prefix 失败，回退到原版");
                return true;
            }
        }

        /// <summary>
        /// 通过 tags 判断是否为 SimpleRadio 注册的 AudioAsset。
        /// </summary>
        private static bool IsSimpleRadioAsset(AudioAsset asset)
        {
            try
            {
                var tags = asset.tags;
                if (tags == null) return false;

                foreach (var tag in tags)
                {
                    if (tag != null && tag.Contains(StationLoader.NetworkKey))
                        return true;
                }
            }
            catch { /* tags 读取失败，视为非 SimpleRadio 资源 */ }

            return false;
        }
    }
}
