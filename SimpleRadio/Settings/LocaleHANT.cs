using Colossal;
using System.Collections.Generic;

namespace SimpleRadio.Settings
{
    public class LocaleHANT : IDictionarySource
    {
        private readonly SimpleRadioSettings m_Setting;

        public LocaleHANT(SimpleRadioSettings setting)
        {
            m_Setting = setting;
        }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(
            IList<IDictionaryEntryError> errors,
            Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                // === Mod 名稱 ===
                { m_Setting.GetSettingsLocaleID(), "SimpleRadio 簡易電台" },

                // === Tab ===
                { m_Setting.GetOptionTabLocaleID(SimpleRadioSettings.kTabInfo), "資訊" },
                { m_Setting.GetOptionTabLocaleID(SimpleRadioSettings.kTabFormat), "格式" },

                // === Group: Status ===
                { m_Setting.GetOptionGroupLocaleID(SimpleRadioSettings.kGroupStatus), "狀態" },

                { m_Setting.GetOptionLabelLocaleID(nameof(SimpleRadioSettings.StationInfo)), "已載入電台數" },
                { m_Setting.GetOptionDescLocaleID(nameof(SimpleRadioSettings.StationInfo)),
                    "在資料目錄中偵測到的自訂電台數量。" },

                { m_Setting.GetOptionLabelLocaleID(nameof(SimpleRadioSettings.SongInfo)), "已載入歌曲數" },
                { m_Setting.GetOptionDescLocaleID(nameof(SimpleRadioSettings.SongInfo)),
                    "所有電台中載入的音訊檔案總數。" },

                { m_Setting.GetOptionLabelLocaleID(nameof(SimpleRadioSettings.DataPath)), "資料目錄" },
                { m_Setting.GetOptionDescLocaleID(nameof(SimpleRadioSettings.DataPath)),
                    "SimpleRadio 資料目錄路徑。在此處建立子資料夾並放入音訊檔案（.ogg、.mp3、.wav）即可新增自訂電台。" },

                // === Group: Actions ===
                { m_Setting.GetOptionGroupLocaleID(SimpleRadioSettings.kGroupActions), "操作" },

                { m_Setting.GetOptionLabelLocaleID(nameof(SimpleRadioSettings.OpenDataFolder)), "開啟資料目錄" },
                { m_Setting.GetOptionDescLocaleID(nameof(SimpleRadioSettings.OpenDataFolder)),
                    "在 Windows 檔案總管中開啟 SimpleRadio 資料目錄。\n\n" +
                    "新增自訂電台的步驟：\n" +
                    "1. 建立一個子資料夾（資料夾名稱即為電台名稱）\n" +
                    "2. 將音訊檔案放入資料夾（.ogg、.mp3、.wav）\n" +
                    "3. 可選：新增 icon.svg 作為電台圖示\n" +
                    "4. 點擊「刷新電台」或重啟遊戲即可生效" },

                { m_Setting.GetOptionLabelLocaleID(nameof(SimpleRadioSettings.RefreshStations)), "♫ 刷新電台" },
                { m_Setting.GetOptionDescLocaleID(nameof(SimpleRadioSettings.RefreshStations)),
                    "重新掃描資料目錄並載入所有自訂電台，無需重啟遊戲。\n\n" +
                    "在資料目錄中新增或刪除音訊檔案後，點擊此按鈕即可刷新。" },

                // === 播放設定 ===
                { m_Setting.GetOptionGroupLocaleID(SimpleRadioSettings.kGroupPlayback), "播放" },
                { m_Setting.GetOptionLabelLocaleID(nameof(SimpleRadioSettings.RestoreLastStation)), "恢復上次使用的電台" },
                { m_Setting.GetOptionDescLocaleID(nameof(SimpleRadioSettings.RestoreLastStation)),
                    "延續上次使用的電台，跨城市共用。關閉後，各城市使用存檔記錄的電台。" +
                    "下次進入城市時生效。" },

                // === Group: Formats ===
                { m_Setting.GetOptionGroupLocaleID(SimpleRadioSettings.kGroupFormats), "音訊格式" },

                { m_Setting.GetOptionLabelLocaleID(nameof(SimpleRadioSettings.EnableMP3)), "啟用 MP3 支援" },
                { m_Setting.GetOptionDescLocaleID(nameof(SimpleRadioSettings.EnableMP3)),
                    "允許載入 .mp3 音訊檔案。\n\n" +
                    "MP3 格式廣泛支援，運作穩定可靠。點擊「刷新電台」即可生效。" },

                { m_Setting.GetOptionLabelLocaleID(nameof(SimpleRadioSettings.EnableWAV)), "啟用 WAV 支援" },
                { m_Setting.GetOptionDescLocaleID(nameof(SimpleRadioSettings.EnableWAV)),
                    "允許載入 .wav 音訊檔案。\n\n" +
                    "注意：WAV 是無壓縮格式，檔案體積通常是 OGG/MP3 的 10 倍（每首約 30-50 MB）。" +
                    "可能顯著增加磁碟佔用，且在傳統硬碟（HDD）上切歌時可能出現短暫卡頓。\n\n" +
                    "建議：將 WAV 轉換為 OGG 或 MP3 以獲得更好的效能。\n\n" +
                    "點擊「刷新電台」即可生效。" },

                // === Group: Compatibility ===
                { m_Setting.GetOptionGroupLocaleID(SimpleRadioSettings.kGroupCompat), "相容性" },

                { SimpleRadioSettings.kLocaleExtendedRadioDetected, "已偵測到 — 相容" },
                { SimpleRadioSettings.kLocaleExtendedRadioMissing, "未偵測到" },

                { m_Setting.GetOptionLabelLocaleID(nameof(SimpleRadioSettings.ExtendedRadioStatus)), "ExtendedRadio" },
                { m_Setting.GetOptionDescLocaleID(nameof(SimpleRadioSettings.ExtendedRadioStatus)),
                    "顯示是否偵測到 ExtendedRadio。\n\n" +
                    "無論是否安裝 ExtendedRadio，SimpleRadio 都可獨立運行，兩者可同時使用、互不衝突。\n\n" +
                    "若其他電台 Mod 在載入時出錯，SimpleRadio 會自動攔截，確保遊戲電台正常可用。" },
            };
        }

        public void Unload() { }
    }
}
