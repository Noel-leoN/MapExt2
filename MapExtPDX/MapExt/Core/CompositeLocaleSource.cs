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
    /// <para><b>为什么需要合并（不是为了整洁，是为了避开一个引擎竞态）</b>：
    /// 当游戏当前活动语言非 en-US 且 <c>localeId</c> 等于 fallback 语言（en-US）时，
    /// <c>LocalizationManager.AddSourceInternal</c> 会接着调用 <c>AddMissingEntriesFromFallback</c> → <c>LocalizationDictionary.MergeFrom</c>，
    /// 而后者用 <c>foreach</c> 遍历<b>整个</b> fallback 字典（游戏本体上万条 + 其他 Mod 的条目）。
    /// <c>LocalizationDictionary</c> 内部是裸 <c>Dictionary</c>，<c>LocalizationManager</c> 全类
    /// 没有任何锁：遍历期间只要有别的执行流写入该字典，就会抛
    /// <c>InvalidOperationException: Collection was modified</c>。</para>
    ///
    /// <para>一旦该异常从 <c>Mod.OnLoad</c> 的首次 <c>AddSource</c> 逸出，会被
    /// <c>ModManager.InitializeMods</c> 的 try-catch 吞掉（只写日志、不弹窗），
    /// 导致全部 patchset、SystemReplacer 与 UI binding 均未执行，
    /// 玩家只会看到卡顿与缩放失效，无从得知 Mod 已完全未生效。</para>
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
