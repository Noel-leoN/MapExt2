using System;
using System.Linq;
using Colossal.IO.AssetDatabase;
using UnityEngine;

namespace SimpleRadio.Core
{
    /// <summary>
    /// 音频格式工具类：扩展名映射、AssetDatabase 注册、ExtendedRadio 检测。
    /// </summary>
    public static class AudioFormatHelper
    {
        // === 支持的扩展名（不含前导点） ===
        private static readonly string[] ExtraExtensions = { ".mp3", ".wav" };

        /// <summary>
        /// 检测 ExtendedRadio 是否已加载。
        /// 若已加载，其全局 LoadAsync Prefix 已覆盖所有格式，我们无需重复 patch。
        /// </summary>
        public static bool IsExtendedRadioLoaded { get; private set; }

        /// <summary>
        /// 根据文件扩展名返回对应的 Unity AudioType。
        /// </summary>
        public static AudioType GetAudioType(string extension)
        {
            if (string.IsNullOrEmpty(extension)) return AudioType.OGGVORBIS;

            return extension.TrimStart('.').ToUpperInvariant() switch
            {
                "OGG" => AudioType.OGGVORBIS,
                "MP3" => AudioType.MPEG,
                "WAV" => AudioType.WAV,
                _ => AudioType.OGGVORBIS
            };
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
            // --- 检测 ExtendedRadio ---
            IsExtendedRadioLoaded = AppDomain.CurrentDomain.GetAssemblies()
                .Any(a => a.GetName().Name == "ExtendedRadio");

            if (IsExtendedRadioLoaded)
            {
                Mod.Logger.Info("检测到 ExtendedRadio 已加载，跳过格式扩展名注册和 LoadAsync 补丁。");
                return;
            }

            // --- 注册扩展名 ---
            var factory = DefaultAssetFactory.instance;
            foreach (var ext in ExtraExtensions)
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
