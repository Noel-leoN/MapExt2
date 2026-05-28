using System;
using System.Collections.Generic;
using System.IO;
using Colossal.IO.AssetDatabase;
using static Colossal.IO.AssetDatabase.AudioAsset;

namespace SimpleRadio.Core
{
    /// <summary>
    /// AudioAsset 加载辅助类：将音频文件注册到 AssetDatabase 并注入元数据。
    /// 支持 OGG、MP3、WAV 格式。
    /// </summary>
    public static class AudioAssetHelper
    {
        // FieldInfo 缓存（避免每次反射查找）
        private static readonly System.Reflection.FieldInfo MetatagsField =
            typeof(AudioAsset).GetField("m_Metatags",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        /// <summary>
        /// 加载音频文件并注册到 AssetDatabase.user，注入元数据。
        /// 若 asset 已注册则跳过注册步骤，但仍会强制设置元数据。
        /// </summary>
        public static AudioAsset LoadAndRegister(string audioFilePath, string stationName, string networkName)
        {
            try
            {
                string normalizedPath = audioFilePath.Replace("\\", "/");
                var fileInfo = new FileInfo(normalizedPath);

                AudioAsset audioAsset;

                // --- 幂等性保护：已注册则复用 ---
                if (AssetDatabase.global.TryGetAsset<AudioAsset>(
                    SearchFilter<AudioAsset>.ByCondition(a => a.path == normalizedPath),
                    out var existing))
                {
                    audioAsset = existing;
                }
                else
                {
                    // 注册到 user AssetDatabase（触发 PostCreate → ATL 解析元数据）
                    // 关键1：必须使用 hasExtension: true 让 AssetDataPath 正确提取扩展名
                    //        否则 m_Extension 为 null → GetAssetType(null) → ArgumentNullException
                    // 关键2：subPath 必须使用正斜杠，与 FileSystemDataSource 内部一致
                    //        否则 ConstructFullPath 的 StartsWith 根路径对比会失败
                    string subPath = fileInfo.DirectoryName.Replace("\\", "/");
                    // 关键3：扩展名必须小写，DefaultAssetFactory.GetAssetType 区分大小写
                    string fileName = Path.GetFileNameWithoutExtension(fileInfo.Name)
                                    + Path.GetExtension(fileInfo.Name).ToLowerInvariant();
                    var assetPath = AssetDataPath.Create(subPath, fileName,
                        hasExtension: true, EscapeStrategy.None);
                    var assetData = AssetDatabase.user.AddAsset(assetPath);

                    if (assetData is not AudioAsset newAsset)
                    {
                        Mod.Logger.Warn($"文件未被识别为 AudioAsset: {audioFilePath}");
                        return null;
                    }
                    audioAsset = newAsset;
                }

                // --- 每次都强制设置元数据 ---
                // ATL 解析标准字段（Title/Artist/Album），但不会设置 Type="Music"
                // 而 RadioUISystem.GetClipInfo 依赖 Type=="Music" 才显示歌曲标题
                ForceSetMetadata(audioAsset, audioFilePath, stationName, networkName);

                return audioAsset;
            }
            catch (Exception e)
            {
                Mod.Logger.Error(e, $"加载音频失败: {audioFilePath}");
                return null;
            }
        }

        /// <summary>
        /// 强制设置 AudioAsset 的 m_Metatags 字典和搜索标签。
        ///
        /// ATL 解析的标准 Vorbis Comment（TITLE/ARTIST/ALBUM）作为优先来源，
        /// 文件名作为 Title 的回退值。Type 强制设为 "Music"。
        /// </summary>
        private static void ForceSetMetadata(AudioAsset audioAsset, string filePath, string stationName, string networkName)
        {
            // --- 读取 ATL 已解析的值作为优先来源 ---
            string atlTitle = null, atlArtist = null, atlAlbum = null;
            try
            {
                var existing = audioAsset.metaTags;
                if (existing != null)
                {
                    existing.TryGetValue(Metatag.Title, out atlTitle);
                    existing.TryGetValue(Metatag.Artist, out atlArtist);
                    existing.TryGetValue(Metatag.Album, out atlAlbum);
                }
            }
            catch { /* 读取失败无影响，使用回退值 */ }

            string title = !string.IsNullOrEmpty(atlTitle) ? atlTitle : Path.GetFileNameWithoutExtension(filePath);
            string artist = !string.IsNullOrEmpty(atlArtist) ? atlArtist : "";
            string album = !string.IsNullOrEmpty(atlAlbum) ? atlAlbum : stationName;

            // --- 构建完整字典并写入私有字段 ---
            var metatags = new Dictionary<Metatag, string>
            {
                [Metatag.Title] = title,
                [Metatag.Artist] = artist,
                [Metatag.Album] = album,
                [Metatag.Type] = "Music",
                [Metatag.RadioStation] = networkName,
                [Metatag.RadioChannel] = stationName
            };

            MetatagsField?.SetValue(audioAsset, metatags);

            // --- 搜索标签（供 GetSegmentAudioClip tag-based 查找使用） ---
            audioAsset.AddTag(title);
            if (!string.IsNullOrEmpty(artist)) audioAsset.AddTag(artist);
            audioAsset.AddTag("Music");
            audioAsset.AddTag("type:Music");
            audioAsset.AddTag($"radio channel:{stationName}");
            audioAsset.AddTag($"radio station:{networkName}");
        }
    }
}
