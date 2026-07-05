using Colossal;
using System.Collections.Generic;

namespace SimpleRadio.Settings
{
    public class LocaleHANT : IDictionarySource
    {
        private readonly ModSettings m_Setting;

        public LocaleHANT(ModSettings setting)
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
                { m_Setting.GetOptionTabLocaleID(ModSettings.kTabInfo), "資訊" },
                { m_Setting.GetOptionTabLocaleID(ModSettings.kTabFormat), "格式" },

                // === Group: Status ===
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kGroupStatus), "狀態" },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.StationInfo)), "已載入電台數" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.StationInfo)),
                    "在資料目錄中偵測到的自訂電台數量。" },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.SongInfo)), "已載入歌曲數" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.SongInfo)),
                    "所有電台中載入的音訊檔案總數。" },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.DataPath)), "資料目錄" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.DataPath)),
                    "SimpleRadio 資料目錄路徑。在此處建立子資料夾並放入音訊檔案（.ogg、.mp3、.wav）即可新增自訂電台。" },

                // === Group: Actions ===
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kGroupActions), "操作" },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.OpenDataFolder)), "開啟資料目錄" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.OpenDataFolder)),
                    "在 Windows 檔案總管中開啟 SimpleRadio 資料目錄。\n\n" +
                    "新增自訂電台的步驟：\n" +
                    "1. 建立一個子資料夾（資料夾名稱即為電台名稱）\n" +
                    "2. 將音訊檔案放入資料夾（.ogg、.mp3、.wav）\n" +
                    "3. 可選：新增 icon.svg 作為電台圖示\n" +
                    "4. 點擊「刷新電台」或重啟遊戲即可生效" },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.RefreshStations)), "♫ 刷新電台" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.RefreshStations)),
                    "重新掃描資料目錄並載入所有自訂電台，無需重啟遊戲。\n\n" +
                    "在資料目錄中新增或刪除音訊檔案後，點擊此按鈕即可刷新。" },

                // === Group: Formats ===
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kGroupFormats), "音訊格式" },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableMP3)), "啟用 MP3 支援" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableMP3)),
                    "允許載入 .mp3 音訊檔案。\n\n" +
                    "MP3 格式廣泛支援，運作穩定可靠。需要重啟遊戲生效。" },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableWAV)), "啟用 WAV 支援" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableWAV)),
                    "允許載入 .wav 音訊檔案。\n\n" +
                    "注意：WAV 是無壓縮格式，檔案體積通常是 OGG/MP3 的 10 倍（每首約 30-50 MB）。" +
                    "可能顯著增加磁碟佔用，且在傳統硬碟（HDD）上切歌時可能出現短暫卡頓。\n\n" +
                    "建議：將 WAV 轉換為 OGG 或 MP3 以獲得更好的效能。\n\n" +
                    "需要重啟遊戲生效。" },

                // === Group: Compatibility ===
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kGroupCompat), "相容性" },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ExtendedRadioStatus)), "ExtendedRadio" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ExtendedRadioStatus)),
                    "顯示是否偵測到 ExtendedRadio。\n\n" +
                    "無論是否安裝 ExtendedRadio，SimpleRadio 都可獨立運行，兩者可同時使用、互不衝突。\n\n" +
                    "若其他電台 Mod 在載入時出錯，SimpleRadio 會自動攔截，確保遊戲電台正常可用。" },
            };
        }

        public void Unload() { }
    }
}
