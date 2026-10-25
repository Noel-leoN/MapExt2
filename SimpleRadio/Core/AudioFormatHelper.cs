using System;
using System.Collections.Generic;
using System.Linq;
using Colossal.IO.AssetDatabase;
using UnityEngine;

namespace SimpleRadio.Core
{
    /// <summary>
    /// 音频格式工具类：扩展名映射与 AssetDatabase 注册。
    /// 扩展名注册与 AudioLoadPatch 一律无条件执行，自身资产自行解码，不依赖其他 Mod 的行为。
    /// </summary>
    public static class AudioFormatHelper
    {
        // === 额外注册的扩展名（含前导点）及其解码器；.ogg 由原版注册，表外的按原版默认的 OGG 解码 ===
        private static readonly Dictionary<string, AudioType> ExtraDecoders =
            new Dictionary<string, AudioType>(StringComparer.OrdinalIgnoreCase)
            {
                [".mp3"] = AudioType.MPEG,
                [".wav"] = AudioType.WAV
            };

        /// <summary>
        /// 根据文件扩展名（含前导点，不分大小写）返回对应的 Unity AudioType。
        /// </summary>
        public static AudioType GetAudioType(string extension)
        {
            return extension != null && ExtraDecoders.TryGetValue(extension, out var type)
                ? type
                : AudioType.OGGVORBIS;
        }

        /// <summary>
        /// 获取当前启用的所有扩展名（基于设置面板开关）。
        /// 始终包含 .ogg。
        /// </summary>
        public static string[] GetEnabledExtensions()
        {
            var settings = Mod.Instance?.Settings;

            // 基础始终包含 .ogg
            int count = 1;
            bool mp3 = settings?.EnableMP3 ?? false;
            bool wav = settings?.EnableWAV ?? false;
            if (mp3) count++;
            if (wav) count++;

            var result = new string[count];
            int i = 0;
            result[i++] = ".ogg";
            if (mp3) result[i++] = ".mp3";
            if (wav) result[i++] = ".wav";
            return result;
        }

        /// <summary>
        /// 在 AssetDatabase 中注册额外的音频格式扩展名映射。
        /// 必须在 Harmony patch 注册前调用。
        /// </summary>
        public static void RegisterExtensions()
        {
            // --- 记下是否加载了 ExtendedRadio，只为排错时查日志，不影响任何补丁或注册 ---
            if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "ExtendedRadio"))
            {
                Mod.Logger.Info("检测到 ExtendedRadio 已加载。");
            }

            // --- 注册扩展名（无条件执行）---
            // 不能因为别的 Mod 也可能注册 .mp3/.wav 就跳过：对方未必真的注册了，而这些扩展名没有类型映射时
            // AssetDatabase.user.AddAsset 返回 null，本 Mod 的 mp3/wav 会全部静默失败。
            // 原版 DefaultAssetFactory 对重复扩展名只警告、不抛异常，所以无条件注册是安全的。
            var factory = DefaultAssetFactory.instance;
            foreach (var ext in ExtraDecoders.Keys)
            {
                try
                {
                    factory.MapSupportedExtension<AudioAsset>(ext, false);
                    Mod.Logger.Info($"已注册音频格式: {ext}");
                }
                catch (Exception e)
                {
                    // 扩展名已被其他 mod 注册时会 warn，不影响运行
                    Mod.Logger.Warn($"注册音频格式 {ext} 时异常（可能已被其他 mod 注册）: {e.Message}");
                }
            }
        }
    }
}
