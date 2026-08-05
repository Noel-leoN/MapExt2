// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.

namespace MapExtPDX.SaveLoadSystem
{
    /// <summary>
    /// 地圖尺寸錯配的全域偵測狀態。
    ///
    /// <para><b>為何需要跨層傳遞</b>：偵測必須發生在
    /// <c>TerrainSystem.FinalizeTerrainData</c> 的 Prefix——只有該處的 <c>inMapSize</c>
    /// 還是地圖檔的原始權威值，之後即被寫入 <c>playableArea</c> 而無法還原。
    /// 但此刻正處於反序列化中途，UI 尚未就緒，無法彈窗；
    /// 故先在此記錄，延到 <c>OnGameLoadingComplete</c> 由
    /// <see cref="MapExtPDX.MapExt.Core.ConflictMonitoringSystem"/> 消費並提示。</para>
    /// </summary>
    public static class MapSizeMismatchState
    {
        /// <summary>本次載入是否偵測到地圖製作模式與當前模式不符。</summary>
        public static bool HasMismatch { get; set; } = false;

        /// <summary>地圖製作時所用的 CoreValue（由 inMapSize / 14336 推得）。</summary>
        public static int AuthoredCoreValue { get; set; } = 0;

        /// <summary>偵測當下的 Mod CoreValue。</summary>
        public static int CurrentCoreValue { get; set; } = 0;

        /// <summary>已對本次錯配彈過提示，避免重複打擾。</summary>
        public static bool DialogShown { get; set; } = false;

        /// <summary>記錄一次錯配（保留最早一次，後續重入不覆寫）。</summary>
        public static void Record(int authoredCV, int currentCV)
        {
            if (HasMismatch) return;

            HasMismatch = true;
            AuthoredCoreValue = authoredCV;
            CurrentCoreValue = currentCV;
        }

        /// <summary>重置所有狀態（場景清理與每次載入開始時呼叫）。</summary>
        public static void Reset()
        {
            HasMismatch = false;
            AuthoredCoreValue = 0;
            CurrentCoreValue = 0;
            DialogShown = false;
        }
    }
}
