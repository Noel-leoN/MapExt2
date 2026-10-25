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
                    "從資料目錄成功載入的自訂電台數量。" },

                { m_Setting.GetOptionLabelLocaleID(nameof(SimpleRadioSettings.SongInfo)), "已載入歌曲數" },
                { m_Setting.GetOptionDescLocaleID(nameof(SimpleRadioSettings.SongInfo)),
                    "所有電台中載入的音訊檔案總數。" },

                { m_Setting.GetOptionLabelLocaleID(nameof(SimpleRadioSettings.DataPath)), "資料目錄" },
                { m_Setting.GetOptionDescLocaleID(nameof(SimpleRadioSettings.DataPath)),
                    "SimpleRadio 資料目錄路徑。這裡的每個子資料夾是一個電台，音訊檔案（.ogg、.mp3、.wav）須直接放在電台資料夾裡，其中的子資料夾不會被掃描。" },

                // === Group: Actions ===
                { m_Setting.GetOptionGroupLocaleID(SimpleRadioSettings.kGroupActions), "操作" },

                { m_Setting.GetOptionLabelLocaleID(nameof(SimpleRadioSettings.OpenDataFolder)), "開啟資料目錄" },
                { m_Setting.GetOptionDescLocaleID(nameof(SimpleRadioSettings.OpenDataFolder)),
                    "在 Windows 檔案總管中開啟 SimpleRadio 資料目錄。\n\n" +
                    "新增自訂電台的步驟：\n" +
                    "1. 建立一個子資料夾（資料夾名稱即為電台名稱）\n" +
                    "2. 將音訊檔案（.ogg、.mp3、.wav）直接放入該資料夾，其中的子資料夾不會被掃描\n" +
                    "3. 可選：新增 icon.svg 作為電台圖示，或新增 .m3u8 播放清單指定順序模式的播放順序\n" +
                    "4. 點擊「刷新電台」或重啟遊戲即可生效" },

                { m_Setting.GetOptionLabelLocaleID(nameof(SimpleRadioSettings.RefreshStations)), "♫ 刷新電台" },
                { m_Setting.GetOptionDescLocaleID(nameof(SimpleRadioSettings.RefreshStations)),
                    "重新掃描資料目錄並載入所有自訂電台，無需重啟遊戲。\n\n" +
                    "新增、刪除音訊檔案或修改播放清單後，點擊此按鈕即可刷新。" },

                // === 播放設定 ===
                { m_Setting.GetOptionGroupLocaleID(SimpleRadioSettings.kGroupPlayback), "播放" },
                { m_Setting.GetOptionLabelLocaleID(nameof(SimpleRadioSettings.RestoreLastStation)), "恢復上次使用的電台" },
                { m_Setting.GetOptionDescLocaleID(nameof(SimpleRadioSettings.RestoreLastStation)),
                    "延續上次使用的電台，跨城市共用。關閉後，各城市使用存檔記錄的電台。" +
                    "下次進入城市時生效。" },

                { m_Setting.GetOptionLabelLocaleID(nameof(SimpleRadioSettings.PlaylistTip)),
                    "提示：順序模式下，在電台資料夾放一個 .m3u8 播放清單即可自訂播放順序，改完點擊「刷新電台」。" },

                { m_Setting.GetOptionLabelLocaleID(nameof(SimpleRadioSettings.PlaybackOrder)), "播放順序" },
                { m_Setting.GetOptionDescLocaleID(nameof(SimpleRadioSettings.PlaybackOrder)),
                    "自訂電台的歌曲播放順序。\n\n" +
                    "隨機：每輪每首歌各播一次，順序隨機；新一輪開頭不會是上一輪最後播的幾首。\n\n" +
                    "順序：按檔名順序播放（與檔案總管的排序一致），播完從頭循環。" +
                    "電台資料夾裡有 .m3u 或 .m3u8 播放清單（UTF-8 編碼）時，先按清單順序，清單沒列到的檔案按檔名排在後面。" +
                    "修改播放清單後點擊「刷新電台」生效。\n\n" +
                    "切換後，正在播放和已排入佇列的歌會先播完，之後按新順序繼續。" },
                { m_Setting.GetEnumValueLocaleID(PlaybackOrder.Shuffle), "隨機" },
                { m_Setting.GetEnumValueLocaleID(PlaybackOrder.Sequential), "順序" },

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
            };
        }

        public void Unload() { }
    }
}
