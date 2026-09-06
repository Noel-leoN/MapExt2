// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.

using System.Collections.Generic;
using Colossal;

namespace MapExtPDX.MapExt.Core
{
    /// <summary>
    /// 把多個 <see cref="IDictionarySource"/> 併成一份對外註冊，使同一個語言只需呼叫一次
    /// <c>LocalizationManager.AddSource</c>。
    ///
    /// <para><b>為什麼要合併（不是為了整潔，是為了避開一個引擎競態）</b>：
    /// <c>LocalizationManager.AddSourceInternal</c> 在 localeId 等於 fallback 語言（en-US）時，
    /// 會接著呼叫 <c>AddMissingEntriesFromFallback</c> → <c>LocalizationDictionary.MergeFrom</c>，
    /// 而後者用 <c>foreach</c> 遍歷<b>整個</b> fallback 字典（遊戲本體上萬條 ＋ 其他 Mod 的條目）。
    /// <c>LocalizationDictionary</c> 內部是裸 <c>Dictionary</c>，<c>LocalizationManager</c> 全類
    /// 沒有任何鎖：遍歷期間只要有別的執行流寫入該字典，就會拋
    /// <c>InvalidOperationException: Collection was modified</c>。</para>
    ///
    /// <para>2026-09-05 實測到一次：異常從 <c>Mod.OnLoad</c> 的第一次 <c>AddSource</c> 逸出，
    /// 被 <c>ModManager.InitializeMods</c> 的 try-catch 吞掉（只寫一行 log、不彈窗），
    /// 於是 20 個 patchset、SystemReplacer、71 個 UI binding 全部沒跑，
    /// 而玩家只看到卡頓與縮放失效，無從得知 Mod 已完全失效。</para>
    ///
    /// <para>只有 en-US 那條路徑會觸發 <c>MergeFrom</c>（active 語言走另一支、
    /// 第三語言兩個條件都不滿足），所以每少註冊一次 en-US 就少一次全字典遍歷、
    /// 少一個競爭窗口。合併是降低<b>發生率</b>；<c>Mod.AddSourceSafe</c> 的 try-catch
    /// 是降低<b>後果</b>。兩者互補，缺一不可。</para>
    /// </summary>
    public sealed class CompositeLocaleSource : IDictionarySource
    {
        private readonly IDictionarySource[] m_Sources;

        public CompositeLocaleSource(params IDictionarySource[] sources)
        {
            m_Sources = sources ?? new IDictionarySource[0];
        }

        /// <summary>
        /// 依建構順序輸出各子來源的條目。鍵衝突時後者勝——引擎的
        /// <c>LoadLocaleSource</c> 用 <c>target.Add</c>（即索引器賦值）寫入，本身即是覆寫語意。
        /// </summary>
        public IEnumerable<KeyValuePair<string, string>> ReadEntries(
            IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            foreach (IDictionarySource source in m_Sources)
            {
                if (source == null) continue;
                foreach (KeyValuePair<string, string> entry in source.ReadEntries(errors, indexCounts))
                {
                    yield return entry;
                }
            }
        }

        public void Unload()
        {
            foreach (IDictionarySource source in m_Sources)
            {
                source?.Unload();
            }
        }
    }
}
