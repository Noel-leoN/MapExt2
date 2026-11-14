# MapExTools — Companion Desktop Utility for MapExt (Cities: Skylines II)

**English** | **[简体中文](#-mapextools--mapext-都市天际线-2桌面配套工具)**

`MapExTools` is a standalone Windows desktop app (`WPF`, `.NET 11`) distributed exclusively via GitHub Releases (not bundled inside the Paradox Mods package). It combines four tools for **Map Extended (MapExt)** players and map creators:

1. **Launch and Startup Settings:** Switch MapExt's map size mode (`57km` / `43km` / `28km` / `114km` / vanilla `14km`), toggle the 6 economy system groups, and launch `Cities2.exe` straight to the Main Menu while skipping the Paradox Launcher — no Steam `%command%` setup required.
2. **Log Diagnostics:** One-screen session summary (`MapExtPDX.log` active mode/CoreValue, Burst compiler state, conflict auto-disables, save entry gate results, `SceneFlow.log` command line, and `Player.log` errors/exit state), live tailing viewer, and one-click Markdown diagnostic report copy.
3. **Saves and Maps Inspector (`.cok`):** Read-only scan of local `.cok` saves and maps with ZSTD preview rendering, localized map titles, mode compatibility status (`Match` / `Convertible` / `Stretches` / `Mismatch`), and one-click mode switching before starting the game.
4. **16-bit Heightmap Viewer:** Inspect `4096x4096` 16-bit grayscale/RGBA `.png` and `.tif`/`.tiff` heightmaps before importing into the Map Editor, with header format validation, raw grayscale, 0.5%–99.5% auto-stretched contrast, sea-level layered hypsometric tinting, zoom/pan, and per-pixel elevation readout.

> **Compatible Mod Version:** MapExTools v1.0 is built for **MapExt v4.12.0 and later** (sharing its startup keys, save conversion rules, and session Burst status log line). If you use MapExTools before MapExt v4.12.0 is live on Paradox Mods, features introduced in v4.12.0 (such as save conversion status and the Burst log line) will only take effect once your MapExt installation updates to v4.12.0+.

---

## 📦 Choosing a Download Package

Download from [GitHub Releases](https://github.com/Noel-leoN/MapExt2/releases) (look for tags prefixed with `mapextools-v*`):

| Package | Who Should Pick It | Requirements |
|:---|:---|:---|
| **`MapExTools-v1.0.0-win-x64-portable.zip` (Recommended)** | Most players — works out of the box with zero runtime installation | Windows 10 / 11 (64-bit) |
| **`MapExTools-v1.0.0-win-x64-lite.zip`** | Smaller download if you already have the .NET 11 Desktop Runtime installed | Windows 10 / 11 (64-bit) + [.NET 11 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/11.0) |

Both archives contain a **single `MapExTools.exe` file** — no installer, no registry entries, no background services, and no administrator privileges required.

---

## 🚀 Getting Started in 3 Steps

1. **Extract to a dedicated folder** (for example, `D:\Tools\MapExTools\`).
   * **Do NOT place `MapExTools.exe` or its `.zip` archive inside `{UserData}\Mods\`** (`%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Mods`). The game scans `Mods\` for `.dll`, `.zip`, and `.json` assets; if launched from `Mods\`, MapExTools will prompt you to move it out.
2. **First launch (Windows SmartScreen):** Because the executable is not code-signed, Windows Defender SmartScreen may show *"Windows protected your PC"* on first run. Click **More info** -> **Run anyway**.
3. **Configure and launch to Main Menu:**
   * Keep **Steam running in the background**, pick your desired MapExt mode and launch options, and click **Launch game**.
   * **Direct launch (`Cities2.exe`, default)** skips the Paradox Launcher without needing `%command%` in Steam, and lands on the game's Main Menu so MapExt's menu save gate checks every load safely.
   * **Note:** When using Direct launch, **launch options configured inside the Steam client do not apply** — select your desired flags (`--developerMode`, `--noSplash`, etc.) directly inside MapExTools. Two fallback channels (`Steam -applaunch` and `runOnce.txt`) are also available.

### Non-Steam Builds (Xbox / Microsoft Store / Game Pass)

If the game cannot be located automatically via Steam, click **Choose Cities2.exe...** on the Launch card and select your game executable manually (for example, `C:\XboxGames\Cities Skylines II\Content\Cities2.exe`). Non-Steam builds use the **Direct launch** channel only. Even without a game executable configured, `.coc` settings editing, log diagnostics, `.cok` save/map inspection, and heightmap viewing all work normally.

---

## 📌 What "Pin to Local Folder" Does

If you double-click `MapExTools.exe` directly inside a `.zip` viewer (`%TEMP%`) or run it from your `Downloads` folder, a banner appears at the top offering **Pin to local folder and create desktop shortcut**.

* **User-initiated only:** Nothing is copied or created unless you click the button and confirm. It never runs silently, never hides its window, and never adds startup entries.
* **Exact actions when clicked:**
  1. Copies the single-file `MapExTools.exe` to `%LOCALAPPDATA%\Programs\MapExTools\MapExTools.exe` (standard user folder, no admin prompt);
  2. Creates `MapExTools.lnk` on your Desktop pointing to that path;
  3. Relaunches `MapExTools.exe` from the new folder and closes the temporary instance.

### Updating and Uninstalling

* **Updating:** Tool preferences (language, theme, game path, launch flags) are stored separately at `%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\ModsData\MapExt2\MapExTools.json`, and every edit to `MapExtPDX.coc` writes a `MapExtPDX.coc.bak` backup first. Overwriting `MapExTools.exe` (or clicking **Pin to local folder** from a newer download) preserves all settings.
* **Uninstalling:** Delete the folder containing `MapExTools.exe` (e.g., `%LOCALAPPDATA%\Programs\MapExTools\`), delete `MapExTools.lnk` on your Desktop, and optionally delete `%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\ModsData\MapExt2\MapExTools.json`.

---

## ⌨️ Command-Line Usage and Exit Codes

`MapExTools.exe` can be driven from scripts or custom shortcuts without opening the GUI whenever a setting, `--launch`, `--show`, or `--help` flag is passed:

```text
MapExt startup settings (writes MapExtPDX.coc; refused while the game is running):
  --mode <ModeA|ModeB|ModeC|ModeD|None>      Map size mode
  --economy-fix <on|off>                     Economy Fix and Performance (master toggle)
  --demand <on|off>                          RCI Demand System Group
  --job-search <on|off>                      Job Search System Group
  --household-property <on|off>              Household, Property and Rent System Group
  --resource-buyer <on|off>                  Resource Buyer and Service Coverage System Group
  --resident-ai <on|off>                     Resident AI Pathing Patch
  --downstream-ai <on|off>                   Vehicle AI and Pathing System Group (Beta)
  --disable-load-game-validation <on|off>    Disable save validation on load
  --reset                                    Reset all 9 startup keys to defaults (can combine with overrides)

Other options:
  --launch                                   Launch the game using the channel and flags saved in MapExTools.json
  --show                                     Print current settings, last applied session config, and launch preview
  --lang <en-US|zh-HANS|zh-HANT>             Output or UI language for this run
  --help                                     Show command-line usage
```

**Exit codes:** `0` = success · `1` = invalid arguments, malformed `.coc`, or launch failure · `2` = game is currently running (no changes made).

> **Important for PowerShell and `cmd`:** Because `MapExTools.exe` is a Windows GUI executable (`WinExe`), an interactive shell returns immediately without waiting for it to finish, so `$LASTEXITCODE` / `%ERRORLEVEL%` will not reflect its exit code unless you wait explicitly:
>
> ```powershell
> # PowerShell — wait and read ExitCode:
> $p = Start-Process -FilePath .\MapExTools.exe -ArgumentList '--mode', 'ModeA', '--launch' -Wait -PassThru
> $p.ExitCode
>
> # Or pipe output (automatically waits for stdout to close):
> .\MapExTools.exe --show --lang en-US | Out-String
> ```
>
> ```cmd
> :: Command Prompt (cmd.exe):
> start /wait "" MapExTools.exe --mode ModeA --launch
> echo %ERRORLEVEL%
> ```

---

## 🔒 Source Code, Reproducible Build, and SHA-256

* **Source directory:** [`MapExTools/`](https://github.com/Noel-leoN/MapExt2/tree/master/MapExTools) in the [Noel-leoN/MapExt2](https://github.com/Noel-leoN/MapExt2) repository.
* **Verify SHA-256:** Compare your downloaded `.zip` against `MapExTools-v1.0.0-SHA256SUMS.txt` attached to the GitHub Release:
  ```powershell
  Get-FileHash .\MapExTools-v1.0.0-win-x64-portable.zip -Algorithm SHA256
  ```
* **Build from source** (requires .NET 11 SDK on Windows):
  ```powershell
  pwsh -NoProfile -File MapExTools/build/Publish-MapExTools.ps1
  ```

---

# 🧰 MapExTools — MapExt（都市：天际线 2）桌面配套工具

`MapExTools` 是在 GitHub Releases 单独发布的 Windows 桌面配套程序（`WPF` + `.NET 11`，不随 Paradox Mods 模组包分发），集成了 **MapExt** 常用的四项外部能力：

1. **启动与设定**：在游戏关闭时修改 `MapExtPDX.coc` 的 9 个启动期设定（地图尺寸模式 `57km` / `43km` / `28km` / `114km` / 原版 `14km`、经济总开关与 6 个子系统分组、读档校验开关），并可**跳过 Paradox 启动器直接启动 `Cities2.exe` 进入游戏主菜单**（无需在 Steam 中配置 `%command%`）。
2. **日志诊断**：单屏汇总上次会话摘要（`MapExtPDX.log` 实际生效配置与 CoreValue、本会话 Burst 编译状态、冲突自动停用记录、存档入口判定，`SceneFlow.log` 启动参数，`Player.log` 异常与退出状态），支持实时跟随浏览、按级别/关键词过滤及一键复制脱敏 Markdown 诊断报告。
3. **存档与地图检视（`.cok`）**：只读扫描本地存档与地图包，解码 ZSTD 预览图与多语言 `.loc` 地图标题，按闸门规则显示与当前设定模式的关系（`匹配` / `可转换` / `开启转换后可转换` / `会拉伸` / `不符`），支持一键将设定切到对应模式后再进游戏主菜单载入。
4. **高度图查看**：在导入地图编辑器前检视 `4096x4096` 16 位灰度 / RGBA `.png`、`.tif`/`.tiff` 高度图，按文件头校验能否被游戏导入，提供原始灰度、0.5%–99.5% 分位自动拉伸对比度、按海平面分层着色（含海岸线描边与图例）三种视图，支持缩放平移与悬停坐标/海拔读数。

> **配套的 Mod 版本**：MapExTools v1.0 按 **MapExt v4.12.0** 的设定键与存档判定规则（如“可转换”判定、本会话 Burst 状态日志）编写，请配合 **MapExt v4.12.0 及以上版本**使用。若本工具先于 v4.12.0 在 GitHub 发布，PDX 上的旧版 MapExt 尚无存档转换与 Burst 状态日志行，相关提示需待 MapExt 升级至 v4.12.0 后一并生效。

---

## 📦 两个压缩包怎么选

请前往仓库的 [GitHub Releases](https://github.com/Noel-leoN/MapExt2/releases) 页面下载（认准 `mapextools-v*` 标签）：

| 发布包 | 适用场景 | 运行环境要求 |
|:---|:---|:---|
| **`MapExTools-v1.0.0-win-x64-portable.zip`（推荐）** | 开箱即用，自带运行时，无需额外安装任何组件 | Windows 10 / 11（64 位） |
| **`MapExTools-v1.0.0-win-x64-lite.zip`** | 体积更小，适合本机已安装 .NET 11 桌面运行时的玩家 | Windows 10 / 11（64 位）+ [.NET 11 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/11.0) |

两个压缩包解压后都**只有一个 `MapExTools.exe` 单文件**——绿色免安装、不写注册表、不装后台服务、不需要管理员权限。

---

## 🚀 三步上手

1. **解压到独立的常规目录**（例如 `D:\Tools\MapExTools\`）。
   * **请勿放入游戏的 `Mods\` 目录**（`%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Mods`）。游戏启动时会把 `Mods\` 下的 `.dll`、`.zip`、`.json` 当作模组或资产扫描；若在 `Mods\` 下运行，工具会弹窗要求移出。
2. **首次打开遇到 SmartScreen 提示**：因程序未购买商业代码签名证书，Windows SmartScreen 首次可能弹出“Windows 已保护你的电脑”，点击 **“更多信息” -> “仍要运行”** 即可。
3. **改好设定并直启进主菜单**：
   * 保持 **Steam 在后台运行**，在“启动与设定”页选好模式与参数后点击 **“启动游戏”**。
   * 默认的**直接启动 `Cities2.exe`** 通道会自动跳过 Paradox 启动器直达游戏主菜单，**不必在 Steam 里设置 `%command%`**；进入主菜单后再读档或点“继续”，可完整经过 MapExt 的读档校验闸门。
   * **注意**：直启时，**您在 Steam 客户端“启动选项”里填写的参数不会生效**，请直接在工具的“启动游戏”卡片中勾选所需选项（同时提供经 Steam `-applaunch` 与经 `runOnce.txt` 两条备用通道）。

### 非 Steam 版本（Xbox / Microsoft Store / Game Pass）

若工具未能通过 Steam 自动找到游戏，请在“启动游戏”卡片点击 **“选择 Cities2.exe...”** 手动指定游戏程序路径（例如 `C:\XboxGames\Cities Skylines II\Content\Cities2.exe`）；非 Steam 版仅支持“直接启动 Cities2.exe”通道。即使未指定游戏路径，MapExt 设定修改、日志诊断、存档与地图检视、高度图查看四个模块仍可照常使用。

---

## 📌 “固定到本地”具体做了什么

如果您在压缩软件里直接双击打开（位于 `%TEMP%` 临时目录），或直接在系统的“下载”文件夹（`Downloads`）里运行，窗口顶部会显示提示横幅，提供 **“固定到本地并创建桌面快捷方式”** 按钮。

* **仅在您点击后执行**：不会自动执行、不会隐藏窗口、不会写注册表或添加开机自启。
* **点击确认后的具体操作**：
  1. 将当前单文件 `MapExTools.exe` 复制到 `%LOCALAPPDATA%\Programs\MapExTools\MapExTools.exe`（当前用户目录，免管理员权限）；
  2. 在桌面创建指向该路径的快捷方式 `MapExTools.lnk`；
  3. 从新位置启动 `MapExTools.exe`，并关闭当前临时窗口。

### 覆盖升级与卸载

* **覆盖升级不丢配置**：工具配置保存在 `%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\ModsData\MapExt2\MapExTools.json`（与 `exe` 位置无关），且每次修改 `MapExtPDX.coc` 前都会自动备份 `MapExtPDX.coc.bak`。升级时直接用新版 `MapExTools.exe` 覆盖旧文件（或在新包里再次点“固定到本地”）不会丢失已有配置。
* **干净卸载**：删除存放 `MapExTools.exe` 的目录（如 `%LOCALAPPDATA%\Programs\MapExTools\`）、桌面快捷方式 `MapExTools.lnk`，以及配置文件 `%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\ModsData\MapExt2\MapExTools.json` 即可。

---

## ⌨️ 命令行用法与退出码

传入任意设定项、`--launch`、`--show` 或 `--help` 时，工具会在命令行中执行完毕并退出，不打开图形窗口：

```text
MapExt 启动设定（写入 MapExtPDX.coc；游戏运行时拒绝写入）：
  --mode <ModeA|ModeB|ModeC|ModeD|None>      地图尺寸模式
  --economy-fix <on|off>                     经济系统修复与性能优化（总开关）
  --demand <on|off>                          RCI需求调节系统组
  --job-search <on|off>                      找工作系统组
  --household-property <on|off>              找房与租金系统组
  --resource-buyer <on|off>                  消费采购与服务覆盖寻路系统组
  --resident-ai <on|off>                     居民AI寻路优化补丁
  --downstream-ai <on|off>                   载具AI与寻路系统组 (Beta)
  --disable-load-game-validation <on|off>    禁止游戏读取存档验证
  --reset                                    将全部 9 个受管键恢复默认值（可与单项覆盖同时传入）

其他选项：
  --launch                                   按 MapExTools.json 中保存的通道与参数启动游戏
  --show                                     输出当前设定、上次启动生效配置与启动命令预览
  --lang <en-US|zh-HANS|zh-HANT>             指定本次运行的输出或界面语言
  --help                                     显示命令行帮助
```

**退出码**：`0` = 成功 · `1` = 参数有误、`.coc` 格式错误或启动失败 · `2` = 游戏正在运行（未做任何改动）。

> **获取退出码说明**：`MapExTools.exe` 是窗口程序（`WinExe`），在交互式 PowerShell 或 `cmd` 中直接输入命令时，外壳不会等待窗口程序结束，因此直接读 `$LASTEXITCODE` 或 `%ERRORLEVEL%` 拿不到退出码。脚本调用请使用 `Start-Process -Wait -PassThru` 或 `start /wait`：
>
> ```powershell
> # PowerShell 等待退出并读取退出码：
> $p = Start-Process -FilePath .\MapExTools.exe -ArgumentList '--mode', 'ModeA', '--launch' -Wait -PassThru
> $p.ExitCode
>
> # 或用管道捕获标准输出（管道会自动等待进程结束）：
> .\MapExTools.exe --show --lang zh-HANS | Out-String
> ```
>
> ```cmd
> :: cmd 命令提示符：
> start /wait "" MapExTools.exe --mode ModeA --launch
> echo %ERRORLEVEL%
> ```

---

## 🔒 源码位置、复现构建与 SHA-256 校验

* **源码目录**：位于公开仓库 [Noel-leoN/MapExt2](https://github.com/Noel-leoN/MapExt2) 的 [`MapExTools/`](https://github.com/Noel-leoN/MapExt2/tree/master/MapExTools) 目录。
* **核对 SHA-256**：下载后可与 Release 附件中的 `MapExTools-v1.0.0-SHA256SUMS.txt` 比对哈希：
  ```powershell
  Get-FileHash .\MapExTools-v1.0.0-win-x64-portable.zip -Algorithm SHA256
  ```
* **从源码构建发布包**（需 Windows 与 .NET 11 SDK）：
  ```powershell
  pwsh -NoProfile -File MapExTools/build/Publish-MapExTools.ps1
  ```
