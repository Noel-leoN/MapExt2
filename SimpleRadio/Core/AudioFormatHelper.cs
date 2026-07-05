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
        /// 偵測 ExtendedRadio 是否已載入。
        /// 純資訊用途：僅供設定面板顯示相容狀態，不再作為任何 patch 的開關。
        /// SimpleRadio 採「軟獨佔」策略，副檔名註冊與 AudioLoadPatch 一律無條件執行，
        /// 對自身資產完全自主解碼，不依賴 ExtendedRadio 的任何行為。
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
            // --- 偵測 ExtendedRadio（純資訊用途）---
            // 此旗標僅供設定面板顯示相容狀態，不再 gating 任何 patch 或註冊行為。
            //
            // 為何副檔名註冊必須無條件執行：
            //   ExtendedRadio 的 mp3/wav/flac 副檔名映射受其自身設定開關 gating，
            //   而那些開關預設全部為 false（見 ExtendedRadio Settings.cs）。
            //   若我們在偵測到它時就 return，兩邊都不會註冊 .mp3/.wav →
            //   這些副檔名沒有型別映射 → AssetDatabase.user.AddAsset 回傳 null →
            //   SimpleRadio 的 mp3/wav 檔案全數靜默失敗，電台空掉。
            //   原版 DefaultAssetFactory 對重複副檔名僅 warn 不拋例外，故無條件註冊是安全的。
            IsExtendedRadioLoaded = AppDomain.CurrentDomain.GetAssemblies()
                .Any(a => a.GetName().Name == "ExtendedRadio");

            if (IsExtendedRadioLoaded)
            {
                Mod.Logger.Info("偵測到 ExtendedRadio 已載入：SimpleRadio 以獨立模式運作（無條件註冊副檔名與 AudioLoadPatch，自主解碼，並防護第三方電台 Mod 於 LoadRadio 的崩潰）。");
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
