# 🌍 MapExt - Cities: Skylines 2 Mod Suite

Welcome to the **MapExt** mod. This repository contains a suite of advanced simulation and map expansion mods for *Cities: Skylines 2*, specifically engineered for mega-cities and performance enthusiasts.

## 📦 Project Structure

This workspace is divided into two main components:

### 1. [MapExtPDX](./MapExtPDX) (Main Mod)

The core mod responsible for map size expansion (up to 114km) and foundational simulation overrides.

- **Key Features**: 
  - Map size scaling (28km / 57km / 114km) and terrain handling.
  - Integrated performance tweaks.
  - **In-Game Dashboard**: Features real-time city statistics monitoring, ECS system performance diagnostics, and dynamic tuning of Rent Formula parameters.
- **Documentation**: [MapExtPDX/README.md](./MapExtPDX/README.md)

### 2. [EconomyEX](./EconomyEX) (Sub-Mod)

An advanced economy and performance optimization module, extracted from MapExt for standalone use on standard maps.

- **Key Features**: Proportional demand calculation, job-seeking CPU optimization, and fair rent systems.
- **Compatibility**: Automatically yields to MapExtPDX when both are present to prevent conflicts.
- **Documentation**: [EconomyEX/README.md](./EconomyEX/README.md)

### 3. [SimpleRadio](./SimpleRadio) (Sub-Mod)

A custom radio mod allowing players to easily play custom music stations by dropping audio files into a folder.

- **Key Features**: Zero-configuration, OGG/MP3/WAV format support, automatic metadata reading, game settings panel integration, station hot-reloads, and compatibility with ExtendedRadio.
- **Documentation**: [SimpleRadio/README.md](./SimpleRadio/README.md)

### 4. [SimpleBrush](./SimpleBrush) (Sub-Mod)

A lightweight resource brush tool that unlocks hidden natural resource brushes and lets you restore depleted resources with one click.

- **Key Features**: 
  - Reveals hidden Ore, Oil, Fertile Land, and Ground Water brushes in the Terraforming toolbar.
  - Works in both Game and Editor modes with zero Harmony patches.
  - One-click buttons to restore Fertility, Ore, Oil, Fish, or all resources to full capacity in Mod Settings.
- **Compatibility**: Fully compatible. When MapExt2 is active, resource operations are transparently redirected to the extended CellMap buffer.
- **Documentation**: [SimpleBrush/README.md](./SimpleBrush/README.md)

---

## 🔗 Community and Links

- [Cities: Skylines Modding Discord](https://discord.com/channels/1024242828114673724/1366810268331540671)

---
---

# 🌍 MapExt - Cities: Skylines 2 模组套件

欢迎使用 **MapExt** 模组。本仓库包含了专为特大城市和追求极致性能的玩家打造的《都市：天际线 2》（Cities: Skylines 2）高级模拟与地图扩展模组套件。

## 📦 项目结构

本工作区分为两个主要组件：

### 1. [MapExtPDX](./MapExtPDX) (主模组)

负责地图尺寸扩展（最大支持 114km）和基础模拟系统重写的核心模组。

- **核心功能**：
  - 地图尺寸扩展（支持 28km / 57km / 114km 等模式）与地形处理。
  - 集成性能优化。
  - **游戏内控制面板 (In-Game Dashboard)**：包含实时城市统计数据监控、ECS 系统性能诊断，以及扩展的租金公式 (Rent Formula) 参数动态调节功能。
- **文档**：[MapExtPDX/README.md](./MapExtPDX/README.md)

### 2. [EconomyEX](./EconomyEX) (子模组)

从 MapExt 核心中提取的高级经济与性能优化模块，专为标准尺寸地图的独立使用而设计。

- **核心功能**：比例需求计算、求职逻辑 CPU 性能优化，以及公平租金系统。
- **兼容性**：当与 MapExtPDX 同时启用时，会自动让权以防止系统冲突。
- **文档**：[EconomyEX/README.md](./EconomyEX/README.md)

### 3. [SimpleRadio](./SimpleRadio) (子模组)

极简自定义音乐电台模组，玩家只需将音频文件拖入文件夹即可轻松在游戏内创建和播放自定义音乐电台。

- **核心功能**：零配置开箱即用、支持 OGG/MP3/WAV 格式、自动读取音乐元数据（歌曲名和艺术家）、记住上次播放状态、支持游戏内设置面板热刷新，并与 ExtendedRadio 完全兼容。
- **文档**：[SimpleRadio/README.md](./SimpleRadio/README.md)

### 4. [SimpleBrush](./SimpleBrush) (子模组)

极简自然资源笔刷与恢复工具，解锁隐藏资源笔刷并支持一键恢复已枯竭的自然资源。

- **核心功能**：
  - 在地形工具栏中解锁隐藏的矿石、石油、肥沃土地和地下水笔刷。
  - 零 Harmony 补丁，纯原生 API 实现，同时支持游戏模式与编辑器模式。
  - 游戏内设置面板提供一键重置功能，可单独或全部恢复肥沃度、矿石、石油、鱼类资源至满额。
- **兼容性**：完全兼容。当 MapExt2 激活时，资源修改操作会自动重定向并应用到扩展的 CellMap 缓冲区。
- **文档**：[SimpleBrush/README.md](./SimpleBrush/README.md)

---

## 🔗 社区与链接

- [Cities: Skylines Modding Discord](https://discord.com/channels/1024242828114673724/1366810268331540671)
