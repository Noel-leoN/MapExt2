using System.Reflection;
using System.Reflection.Emit;
using MapExTools.Core.Conflicts;
using MapExTools.Core.Paths;

namespace MapExTools.Tests.Conflicts;

public sealed class PlaysetModScannerTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly UserDataPaths _paths;

    public PlaysetModScannerTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "MapExTools_ScannerTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        _paths = new UserDataPaths(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            RemoveDirectorySafe(_tempRoot);
        }
    }

    [Fact]
    public void Scan_PicksHighestIntegerVersion_AndResolvesDisplayNameFromCacheAndMetadata()
    {
        string pdxRoot = Path.Combine(_paths.ModsCacheDir, "pdx_mods");
        string localRoot = Path.Combine(_paths.ModsCacheDir, "local");
        Directory.CreateDirectory(pdxRoot);
        Directory.CreateDirectory(localRoot);

        // 构造 86944_9 与 86944_10 两个版本目录：字典序 "9" > "10"，整数序 10 > 9
        string v9Dir = Path.Combine(pdxRoot, "86944_9");
        string v10Dir = Path.Combine(pdxRoot, "86944_10");
        Directory.CreateDirectory(v9Dir);
        Directory.CreateDirectory(v10Dir);

        File.WriteAllBytes(Path.Combine(v9Dir, "SampleMod.dll"), BuildSimpleIMod("SampleMod", new Version(1, 9, 0)));
        File.WriteAllBytes(Path.Combine(v10Dir, "SampleMod.dll"), BuildSimpleIMod("SampleMod", new Version(1, 10, 0)));
        // Burst 原生后缀库应被跳过，不进入 UnreadableAssemblies
        File.WriteAllBytes(Path.Combine(v10Dir, "SampleMod_win_x86_64.dll"), [0x4D, 0x5A, 0x00, 0x00]);

        // SDK local 包带 .metadata/metadata.json
        string sdkLocalDir = Path.Combine(localRoot, "MyLocalPack_1");
        Directory.CreateDirectory(Path.Combine(sdkLocalDir, ".metadata"));
        File.WriteAllBytes(Path.Combine(sdkLocalDir, "MyLocalPack.dll"), BuildSimpleIMod("MyLocalPack", new Version(0, 5, 0)));
        File.WriteAllText(Path.Combine(sdkLocalDir, ".metadata", "metadata.json"),
            """{"displayName":"SDK Local Mod","userModVersion":"0.5.0-local"}""");

        // pdx_mods_cache.json 提供订阅包商店显示名
        File.WriteAllText(_paths.PdxModsCacheFile, """
            {
              "items": [
                { "source": "pdx_mods", "id": "86944", "version": "9", "userModVersion": "1.9.0", "displayName": "Store Sample Old" },
                { "source": "pdx_mods", "id": "86944", "version": "10", "userModVersion": "1.10.0", "displayName": "Store Sample Mod" }
              ]
            }
            """);

        // playset_config.json 包含启用项、禁用项、云端待下载项与非激活 Playset
        File.WriteAllText(_paths.PlaysetConfigFile, """
            {
              "activePlaysetId": "playset-active",
              "playsets": [
                {
                  "id": "playset-other",
                  "presentation": { "name": "Other" },
                  "mods": [ { "source": "pdx_mods", "sourceId": "99999", "isEnabled": true } ]
                },
                {
                  "id": "playset-active",
                  "presentation": { "name": "Main Test Playset" },
                  "mods": [
                    { "source": "pdx_mods", "sourceId": "86944", "isEnabled": true },
                    { "source": "pdx_mods", "sourceId": "77777", "isEnabled": false },
                    { "source": "pdx_mods", "sourceId": "55555", "isEnabled": true },
                    { "source": "local", "sourceId": "MyLocalPack", "isEnabled": true }
                  ]
                }
              ]
            }
            """);

        PlaysetScanResult result = PlaysetModScanner.Scan(_paths, gameDir: null);

        Assert.Equal("Main Test Playset", result.ActivePlaysetName);
        Assert.False(result.ManagedDirectoryAvailable);
        Assert.Empty(result.UnreadableAssemblies);

        EnabledModPackage samplePkg = Assert.Single(result.Packages, p => p.PdxModId == "86944");
        Assert.Equal(10, samplePkg.PlatformVersion);
        Assert.Equal("Store Sample Mod", samplePkg.DisplayName);
        Assert.Equal("1.10.0", samplePkg.DisplayVersion);
        WinningAssemblyEntry sampleWin = Assert.Single(samplePkg.WinningAssemblies);
        Assert.Equal(new Version(1, 10, 0, 0), sampleWin.AssemblyVersion);

        EnabledModPackage pendingPkg = Assert.Single(result.Packages, p => p.PdxModId == "55555");
        Assert.True(pendingPkg.IsPendingCloudDownload);

        EnabledModPackage sdkLocalPkg = Assert.Single(result.Packages, p => p.Source == ModInstallSource.PdxLocal);
        Assert.Equal("SDK Local Mod", sdkLocalPkg.DisplayName);
        Assert.Equal("0.5.0-local", sdkLocalPkg.DisplayVersion);
        Assert.False(Assert.Single(sdkLocalPkg.WinningAssemblies).IsLocal);
    }

    [Fact]
    public void Scan_SkipsDotAndTildeFolders_AndFiltersUnloadableDuplicateBeforeRanking()
    {
        // 准备模拟游戏 Managed/ 目录
        string fakeGameDir = Path.Combine(_tempRoot, "FakeGame");
        string managedDir = Path.Combine(fakeGameDir, "Cities2_Data", "Managed");
        Directory.CreateDirectory(managedDir);

        string runtimeCoreName = typeof(object).Assembly.GetName().Name!;
        File.WriteAllBytes(Path.Combine(managedDir, runtimeCoreName + ".dll"), [0]);
        File.WriteAllBytes(Path.Combine(managedDir, "Unity.Entities.dll"), SyntheticModBuilder.Env.UnityEntitiesBytes);
        File.WriteAllBytes(Path.Combine(managedDir, "Game.dll"), SyntheticModBuilder.Env.GameBytes);
        File.WriteAllBytes(Path.Combine(managedDir, "Newtonsoft.Json.dll"), [0]);

        // 1. Skyve 禁用的 .DisabledMod 与 ~Backup 目录必须跳过
        string skyveDisabled = Path.Combine(_paths.ModsDir, ".DisabledBySkyve");
        string tildeBackup = Path.Combine(_paths.ModsDir, "~BackupMod");
        Directory.CreateDirectory(skyveDisabled);
        Directory.CreateDirectory(tildeBackup);
        File.WriteAllBytes(Path.Combine(skyveDisabled, "ShouldSkip.dll"), BuildSimpleIMod("ShouldSkip", new Version(1, 0, 0)));
        File.WriteAllBytes(Path.Combine(tildeBackup, "ShouldSkip2.dll"), BuildSimpleIMod("ShouldSkip2", new Version(1, 0, 0)));

        // 2. 本地 Mods/DupMod 虽为 isLocal==true 且版本号更高（2.0.0），但引用了不存在的前置 GhostDep，导致 !canBeLoaded
        byte[] ghostDepBytes = SyntheticModBuilder.BuildModAssembly("GhostDep", new Version(1, 0, 0), (m, _) =>
        {
            TypeBuilder tb = m.DefineType("Ghost.Marker", TypeAttributes.Public | TypeAttributes.Class);
            tb.DefineDefaultConstructor(MethodAttributes.Public);
            tb.CreateType();
        });
        Assembly ghostAsm = SyntheticModBuilder.Env.Alc.LoadFromStream(new MemoryStream(ghostDepBytes));
        Type ghostMarker = ghostAsm.GetType("Ghost.Marker")!;

        byte[] brokenLocalDup = SyntheticModBuilder.BuildModAssembly("DupMod", new Version(2, 0, 0), (m, env) =>
        {
            TypeBuilder tb = m.DefineType("Dup.BrokenMod", TypeAttributes.Public | TypeAttributes.Class);
            tb.AddInterfaceImplementation(env.IModType);
            tb.DefineField("_ghost", ghostMarker, FieldAttributes.Private);
            DefineIModMethods(tb, env);
            tb.CreateType();
        });

        string localDupDir = Path.Combine(_paths.ModsDir, "DupMod");
        Directory.CreateDirectory(localDupDir);
        File.WriteAllBytes(Path.Combine(localDupDir, "DupMod.dll"), brokenLocalDup);
        // 与 Managed/ 同名的随包 Newtonsoft.Json.dll（故意写坏字节）：应按 GetModAssets 提前跳过，不报损坏
        File.WriteAllBytes(Path.Combine(localDupDir, "Newtonsoft.Json.dll"), [0x01, 0x02, 0x03]);
        // 另一个畸形非同名 DLL：容错记入 UnreadableAssemblies，不中断扫描
        File.WriteAllBytes(Path.Combine(localDupDir, "CorruptedHelper.dll"), [0xDE, 0xAD, 0xBE, 0xEF]);

        // 3. PDX 订阅版 DupMod（1.0.0）依赖齐全（canBeLoaded == true），应先滤除不可加载的本地版后胜出！
        string pdxRoot = Path.Combine(_paths.ModsCacheDir, "pdx_mods");
        string pdxDupDir = Path.Combine(pdxRoot, "12345_1");
        Directory.CreateDirectory(pdxDupDir);
        File.WriteAllBytes(Path.Combine(pdxDupDir, "DupMod.dll"), BuildSimpleIMod("DupMod", new Version(1, 0, 0)));

        File.WriteAllText(_paths.PlaysetConfigFile, """
            {
              "activePlaysetId": "p1",
              "playsets": [
                {
                  "id": "p1",
                  "presentation": { "name": "DepFilterTest" },
                  "mods": [ { "source": "pdx_mods", "sourceId": "12345", "isEnabled": true } ]
                }
              ]
            }
            """);

        PlaysetScanResult result = PlaysetModScanner.Scan(_paths, fakeGameDir);

        Assert.True(result.ManagedDirectoryAvailable);
        Assert.DoesNotContain(result.WinningAssemblies, a => a.AssemblyName.StartsWith("ShouldSkip", StringComparison.Ordinal));

        // 关键断言：先滤除缺 GhostDep 的本地 2.0.0 副本，选出可加载的订阅 1.0.0 副本！
        WinningAssemblyEntry dupWinner = Assert.Single(result.WinningAssemblies, a => a.AssemblyName == "DupMod");
        Assert.Equal(ModInstallSource.PdxMods, dupWinner.Package.Source);
        Assert.Equal(new Version(1, 0, 0, 0), dupWinner.AssemblyVersion);

        // 损坏的 CorruptedHelper.dll 被记录，而同名的 Newtonsoft.Json.dll 被提前跳过
        UnreadableAssemblyEntry bad = Assert.Single(result.UnreadableAssemblies);
        Assert.EndsWith("CorruptedHelper.dll", bad.FilePath, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    public void Scan_NonObjectPlaysetConfigRoot_RecordsErrorWithoutThrowing(string rootJson)
    {
        Directory.CreateDirectory(_paths.ModsCacheDir);
        File.WriteAllText(_paths.PlaysetConfigFile, rootJson);
        File.WriteAllText(_paths.ModsDirectoryFile, "[]");
        File.WriteAllText(_paths.PdxModsCacheFile, "[]");

        PlaysetScanResult result = PlaysetModScanner.Scan(_paths, gameDir: null);

        Assert.NotNull(result.PlaysetConfigError);
        Assert.Empty(result.Packages);
    }

    [Fact]
    public void Scan_NullElementsInPlaysetAndNonObjectCacheRoots_SkipsCleanlyWithoutThrowing()
    {
        string pdxRoot = Path.Combine(_paths.ModsCacheDir, "pdx_mods");
        string v1Dir = Path.Combine(pdxRoot, "86944_1");
        Directory.CreateDirectory(v1Dir);
        File.WriteAllBytes(Path.Combine(v1Dir, "GoodMod.dll"), BuildSimpleIMod("GoodMod", new Version(1, 0, 0)));

        File.WriteAllText(_paths.ModsDirectoryFile, "[]");
        File.WriteAllText(_paths.PdxModsCacheFile, "[]");
        File.WriteAllText(_paths.PlaysetConfigFile, """
            {
              "activePlaysetId": "p1",
              "playsets": [
                null,
                "invalid-entry",
                {
                  "id": "p1",
                  "presentation": null,
                  "mods": [
                    null,
                    123,
                    { "source": "pdx_mods", "sourceId": "86944", "isEnabled": true }
                  ]
                }
              ]
            }
            """);

        PlaysetScanResult result = PlaysetModScanner.Scan(_paths, gameDir: null);

        Assert.Null(result.PlaysetConfigError);
        EnabledModPackage pkg = Assert.Single(result.Packages, p => p.PdxModId == "86944");
        Assert.Single(pkg.WinningAssemblies);
    }

    [Fact]
    public void Scan_LooseRootDll_WithSameStemAsSubfolder_KeptAsSeparatePackage()
    {
        Directory.CreateDirectory(_paths.ModsDir);
        string subDir = Path.Combine(_paths.ModsDir, "SameName");
        Directory.CreateDirectory(subDir);

        File.WriteAllBytes(Path.Combine(subDir, "FolderMod.dll"), BuildSimpleIMod("FolderMod", new Version(1, 0, 0)));
        File.WriteAllBytes(Path.Combine(_paths.ModsDir, "SameName.dll"), BuildSimpleIMod("LooseRootMod", new Version(1, 0, 0)));

        PlaysetScanResult result = PlaysetModScanner.Scan(_paths, gameDir: null);

        Assert.Contains(result.Packages, p => p.PackageKey == "usermods:SameName");
        Assert.Contains(result.Packages, p => p.PackageKey == "usermods-file:SameName.dll");
        Assert.Contains(result.WinningAssemblies, a => a.AssemblyName == "FolderMod");
        Assert.Contains(result.WinningAssemblies, a => a.AssemblyName == "LooseRootMod");
    }

    [Fact]
    public void Scan_PreferredVersionMissingOnDisk_DoesNotFallbackToOtherVersion()
    {
        string pdxRoot = Path.Combine(_paths.ModsCacheDir, "pdx_mods");
        string v9Dir = Path.Combine(pdxRoot, "86944_9");
        Directory.CreateDirectory(v9Dir);
        File.WriteAllBytes(Path.Combine(v9Dir, "SampleMod.dll"), BuildSimpleIMod("SampleMod", new Version(1, 9, 0)));

        File.WriteAllText(_paths.PlaysetConfigFile, """
            {
              "activePlaysetId": "p1",
              "playsets": [
                {
                  "id": "p1",
                  "presentation": { "name": "PinnedVersion" },
                  "mods": [
                    { "source": "pdx_mods", "sourceId": "86944", "sourceVersion": "10", "isEnabled": true }
                  ]
                }
              ]
            }
            """);

        PlaysetScanResult result = PlaysetModScanner.Scan(_paths, gameDir: null);

        EnabledModPackage pkg = Assert.Single(result.Packages, p => p.PdxModId == "86944");
        Assert.True(pkg.IsPendingCloudDownload);
        Assert.Equal(10, pkg.PlatformVersion);
        Assert.Empty(pkg.WinningAssemblies);
    }

    [Fact]
    public void Scan_TargetInactivePlayset_ReadsSyncBackendIds_Tags_DisabledMods_AndMapExtPresence()
    {
        string pdxRoot = Path.Combine(_paths.ModsCacheDir, "pdx_mods");
        string mapExtDir = Path.Combine(pdxRoot, "74417_17");
        string otherModDir = Path.Combine(pdxRoot, "99999_2");
        string assetPackDir = Path.Combine(pdxRoot, "88888_1");
        Directory.CreateDirectory(mapExtDir);
        Directory.CreateDirectory(otherModDir);
        Directory.CreateDirectory(assetPackDir);

        File.WriteAllBytes(Path.Combine(mapExtDir, "MapExt2.dll"), BuildSimpleIMod("MapExt2", new Version(4, 12, 0)));
        File.WriteAllBytes(Path.Combine(otherModDir, "OtherMod.dll"), BuildSimpleIMod("OtherMod", new Version(2, 1, 0)));
        File.WriteAllText(Path.Combine(assetPackDir, "AssetOnly.cok"), "dummy-asset");

        File.WriteAllText(_paths.PdxModsCacheFile, """
            {
              "items": [
                {
                  "source": "pdx_mods",
                  "id": "99999",
                  "version": "2",
                  "userModVersion": "2.1.0",
                  "displayName": "Inactive Code Mod",
                  "author": "Alice",
                  "requiredGameVersion": "1.6.*",
                  "size": 2097152,
                  "tags": [ { "id": "t1", "displayName": "Simulation" } ],
                  "systemTags": [ { "id": "s1", "displayName": "Code Mod", "type": "ModType" } ]
                },
                {
                  "source": "pdx_mods",
                  "id": "88888",
                  "version": "1",
                  "userModVersion": "1.0",
                  "displayName": "Harbor Asset Pack",
                  "author": "Bob",
                  "size": 0,
                  "tags": [ { "id": "t2", "displayName": "Asset" } ]
                },
                {
                  "source": "pdx_mods",
                  "id": "77777",
                  "version": "4",
                  "userModVersion": "0.9",
                  "displayName": "Disabled Sample Mod",
                  "author": "Carol"
                }
              ]
            }
            """);

        File.WriteAllText(_paths.PlaysetSyncFile, """
            {
              "configVersion": 1,
              "playsets": [
                {
                  "id": "uuid-active",
                  "backends": [ { "id": "pdx_mods", "playsetId": "1001" } ]
                },
                {
                  "id": "uuid-inactive",
                  "backends": [ { "id": "pdx_mods", "playsetId": "2002" } ]
                },
                {
                  "id": "uuid-dup-a",
                  "backends": [ { "id": "pdx_mods", "playsetId": "3001" } ]
                },
                {
                  "id": "uuid-dup-b",
                  "backends": [ { "id": "pdx_mods", "playsetId": "3002" } ]
                }
              ]
            }
            """);

        // 在 uuid-inactive 中构造：同一 ID 99999 先禁用后启用（验证禁用项不吞掉后续启用项），且不含 MapExt 74417
        File.WriteAllText(_paths.PlaysetConfigFile, """
            {
              "activePlaysetId": "uuid-active",
              "playsets": [
                {
                  "id": "uuid-inactive",
                  "isOffdisk": true,
                  "presentation": { "name": "Vanilla Builder Set" },
                  "mods": [
                    { "source": "pdx_mods", "sourceId": "99999", "isEnabled": false },
                    { "source": "pdx_mods", "sourceId": "99999", "isEnabled": true },
                    { "source": "pdx_mods", "sourceId": "88888", "isEnabled": true },
                    { "source": "pdx_mods", "sourceId": "77777", "isEnabled": false }
                  ]
                },
                {
                  "id": "uuid-active",
                  "isOffdisk": true,
                  "presentation": { "name": "MapExt Active Set" },
                  "mods": [
                    { "source": "pdx_mods", "sourceId": "74417", "isEnabled": true }
                  ]
                },
                {
                  "id": "uuid-dup-a",
                  "presentation": { "name": "Duplicate Name" },
                  "mods": []
                },
                {
                  "id": "uuid-dup-b",
                  "presentation": { "name": "Duplicate Name" },
                  "mods": []
                }
              ]
            }
            """);

        // 1. ListPlaysets 与 ResolvePlayset：激活项排第一且强制 IsOffdisk=false，支持按后端数字 ID 命中与重名歧义检测
        PlaysetCatalogResult catalog = PlaysetModScanner.ListPlaysets(_paths);
        Assert.Equal(4, catalog.Playsets.Count);
        Assert.True(catalog.Playsets[0].IsActive);
        Assert.False(catalog.Playsets[0].IsOffdisk);
        Assert.Equal("1001", catalog.Playsets[0].BackendPlaysetId);

        PlaysetLookupResult byBackendId = PlaysetModScanner.ResolvePlayset(catalog.Playsets, "2002");
        Assert.Equal(PlaysetLookupKind.Matched, byBackendId.Kind);
        Assert.Equal("uuid-inactive", byBackendId.Match!.Id);

        PlaysetLookupResult ambiguous = PlaysetModScanner.ResolvePlayset(catalog.Playsets, "duplicate name");
        Assert.Equal(PlaysetLookupKind.Ambiguous, ambiguous.Kind);
        Assert.Equal(2, ambiguous.Candidates.Count);

        // 2. 默认扫描激活播放集：包含 MapExt
        PlaysetScanResult activeScan = PlaysetModScanner.Scan(_paths);
        Assert.True(activeScan.IsTargetActivePlayset);
        Assert.True(activeScan.IsMapExtEnabledInTarget);

        // 3. 指定扫描非激活播放集 uuid-inactive
        PlaysetScanResult inactiveScan = PlaysetModScanner.Scan(_paths, targetPlaysetId: "uuid-inactive");
        Assert.False(inactiveScan.IsTargetActivePlayset);
        Assert.True(inactiveScan.IsTargetOffdisk);
        Assert.True(inactiveScan.TargetPlaysetFound);
        Assert.False(inactiveScan.IsMapExtEnabledInTarget);
        Assert.Equal("Vanilla Builder Set", inactiveScan.EffectivePlaysetName);

        // 99999 虽第一条是 isEnabled:false，但第二条 isEnabled:true 必须正常进入胜出程序集！
        Assert.Contains(inactiveScan.WinningAssemblies, a => a.AssemblyName == "OtherMod");

        IReadOnlyList<PlaysetModListItem> list = inactiveScan.EffectiveAllModItems;
        Assert.Equal(4, list.Count);
        PlaysetModListItem enabledCode = Assert.Single(list, m => m.LoadOrder == 2);
        Assert.Equal(ModLoadState.LoadedCodeMod, enabledCode.LoadState);
        Assert.Equal("Alice", enabledCode.Author);
        Assert.Equal("1.6.*", enabledCode.RequiredGameVersion);
        Assert.Equal(2097152UL, enabledCode.SizeBytes);
        Assert.Equal(["Simulation", "Code Mod"], enabledCode.Tags);

        PlaysetModListItem assetItem = Assert.Single(list, m => m.LoadOrder == 3);
        Assert.Equal(ModLoadState.AssetOrNonCode, assetItem.LoadState);
        Assert.Null(assetItem.SizeBytes); // size == 0 视为未知不展示

        PlaysetModListItem disabledItem = Assert.Single(list, m => m.LoadOrder == 4);
        Assert.False(disabledItem.IsEnabled);
        Assert.Equal(ModLoadState.Disabled, disabledItem.LoadState);
        Assert.Equal("Carol", disabledItem.Author);
    }

    [Fact]
    public void Scan_MapExtPendingDownloadInTarget_CoversAllFourBranches()
    {
        Directory.CreateDirectory(_paths.ModsCacheDir);
        File.WriteAllText(_paths.PlaysetConfigFile, """
            {
              "configVersion": 1,
              "activePlaysetId": "ps-pending",
              "playsets": [
                {
                  "id": "ps-pending",
                  "presentation": { "name": "Pending MapExt" },
                  "mods": [ { "source": "pdx_mods", "sourceId": "107939", "isEnabled": true } ]
                },
                {
                  "id": "ps-none",
                  "presentation": { "name": "No MapExt" },
                  "mods": []
                }
              ]
            }
            """);

        // 1. 订阅版 107939 尚未下载到本地：IsMapExtEnabledInTarget 与 IsMapExtPendingDownloadInTarget 均为真
        PlaysetScanResult pending = PlaysetModScanner.Scan(_paths, targetPlaysetId: "ps-pending");
        Assert.True(pending.IsMapExtEnabledInTarget);
        Assert.True(pending.IsMapExtPendingDownloadInTarget);

        // 2. 目标未启用 MapExt 且本地 Mods/ 无 MapExt：两者均为假
        PlaysetScanResult none = PlaysetModScanner.Scan(_paths, targetPlaysetId: "ps-none");
        Assert.False(none.IsMapExtEnabledInTarget);
        Assert.False(none.IsMapExtPendingDownloadInTarget);

        // 3. 本地 Mods/ 有 MapExt2.dll，同时订阅版 107939 仍待下载：已落盘优先，IsMapExtPendingDownloadInTarget 为假
        string localMapExtDir = Path.Combine(_paths.ModsDir, "MapExt2");
        Directory.CreateDirectory(localMapExtDir);
        File.WriteAllBytes(Path.Combine(localMapExtDir, "MapExt2.dll"), BuildSimpleIMod("MapExt2", new Version(4, 12, 0)));

        PlaysetScanResult localAndPending = PlaysetModScanner.Scan(_paths, targetPlaysetId: "ps-pending");
        Assert.True(localAndPending.IsMapExtEnabledInTarget);
        Assert.False(localAndPending.IsMapExtPendingDownloadInTarget);

        // 4. 清理本地 Mods/，改为订阅版 107939 已落盘：IsMapExtEnabledInTarget 为真、IsMapExtPendingDownloadInTarget 为假
        RemoveDirectorySafe(localMapExtDir);
        string pdxMapExtDir = Path.Combine(_paths.ModsCacheDir, "pdx_mods", "107939_1");
        Directory.CreateDirectory(pdxMapExtDir);
        File.WriteAllBytes(Path.Combine(pdxMapExtDir, "MapExt2.dll"), BuildSimpleIMod("MapExt2", new Version(4, 12, 0)));

        PlaysetScanResult pdxOnDisk = PlaysetModScanner.Scan(_paths, targetPlaysetId: "ps-pending");
        Assert.True(pdxOnDisk.IsMapExtEnabledInTarget);
        Assert.False(pdxOnDisk.IsMapExtPendingDownloadInTarget);
    }

    [Fact]
    public void ListPlaysets_AndResolvePlayset_MatchSdkCaseSensitivityAndDetectCaseAmbiguity()
    {
        Directory.CreateDirectory(_paths.ModsCacheDir);
        // activePlaysetId 故意写成错误大小写 "ps-beta"：对齐 SDK（LocalId == activePlaysetId 区分大小写），不应将 "ps-Beta" 误判为已激活
        File.WriteAllText(_paths.PlaysetConfigFile, """
            {
              "configVersion": 1,
              "activePlaysetId": "ps-beta",
              "playsets": [
                { "id": "ps-Beta", "presentation": { "name": "Beta" }, "mods": [] },
                { "id": "ps-A", "presentation": { "name": "Upper A" }, "mods": [] },
                { "id": "ps-a", "presentation": { "name": "Lower A" }, "mods": [] }
              ]
            }
            """);

        PlaysetCatalogResult catalog = PlaysetModScanner.ListPlaysets(_paths);
        Assert.Equal(3, catalog.Playsets.Count);
        Assert.DoesNotContain(catalog.Playsets, p => p.IsActive);

        // 精确大小写匹配优先
        PlaysetLookupResult exactLower = PlaysetModScanner.ResolvePlayset(catalog.Playsets, "ps-a");
        Assert.Equal(PlaysetLookupKind.Matched, exactLower.Kind);
        Assert.Equal("ps-a", exactLower.Match!.Id);

        // 两者都不是精确匹配（全大写 "PS-A"）且存在多个忽略大小写匹配时返回 Ambiguous
        PlaysetLookupResult ciAmbiguous = PlaysetModScanner.ResolvePlayset(catalog.Playsets, "PS-A");
        Assert.Equal(PlaysetLookupKind.Ambiguous, ciAmbiguous.Kind);
        Assert.Equal(2, ciAmbiguous.Candidates.Count);
    }

    private static byte[] BuildSimpleIMod(string asmName, Version version) =>
        SyntheticModBuilder.BuildModAssembly(asmName, version, (mod, env) =>
        {
            TypeBuilder tb = mod.DefineType(asmName + ".ModEntry", TypeAttributes.Public | TypeAttributes.Class);
            tb.AddInterfaceImplementation(env.IModType);
            DefineIModMethods(tb, env);
            tb.CreateType();
        });

    private static void DefineIModMethods(TypeBuilder tb, SyntheticModBuilder.StubEnvironment env)
    {
        Type updateSysType = env.GameAsm.GetType("Game.UpdateSystem")!;
        tb.DefineMethod("OnLoad", MethodAttributes.Public | MethodAttributes.Virtual, typeof(void), [updateSysType])
          .GetILGenerator().Emit(OpCodes.Ret);
        tb.DefineMethod("OnDispose", MethodAttributes.Public | MethodAttributes.Virtual, typeof(void), Type.EmptyTypes)
          .GetILGenerator().Emit(OpCodes.Ret);
    }

    private static void RemoveDirectorySafe(string root)
    {
        var dirs = new Stack<string>();
        var postOrder = new Stack<string>();
        dirs.Push(root);
        while (dirs.Count > 0)
        {
            string cur = dirs.Pop();
            postOrder.Push(cur);
            foreach (string f in Directory.GetFiles(cur))
                File.Delete(f);
            foreach (string sub in Directory.GetDirectories(cur))
                dirs.Push(sub);
        }

        while (postOrder.Count > 0)
        {
            Directory.Delete(postOrder.Pop(), recursive: false);
        }
    }
}
