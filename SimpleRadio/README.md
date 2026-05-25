# SimpleRadio - Simple Custom Music Player

Drop your `.ogg` music files into a folder and they appear as a radio station in-game. No JSON configs, no nested directories, no hassle.

## Features

- **Zero Configuration**: Just create a folder and drop `.ogg` files
- **Auto Metadata**: Reads song title and artist from OGG Vorbis Comments
- **Remember Last Station**: Automatically resumes the last played station when re-entering the game
- **Custom Icons**: Place `icon.svg` in any station folder, or use the built-in icon library
- **Hot Reload**: Refresh stations from the settings panel without restarting the game
- **Lightweight**: Zero runtime overhead during playback
- **Compatible**: Works alongside ExtendedRadio without conflicts (but make sure not to use the same station names)

## How to Use

1. Navigate to your game's user data folder:
   `%LOCALAPPDATA%Low\Colossal Order\Cities Skylines II\ModsData\SimpleRadio\`
2. Create a subfolder - the folder name becomes the station name
3. Place your `.ogg` audio files inside
4. Start or restart the game

> **Tip**: You can also click **"Open Data Folder"** in the settings panel to quickly navigate to the data directory.

### Example Structure

```
ModsData/SimpleRadio/
├── My Rock Station/
│   ├── track01.ogg
│   ├── track02.ogg
│   └── icon.svg          ← Optional custom icon
└── Chill Vibes/
    ├── lofi_beat_1.ogg
    └── lofi_beat_2.ogg
```

## Icons

Stations are assigned icons in the following priority:

1. **Custom icon**: Place an `icon.svg` file in the station folder
2. **Built-in icon library**: If `Resources/StationIcons/station_XX.svg` files exist in the mod directory, they are assigned deterministically by station name
3. **Default icon**: Falls back to the default SimpleRadio icon

## Supported Formats

- **OGG Vorbis** (`.ogg`) - the native game audio format

## In-Game Settings

Open **Options > SimpleRadio** to access:

- Number of loaded stations and songs
- Data folder path
- **Open Data Folder** - opens the data directory in Explorer
- **Refresh Stations** - hot reload without restarting (available after entering a map)

## Compatibility

| Mod            | Status                                         |
| -------------- | ---------------------------------------------- |
| ExtendedRadio  | ✅ Fully compatible (independent network keys) |
| All other mods | ✅ No known conflicts                          |

## Requirements

- No additional dependencies

## Credits

- **Author**: Noel2
- **License**: MIT

---

# 极简自定义音乐播放器

将 `.ogg` 音乐文件放入文件夹即可在游戏中显示为电台。无需 JSON 配置，无需嵌套目录，开箱即用。

## 功能

- **零配置**：创建文件夹并放入 `.ogg` 文件即可
- **自动元数据**：自动读取 OGG 文件中的歌曲名和艺术家信息
- **记住上次电台**：再次进入游戏时自动恢复上次播放的电台
- **自定义图标**：在电台文件夹中放置 `icon.svg` 自定义图标，或使用内置图标库
- **热刷新**：在设置面板中刷新电台，无需重启游戏
- **轻量级**：播放时零运行时开销
- **兼容性**：与 ExtendedRadio 兼容，互不冲突(但注意不要使用同样的电台名称)

## 使用方法

1. 打开游戏用户数据目录：
   `%LOCALAPPDATA%Low\Colossal Order\Cities Skylines II\ModsData\SimpleRadio\`
2. 创建子文件夹——文件夹名即为电台名
3. 将 `.ogg` 音频文件放入其中
4. 启动或重启游戏

> **提示**：也可以在设置面板中点击**"打开数据目录"**快速定位。

### 目录结构示例

```
ModsData/SimpleRadio/
├── 流行音乐/
│   ├── 歌曲1.ogg
│   ├── 歌曲2.ogg
│   └── icon.svg          ← 可选自定义图标
└── 古典音乐/
    ├── lofi_1.ogg
    └── lofi_2.ogg
```

## 图标

电台图标按以下优先级分配：

1. **自定义图标**：在电台文件夹中放置 `icon.svg`
2. **内置图标库**：若 mod 目录中存在 `Resources/StationIcons/station_XX.svg` 预设图标，将按电台名随机分配
3. **默认图标**：兜底使用默认 SimpleRadio 图标

## 支持格式

- **OGG Vorbis** (`.ogg`) - 游戏原生音频格式

## 游戏内设置

打开 **选项 > SimpleRadio** 可查看：

- 已加载的电台和歌曲数量
- 数据目录路径
- **打开数据目录** - 在资源管理器中打开数据目录
- **刷新电台** - 热刷新电台，无需重启（进入存档后可用）

## 兼容性

| Mod           | 状态                          |
| ------------- | ----------------------------- |
| ExtendedRadio | ✅ 完全兼容（独立的网络键名） |
| 其他 mod      | ✅ 无已知冲突                 |

## 系统要求

- 无额外依赖

## 致谢

- **作者**: Noel2
- **许可**: MIT
