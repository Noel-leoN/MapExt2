# 🌍 MapExt - Cities: Skylines 2 Mod Suite

Welcome to the **MapExt** mod. This repository contains a suite of advanced simulation and map expansion mods and tools for *Cities: Skylines 2*, specifically engineered for mega-cities and performance enthusiasts.

## 📦 Project Structure

This repository contains the following mods and tools:

### 1. [MapExtPDX](./MapExtPDX) (Main Mod)

The core mod responsible for map size expansion (57km default, 28km, 43km experimental, 114km not recommended, and vanilla 14km) and foundational simulation overrides.

- **Key Features**: 
  - Map size scaling (`57km` / `28km` / `43km` / `114km` / vanilla `14km`) and terrain handling.
  - Integrated performance tweaks and 3-source map extension conversion (vanilla 14km saves, vanilla 14km maps, and 28km saves to 57km).
  - **In-Game Dashboard**: Real-time city statistics monitoring, key economic metrics, and dynamic tuning of rent and pathfinding parameters.
- **Documentation**: [MapExtPDX/README.md](./MapExtPDX/README.md)

### 2. [MapExTools](./MapExTools) (Companion Desktop App — GitHub Releases)

A standalone Windows desktop utility (`WPF`, `.NET 11`) distributed separately via [GitHub Releases](https://github.com/Noel-leoN/MapExt2/releases) (`mapextools-v*` tags).

- **Key Features**:
  - Offline editing of all 65 MapExt OptionUI settings (`MapExtPDX.coc`) and direct game launch skipping the Paradox Launcher.
  - Local playset preview/switching, full mod list browsing, and static metadata/IL bytecode conflict pre-checks.
  - Session log diagnostics, read-only `.cok` save/map inspection, and 16-bit heightmap viewer.
- **Documentation**: [MapExTools/README.md](./MapExTools/README.md)

### 3. [EconomyEX](./EconomyEX) (Sub-Mod — Maintenance Paused)

> **Status:** Updates to standalone `EconomyEX` are currently **paused**. All economy and performance features have been migrated to and are actively maintained in **MapExtPDX** (including the vanilla 14km map mode). New users should use [MapExtPDX](./MapExtPDX) directly.

An advanced economy and performance optimization module previously extracted from MapExt for standalone use on standard maps.

- **Key Features**: Proportional demand calculation, job-seeking CPU optimization, and fair rent systems.
- **Compatibility**: Automatically yields to MapExtPDX when both are present to prevent conflicts.
- **Documentation**: [EconomyEX/README.md](./EconomyEX/README.md)

### 4. [SimpleRadio](./SimpleRadio) (Sub-Mod)

A custom radio mod allowing players to easily play custom music stations by dropping audio files into a folder.

- **Key Features**: Zero-configuration, OGG/MP3/WAV format support, automatic metadata reading, remembers last playback state, game settings panel integration, and station hot-reloads.
- **Documentation**: [SimpleRadio/README.md](./SimpleRadio/README.md)

### 5. [SimpleBrush](./SimpleBrush) (Sub-Mod)

A lightweight resource brush tool that unlocks hidden natural resource brushes and lets you restore depleted resources with one click.

- **Key Features**: 
  - Reveals hidden Ore, Oil, Fertile Land, and Ground Water brushes in the Terraforming toolbar.
  - Works in both Game and Editor modes with zero Harmony patches.
  - One-click buttons to restore Fertility, Ore, Oil, Fish, or all resources to full capacity in Mod Settings.
- **Compatibility**: Fully compatible. When MapExt is active, resource operations are transparently redirected to the extended CellMap buffer.
- **Documentation**: [SimpleBrush/README.md](./SimpleBrush/README.md)

---

## 🔗 Community and Links

- [Cities: Skylines Modding Discord](https://discord.com/channels/1024242828114673724/1366810268331540671)

---

# 🌍 MapExt - Cities: Skylines 2 模组套件

欢迎使用 **MapExt** 模组。本仓库包含了专为特大城市和追求极致性能的玩家打造的《都市：天际线 2》（Cities: Skylines 2）高级模拟与地图扩展模组及配套工具套件。

## 📦 项目结构

本仓库包含以下模组与工具：

### 1. [MapExtPDX](./MapExtPDX)（主模组）

负责地图尺寸扩展（默认 57km，支持 28km、实验性 43km、不推荐的 114km 与原版 14km 模式）和基础模拟系统重写的核心模组。

- **核心功能**：
  - 地图尺寸扩展（`57km` / `28km` / `43km` / `114km` / 原版 `14km`）与地形处理。
  - 集成性能优化与三源地图扩展转换（原版 14km 存档、原版 14km 地图及 28km 存档扩展至 57km）。
  - **游戏内控制面板 (In-Game Dashboard)**：包含实时城市统计数据监控、关键经济指标快查，以及租金与寻路参数动态调节功能。
- **文档**：[MapExtPDX/README.md](./MapExtPDX/README.md)

### 2. [MapExTools](./MapExTools)（桌面配套工具 — GitHub Releases 独立发布）

独立的 Windows 桌面配套程序（`WPF` + `.NET 11`），通过 [GitHub Releases](https://github.com/Noel-leoN/MapExt2/releases)（`mapextools-v*` 标签）单独分发。

- **核心功能**：
  - 离线编辑 MapExt 全部 65 项 OptionUI 设置（`MapExtPDX.coc`），支持跳过 Paradox 启动器直启游戏进主菜单。
  - 本地播放集预览/切换、完整模组列表检视，以及静态元数据与 IL 字节码冲突预检。
  - 会话日志诊断、`.cok` 存档与地图只读检视，以及 16 位高度图查看。
- **文档**：[MapExTools/README.md](./MapExTools/README.md)

### 3. [EconomyEX](./EconomyEX)（子模组 — 暂停更新）

> **当前状态**：独立版 `EconomyEX` 已**暂停更新**，其全部经济与性能优化功能已迁移并全面集成至 **MapExtPDX**（原生支持 14km 原版尺寸模式），推荐直接使用 [MapExtPDX](./MapExtPDX)。

早期从 MapExt 核心中提取的高级经济与性能优化模块，面向标准尺寸地图的独立使用。

- **核心功能**：比例需求计算、求职逻辑 CPU 性能优化，以及公平租金系统。
- **兼容性**：当与 MapExtPDX 同时启用时，会自动让权以防止系统冲突。
- **文档**：[EconomyEX/README.md](./EconomyEX/README.md)

### 4. [SimpleRadio](./SimpleRadio)（子模组）

极简自定义音乐电台模组，玩家只需将音频文件拖入文件夹即可轻松在游戏内创建和播放自定义音乐电台。

- **核心功能**：零配置开箱即用、支持 OGG/MP3/WAV 格式、自动读取音乐元数据（歌曲名和艺术家）、记住上次播放状态、支持游戏内设置面板热刷新。
- **文档**：[SimpleRadio/README.md](./SimpleRadio/README.md)

### 5. [SimpleBrush](./SimpleBrush)（子模组）

极简自然资源笔刷与恢复工具，解锁隐藏资源笔刷并支持一键恢复已枯竭的自然资源。

- **核心功能**：
  - 在地形工具栏中解锁隐藏的矿石、石油、肥沃土地和地下水笔刷。
  - 零 Harmony 补丁，纯原生 API 实现，同时支持游戏模式与编辑器模式。
  - 游戏内设置面板提供一键重置功能，可单独或全部恢复肥沃度、矿石、石油、鱼类资源至满额。
- **兼容性**：完全兼容。当 MapExt 激活时，资源修改操作会自动重定向并应用到扩展的 CellMap 缓冲区。
- **文档**：[SimpleBrush/README.md](./SimpleBrush/README.md)

---

## 🔗 社区与链接

- [Cities: Skylines Modding Discord](https://discord.com/channels/1024242828114673724/1366810268331540671)
