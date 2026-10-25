using System;
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
        // === 支持的扩展名（不含前导点） ===
        private static readonly string[] ExtraExtensions = { ".mp3", ".wav" };

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
            // --- 记下是否加载了 ExtendedRadio，只为排错时查日志，不影响任何补丁或注册 ---
            //
            // 為何副檔名註冊必須無條件執行：
            //   ExtendedRadio 的 mp3/wav/flac 副檔名映射受其自身設定開關 gating，
            //   而那些開關預設全部為 false（見 ExtendedRadio Settings.cs）。
            //   若我們在偵測到它時就 return，兩邊都不會註冊 .mp3/.wav →
            //   這些副檔名沒有型別映射 → AssetDatabase.user.AddAsset 回傳 null →
            //   SimpleRadio 的 mp3/wav 檔案全數靜默失敗，電台空掉。
            //   原版 DefaultAssetFactory 對重複副檔名僅 warn 不拋例外，故無條件註冊是安全的。
            if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "ExtendedRadio"))
            {
                Mod.Logger.Info("检测到 ExtendedRadio 已加载。");
            }

            // --- 注册扩展名（无条件执行）---
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
