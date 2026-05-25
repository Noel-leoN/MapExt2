using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SimpleRadio.Core
{
    /// <summary>
    /// 轻量级 OGG Vorbis Comment 解析器。
    /// 零依赖实现，仅提取 TITLE、ARTIST、ALBUM 三个标准字段。
    /// 
    /// 参考规范: https://xiph.org/vorbis/doc/v-comment.html
    /// 
    /// OGG 文件结构简述:
    ///   Page 1 → Vorbis Identification Header ("\x01vorbis")
    ///   Page 2 → Vorbis Comment Header ("\x03vorbis")
    ///              - vendor string length (LE uint32) + vendor string
    ///              - comment count (LE uint32)
    ///              - 逐条 comment: length (LE uint32) + "KEY=VALUE" (UTF-8)
    /// </summary>
    public static class VorbisCommentReader
    {
        /// <summary>
        /// 解析结果容器。
        /// </summary>
        public sealed class VorbisMetadata
        {
            public string Title { get; set; }
            public string Artist { get; set; }
            public string Album { get; set; }
        }

        // OGG 页面头标识: "OggS"
        private static readonly byte[] OggMagic = { 0x4F, 0x67, 0x67, 0x53 };

        // Vorbis Comment Header 标识: "\x03vorbis"
        private static readonly byte[] VorbisCommentMagic = { 0x03, 0x76, 0x6F, 0x72, 0x62, 0x69, 0x73 };

        /// <summary>
        /// 从 OGG 流中解析 Vorbis Comment，提取 TITLE/ARTIST/ALBUM。
        /// 解析失败返回 null（不抛异常）。
        /// </summary>
        public static VorbisMetadata Parse(Stream stream)
        {
            if (stream == null || !stream.CanRead) return null;

            try
            {
                using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

                // 跳过所有 OGG 页面直到找到 Vorbis Comment Header
                byte[] commentData = FindVorbisCommentPage(reader);
                if (commentData == null) return null;

                return ParseCommentBlock(commentData);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 遍历 OGG 页面，找到包含 Vorbis Comment Header 的页面并返回其载荷。
        /// </summary>
        private static byte[] FindVorbisCommentPage(BinaryReader reader)
        {
            // 最多搜索前 10 个页面（Comment Header 通常在第 2 页）
            for (int pageIdx = 0; pageIdx < 10; pageIdx++)
            {
                // --- 读取 OGG 页面头 ---
                byte[] magic = reader.ReadBytes(4);
                if (!BytesEqual(magic, OggMagic)) return null;

                // 跳过: version(1) + header_type(1) + granule_position(8) + 
                //        serial(4) + page_sequence(4) + checksum(4)
                reader.ReadBytes(22);

                // segment_count
                byte segmentCount = reader.ReadByte();

                // segment_table (每个 segment 的字节长度)
                byte[] segmentTable = reader.ReadBytes(segmentCount);

                // 计算总载荷长度
                int totalPayload = 0;
                foreach (byte s in segmentTable)
                {
                    totalPayload += s;
                }

                // 读取载荷
                byte[] payload = reader.ReadBytes(totalPayload);

                // 检查载荷是否以 "\x03vorbis" 开头
                if (payload.Length >= VorbisCommentMagic.Length &&
                    BytesStartsWith(payload, VorbisCommentMagic))
                {
                    return payload;
                }
            }

            return null;
        }

        /// <summary>
        /// 解析 Vorbis Comment 数据块。
        /// </summary>
        private static VorbisMetadata ParseCommentBlock(byte[] data)
        {
            // 跳过 "\x03vorbis" 标识 (7 字节)
            int offset = VorbisCommentMagic.Length;

            if (data.Length < offset + 4) return null;

            // --- 读取 vendor string ---
            uint vendorLength = ReadUInt32LE(data, offset);
            offset += 4;

            if (data.Length < offset + (int)vendorLength + 4) return null;
            offset += (int)vendorLength; // 跳过 vendor string 内容

            // --- 读取 comment count ---
            uint commentCount = ReadUInt32LE(data, offset);
            offset += 4;

            // --- 逐条解析 comment ---
            var result = new VorbisMetadata();

            for (uint i = 0; i < commentCount && offset + 4 <= data.Length; i++)
            {
                uint commentLength = ReadUInt32LE(data, offset);
                offset += 4;

                if (offset + (int)commentLength > data.Length) break;

                string comment = Encoding.UTF8.GetString(data, offset, (int)commentLength);
                offset += (int)commentLength;

                // 解析 "KEY=VALUE" 格式
                int eqIndex = comment.IndexOf('=');
                if (eqIndex <= 0) continue;

                string key = comment.Substring(0, eqIndex).ToUpperInvariant();
                string value = comment.Substring(eqIndex + 1);

                switch (key)
                {
                    case "TITLE":
                        result.Title = value;
                        break;
                    case "ARTIST":
                        result.Artist = value;
                        break;
                    case "ALBUM":
                        result.Album = value;
                        break;
                }

                // 三个字段都找到了就提前退出
                if (result.Title != null && result.Artist != null && result.Album != null)
                    break;
            }

            // 至少有一个字段被解析到才返回
            if (result.Title == null && result.Artist == null && result.Album == null)
                return null;

            return result;
        }

        #region Helpers

        private static uint ReadUInt32LE(byte[] data, int offset)
        {
            return (uint)(data[offset]
                | (data[offset + 1] << 8)
                | (data[offset + 2] << 16)
                | (data[offset + 3] << 24));
        }

        private static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }

        private static bool BytesStartsWith(byte[] data, byte[] prefix)
        {
            if (data.Length < prefix.Length) return false;
            for (int i = 0; i < prefix.Length; i++)
            {
                if (data[i] != prefix[i]) return false;
            }
            return true;
        }

        #endregion
    }
}
