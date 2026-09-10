# SimpleRadio - Simple Custom Music Player

Drop your music files into a folder and they appear as a radio station in-game. No JSON configs, no nested directories, no hassle.

## Features

- **Zero Configuration**: Just create a folder and drop audio files
- **Multi-Format**: Supports OGG, MP3, and WAV with per-format toggles
- **Auto Metadata**: Reads song title and artist from audio file tags
- **Remember Last Station**: Resumes your last used station across cities by default; optionally restore each city's saved station
- **Custom Icons**: Place `icon.svg` in any station folder, or use the built-in icon library
- **Hot Reload**: Refresh stations from the settings panel without restarting the game
- **Lightweight**: Zero runtime overhead during playback
- **Compatible**: Works alongside ExtendedRadio without conflicts (but make sure not to use the same station names)

## How to Use

1. Navigate to your game's user data folder:
   `%LOCALAPPDATA%Low\Colossal Order\Cities Skylines II\ModsData\SimpleRadio\`
2. Create a subfolder - the folder name becomes the station name
3. Place your audio files inside (`.ogg`, `.mp3`, `.wav`)
4. Start or restart the game

> **Tip**: You can also click **"Open Data Folder"** in the settings panel to quickly navigate to the data directory.

### Example Structure

**ModsData/SimpleRadio/**

- **My Rock Station/** — track01.ogg, track02.mp3, icon.svg (optional custom icon)
- **Chill Vibes/** — lofi_beat_1.ogg, lofi_beat_2.wav

## Supported Formats

- ✅ **OGG Vorbis** (.ogg) — Always enabled. Native game format, recommended.
- ✅ **MP3** (.mp3) — Enabled by default. Widely compatible.
- ✅ **WAV** (.wav) — Enabled by default. See warning below.

> **WAV Warning**: WAV files are uncompressed and typically **10x larger** than OGG/MP3 (~30-50 MB per song). This may significantly increase disk usage and cause brief stuttering on HDD when switching tracks. **Recommended: convert WAV to OGG or MP3** for better performance.

Each format can be individually enabled or disabled in **Options > SimpleRadio > Format**.

## Icons

Stations are assigned icons in the following priority:

1. **Custom icon**: Place an `icon.svg` file in the station folder
2. **Built-in icon library**: If `Resources/StationIcons/station_XX.svg` files exist in the mod directory, one is picked at random on every load
3. **Default icon**: Falls back to the default SimpleRadio icon

## In-Game Settings

Open **Options > SimpleRadio** to access:

- Number of loaded stations and songs
- Data folder path
- **Open Data Folder** - opens the data directory in Explorer
- **Refresh Stations** - hot reload without restarting (available after entering a map)
- **Restore Last Used Station** - enabled by default under Info > Playback; disable to restore each city's saved station the next time you enter a city
- **Format toggles** - enable/disable MP3 and WAV support (click **Refresh Stations** to apply)
- **ExtendedRadio status** - compatibility detection

## Compatibility

- **ExtendedRadio** — ✅ Compatible, can be used together (just avoid identical station names)
- **All other mods** — ✅ No known conflicts

SimpleRadio runs fully standalone — its audio format support only applies to its own stations, so it behaves the same whether ExtendedRadio is installed or not. As an extra safeguard, if another radio mod crashes while stations are loading, SimpleRadio catches the error so the game's radio keeps working.

## Requirements

- No additional dependencies

---

# 极简自定义音乐播放器

将音乐文件放入文件夹即可在游戏中显示为电台。无需 JSON 配置，无需嵌套目录，开箱即用。

## 功能

- **零配置**：创建文件夹并放入音频文件即可
- **多格式**：支持 OGG、MP3、WAV，每种格式可独立开关
- **自动元数据**：自动读取音频文件中的歌曲名和艺术家信息
- **记住上次电台**：默认跨城市恢复上次使用的电台，也可改为恢复各城市存档记录的电台
- **自定义图标**：在电台文件夹中放置 `icon.svg` 自定义图标，或使用内置图标库
- **热刷新**：在设置面板中刷新电台，无需重启游戏
- **轻量级**：播放时零运行时开销
- **兼容性**：与 ExtendedRadio 兼容，互不冲突（但注意不要使用同样的电台名称）

## 使用方法

1. 打开游戏用户数据目录：
   `%LOCALAPPDATA%Low\Colossal Order\Cities Skylines II\ModsData\SimpleRadio\`
2. 创建子文件夹——文件夹名即为电台名
3. 将音频文件（`.ogg`、`.mp3`、`.wav`）放入其中
4. 启动或重启游戏

> **提示**：也可以在设置面板中点击**"打开数据目录"**快速定位。

### 目录结构示例

**ModsData/SimpleRadio/**

- **流行音乐/** — 歌曲1.ogg, 歌曲2.mp3, icon.svg（可选自定义图标）
- **古典音乐/** — lofi_1.ogg, lofi_2.wav

## 支持格式

- ✅ **OGG Vorbis** (.ogg) — 始终启用，游戏原生格式，推荐使用。
- ✅ **MP3** (.mp3) — 默认启用，广泛兼容。
- ✅ **WAV** (.wav) — 默认启用，请注意下方提醒。

> **WAV 提醒**：WAV 是无压缩格式，文件体积通常是 OGG/MP3 的 **10 倍**（每首约 30-50 MB）。可能显著增加磁盘占用，且在机械硬盘上切歌时可能出现短暂卡顿。**建议：将 WAV 转换为 OGG 或 MP3** 以获得更好的性能。

每种格式可在 **选项 > SimpleRadio > 格式** 中独立启用或禁用。

## 图标

电台图标按以下优先级分配：

1. **自定义图标**：在电台文件夹中放置 `icon.svg`
2. **内置图标库**：若 mod 目录中存在 `Resources/StationIcons/station_XX.svg` 预设图标，每次加载会随机抽取一张
3. **默认图标**：兜底使用默认 SimpleRadio 图标

## 游戏内设置

打开 **选项 > SimpleRadio** 可查看：

- 已加载的电台和歌曲数量
- 数据目录路径
- **打开数据目录** - 在资源管理器中打开数据目录
- **刷新电台** - 热刷新电台，无需重启（进入存档后可用）
- **恢复上次使用的电台** - 位于“信息 > 播放”，默认开启；关闭后，下次进入城市改用存档记录的电台
- **格式开关** - 启用/禁用 MP3 和 WAV 支持（点击**刷新电台**即可生效）
- **ExtendedRadio 状态** - 兼容性检测

## 兼容性

- **ExtendedRadio** — ✅ 兼容，可同时使用（电台名称避免与其他 Mod 重复即可）
- **其他 Mod** — ✅ 无已知冲突

SimpleRadio 完全独立运行——其音频格式支持仅作用于自身电台，无论是否安装 ExtendedRadio，行为均完全一致。此外还有一层保护：若其他电台 Mod 在加载电台时出错，SimpleRadio 会自动捕获该异常，确保游戏电台功能持续正常运行。

## 系统要求

- 无额外依赖

