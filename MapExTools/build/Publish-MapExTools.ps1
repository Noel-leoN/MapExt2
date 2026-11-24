#Requires -Version 7
<#
.SYNOPSIS
    发布 MapExTools 的 portable（自带运行时）与 lite（依赖 .NET 11 Desktop Runtime）两个单文件压缩包及 SHA-256。

.DESCRIPTION
    按 MapExTools v1.0 计划 5.4 构建并校验两个 Windows x64 单文件发布包：
      - MapExTools-v<Version>-win-x64-portable.zip（SelfContained=true，内嵌原生库并启用单文件压缩）
      - MapExTools-v<Version>-win-x64-lite.zip（SelfContained=false，依赖本机 .NET 11 Desktop Runtime）
      - MapExTools-v<Version>-SHA256SUMS.txt

    构建前默认先跑完单元测试；每个包打包前与打包后都会核对产物中只有唯一一个 MapExTools.exe（无 .pdb、无散落 .dll）。
    默认输出到 MapExTools/bin/publish（已被仓库根目录 .gitignore 的 [Bb]in/ 忽略，不污染工作区）。

    用法：
        pwsh -NoProfile -File MapExTools/build/Publish-MapExTools.ps1
#>
[CmdletBinding()]
param(
    [string]$OutDir = (Join-Path $PSScriptRoot '../bin/publish'),
    [string]$Version,
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem, System.Reflection.Metadata

if (-not ('MapExToolsSingleFilePdbVerifier' -as [type])) {
    Add-Type -ReferencedAssemblies 'System.Collections', 'System.Collections.Immutable', 'System.IO.Compression', 'System.Reflection.Metadata', 'System.Text.RegularExpressions' -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.RegularExpressions;

public static class MapExToolsSingleFilePdbVerifier
{
    private static readonly byte[] BundleSignature = new byte[]
    {
        0x8b, 0x12, 0x02, 0xb9, 0x6a, 0x61, 0x20, 0x38,
        0x72, 0x7b, 0x93, 0x02, 0x14, 0xd7, 0xa0, 0x32,
        0x13, 0xf5, 0xb9, 0xe6, 0xef, 0xae, 0x33, 0x18,
        0xee, 0x3b, 0x2d, 0xce, 0x24, 0xb3, 0x6a, 0xae
    };

    private static readonly Guid SourceLinkGuid = new Guid("cc110556-a091-4d38-9fec-25ab9a351a6a");
    private static readonly Regex DrivePathRegex = new Regex(@"(?:^|[^A-Za-z])[A-Za-z]:(?:\\|/)", RegexOptions.Compiled);

    public static int VerifySingleFileBundle(string exePath, string[] targetDllNames, out string[] leaks)
    {
        byte[] exeBytes = File.ReadAllBytes(exePath);
        int sigOffset = FindSignatureOffset(exeBytes, BundleSignature);
        if (sigOffset < 8)
            throw new InvalidDataException($"在 {exePath} 中未找到 .NET 单文件 Bundle 签名");

        long headerOffset = BitConverter.ToInt64(exeBytes, sigOffset - 8);
        if (headerOffset <= 0 || headerOffset >= exeBytes.Length)
            throw new InvalidDataException($"Bundle 清单偏移无效: {headerOffset}");

        var targets = new HashSet<string>(targetDllNames, StringComparer.OrdinalIgnoreCase);
        var leakList = new List<string>();
        int checkedPdbs = 0;

        using var ms = new MemoryStream(exeBytes, writable: false);
        ms.Position = headerOffset;
        using var reader = new BinaryReader(ms, Encoding.UTF8, leaveOpen: true);

        uint majorVersion = reader.ReadUInt32();
        uint minorVersion = reader.ReadUInt32();
        int fileCount = reader.ReadInt32();
        _ = reader.ReadString(); // bundleId

        if (majorVersion >= 2)
        {
            _ = reader.ReadInt64(); // depsJsonOffset
            _ = reader.ReadInt64(); // depsJsonSize
            _ = reader.ReadInt64(); // runtimeConfigJsonOffset
            _ = reader.ReadInt64(); // runtimeConfigJsonSize
            _ = reader.ReadUInt64(); // flags
        }

        var extracted = new List<(string Name, long Offset, long Size, long CompressedSize)>();
        for (int i = 0; i < fileCount; i++)
        {
            long offset = reader.ReadInt64();
            long size = reader.ReadInt64();
            long compressedSize = majorVersion >= 6 ? reader.ReadInt64() : 0;
            _ = reader.ReadByte(); // fileType
            string relativePath = reader.ReadString();

            if (targets.Contains(relativePath))
                extracted.Add((relativePath, offset, size, compressedSize));
        }

        foreach (var item in extracted)
        {
            byte[] dllBytes = ReadBundleEntry(exeBytes, item.Offset, item.Size, item.CompressedSize);
            checkedPdbs += InspectEmbeddedPdb(item.Name, dllBytes, leakList);
        }

        leaks = leakList.ToArray();
        return checkedPdbs;
    }

    private static byte[] ReadBundleEntry(byte[] exeBytes, long offset, long size, long compressedSize)
    {
        if (compressedSize > 0)
        {
            using var compStream = new MemoryStream(exeBytes, (int)offset, (int)compressedSize, writable: false);
            using var deflate = new DeflateStream(compStream, CompressionMode.Decompress);
            using var output = new MemoryStream((int)size);
            deflate.CopyTo(output);
            byte[] result = output.ToArray();
            if (result.Length != size)
                throw new InvalidDataException($"单文件条目解压尺寸不符：预期 {size}，实际 {result.Length}");
            return result;
        }

        byte[] raw = new byte[size];
        Buffer.BlockCopy(exeBytes, (int)offset, raw, 0, (int)size);
        return raw;
    }

    private static int InspectEmbeddedPdb(string dllName, byte[] dllBytes, List<string> leaks)
    {
        int count = 0;
        using var peStream = new MemoryStream(dllBytes, writable: false);
        using var peReader = new PEReader(peStream);

        foreach (DebugDirectoryEntry entry in peReader.ReadDebugDirectory())
        {
            if (entry.Type != DebugDirectoryEntryType.EmbeddedPortablePdb)
                continue;

            using MetadataReaderProvider provider = peReader.ReadEmbeddedPortablePdbDebugDirectoryData(entry);
            MetadataReader pdb = provider.GetMetadataReader();
            count++;

            foreach (DocumentHandle dh in pdb.Documents)
            {
                string docName = pdb.GetString(pdb.GetDocument(dh).Name);
                if (DrivePathRegex.IsMatch(docName))
                    leaks.Add($"{dllName} [Document]: {docName}");
            }

            foreach (CustomDebugInformationHandle ch in pdb.CustomDebugInformation)
            {
                CustomDebugInformation cdi = pdb.GetCustomDebugInformation(ch);
                if (pdb.GetGuid(cdi.Kind) == SourceLinkGuid)
                {
                    string json = Encoding.UTF8.GetString(pdb.GetBlobBytes(cdi.Value));
                    if (DrivePathRegex.IsMatch(json))
                        leaks.Add($"{dllName} [SourceLink]: {json}");
                }
            }
        }

        return count;
    }

    private static int FindSignatureOffset(byte[] haystack, byte[] needle)
    {
        int limit = haystack.Length - needle.Length;
        byte first = needle[0];
        for (int i = 0; i <= limit; i++)
        {
            if (haystack[i] != first)
                continue;

            int j = 1;
            while (j < needle.Length && haystack[i + j] == needle[j])
                j++;
            if (j == needle.Length)
                return i;
        }

        return -1;
    }
}
'@
}

$toolRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$solution = Join-Path $toolRoot 'MapExTools.slnx'
$appProj = Join-Path $toolRoot 'src/MapExTools/MapExTools.csproj'
$propsFile = Join-Path $toolRoot 'Directory.Build.props'

if ([string]::IsNullOrWhiteSpace($Version)) {
    [xml]$propsXml = [IO.File]::ReadAllText($propsFile, [Text.Encoding]::UTF8)
    $Version = ($propsXml.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1).Trim()
    if ([string]::IsNullOrWhiteSpace($Version)) {
        throw "无法从 $propsFile 读取 <Version>"
    }
}

$outRoot = [IO.Path]::GetFullPath($OutDir)
[IO.Directory]::CreateDirectory($outRoot) | Out-Null

Write-Host "=== MapExTools v$Version 发布构建 ==="
$sdkVer = (& dotnet --version).Trim()
if ($LASTEXITCODE -ne 0) { throw 'dotnet --version 执行失败' }
Write-Host "SDK 版本: $sdkVer"

if (-not $SkipTests) {
    Write-Host '--- 运行单元测试 (Release) ---'
    & dotnet test $solution -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw "单元测试失败（退出码 $LASTEXITCODE）" }
}

$variants = @(
    @{
        Suffix    = 'portable'
        # WPF 含 C++/CLI 混合模式程序集 DirectWriteForwarder.dll（CorHeader 非空但非纯 IL）：
        # Bundler.IsAssembly 按托管程序集打入单文件，不设 IncludeAllContentForSelfExtract 时 CoreCLR 尝试从内存加载会抛
        # BadImageFormatException；SDK 11 对该属性报 NETSDK1244 提示，仅在便携包发布命令行局部压制，保持工程级零警告。
        ExtraArgs = @(
            '-p:SelfContained=true',
            '-p:IncludeNativeLibrariesForSelfExtract=true',
            '-p:IncludeAllContentForSelfExtract=true',
            '-p:EnableCompressionInSingleFile=true',
            '-p:NoWarn=NETSDK1244'
        )
    },
    @{
        Suffix    = 'lite'
        ExtraArgs = @(
            '-p:SelfContained=false'
        )
    }
)

$results = [System.Collections.Generic.List[object]]::new()

foreach ($v in $variants) {
    $suffix = $v.Suffix
    $zipName = "MapExTools-v$Version-win-x64-$suffix.zip"
    $zipPath = Join-Path $outRoot $zipName
    $stageDir = Join-Path $outRoot "stage-$suffix"

    if ([IO.Directory]::Exists($stageDir)) {
        $leftoverDirs = @([IO.Directory]::GetDirectories($stageDir))
        if ($leftoverDirs.Count -gt 0) {
            $names = ($leftoverDirs | ForEach-Object { [IO.Path]::GetFileName($_) }) -join ', '
            throw "暂存目录 '$stageDir' 中存在残留子目录（$names），本脚本不执行递归删除，请先手动清空该目录后重试。"
        }
        foreach ($oldFile in [IO.Directory]::GetFiles($stageDir)) {
            [IO.File]::Delete($oldFile)
        }
        [IO.Directory]::Delete($stageDir, $false)
    }
    if ([IO.File]::Exists($zipPath)) {
        [IO.File]::Delete($zipPath)
    }

    Write-Host "--- 发布 $suffix 单文件包 ---"
    $pubArgs = @(
        'publish', $appProj,
        '-c', 'Release',
        '-r', 'win-x64',
        '-p:PublishSingleFile=true',
        '-p:DebugType=embedded',
        '-p:ContinuousIntegrationBuild=true',
        '-p:PublishTrimmed=false',
        "-p:Version=$Version",
        '-o', $stageDir,
        '--nologo'
    ) + $v.ExtraArgs

    & dotnet @pubArgs
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish ($suffix) 失败（退出码 $LASTEXITCODE）" }

    # 严格核对暂存目录：不能有任何子目录，且只能有唯一一个 MapExTools.exe（不能有 .pdb 或原生 .dll）
    $subDirs = @([IO.Directory]::GetDirectories($stageDir))
    $files = @([IO.Directory]::GetFiles($stageDir))
    if ($subDirs.Count -ne 0 -or $files.Count -ne 1 -or [IO.Path]::GetFileName($files[0]) -cne 'MapExTools.exe') {
        $found = ($subDirs + $files) | ForEach-Object { [IO.Path]::GetRelativePath($stageDir, $_) }
        throw "$suffix 发布目录不是纯净单文件 exe，实际包含: $($found -join ', ')"
    }

    $exePath = $files[0]
    $exeBytes = ([IO.FileInfo]::new($exePath)).Length

    # 解开单文件 Bundle v6 清单（含 portable 的二次 Deflate 解压），核对 2 份内嵌 PDB 的文档表与 SourceLink 均不含盘符路径
    [string[]]$pdbLeaks = @()
    $checkedPdbs = [MapExToolsSingleFilePdbVerifier]::VerifySingleFileBundle($exePath, @('MapExTools.dll', 'MapExTools.Core.dll'), [ref]$pdbLeaks)
    if ($checkedPdbs -ne 2) {
        throw "$suffix 内嵌 PDB 自检失败：预期查到 2 份内嵌 PDB，实际查到 $checkedPdbs 份"
    }
    if ($pdbLeaks.Count -gt 0) {
        $preview = ($pdbLeaks | Select-Object -First 5) -join "`n  "
        throw "$suffix 内嵌 PDB 检出 $($pdbLeaks.Count) 处本机绝对路径残留：`n  $preview"
    }
    Write-Host "  [OK] $suffix 内嵌 PDB 路径脱敏自检通过（已核查 $checkedPdbs 份内嵌 PDB 的文档表与 SourceLink）"

    # 启动冒烟核对：调用 --help 会经过 WPF Application 静态初始化（触发 DirectWriteForwarder.dll 加载），不开窗口、不读写玩家目录
    $helpOutFile = Join-Path $outRoot "smoke-$suffix.txt"
    $smokeProc = Start-Process -FilePath $exePath -ArgumentList '--help', '--lang', 'en-US' -RedirectStandardOutput $helpOutFile -NoNewWindow -Wait -PassThru
    $helpText = if ([IO.File]::Exists($helpOutFile)) { [IO.File]::ReadAllText($helpOutFile, [Text.Encoding]::UTF8) } else { '' }
    if ([IO.File]::Exists($helpOutFile)) { [IO.File]::Delete($helpOutFile) }
    if ($smokeProc.ExitCode -ne 0 -or -not $helpText.Contains('--mode')) {
        throw "$suffix 单文件启动冒烟失败（退出码 $($smokeProc.ExitCode)）"
    }

    # 打包成 zip，并重新打开核对归档内只有唯一条目 MapExTools.exe
    [IO.Compression.ZipFile]::CreateFromDirectory($stageDir, $zipPath, [IO.Compression.CompressionLevel]::Optimal, $false)
    $zipArchive = [IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        if ($zipArchive.Entries.Count -ne 1 -or $zipArchive.Entries[0].FullName -cne 'MapExTools.exe') {
            $entryNames = ($zipArchive.Entries | ForEach-Object { $_.FullName }) -join ', '
            throw "$zipName 条目校验失败: $entryNames"
        }
    }
    finally {
        $zipArchive.Dispose()
    }

    # 清理单文件暂存目录（不使用递归删除）
    [IO.File]::Delete($exePath)
    [IO.Directory]::Delete($stageDir, $false)

    $zipBytes = ([IO.FileInfo]::new($zipPath)).Length
    $sha = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $results.Add([pscustomobject]@{
        Variant  = $suffix
        ZipName  = $zipName
        ZipPath  = $zipPath
        ExeBytes = $exeBytes
        ZipBytes = $zipBytes
        Sha256   = $sha
    })
}

$sumsName = "MapExTools-v$Version-SHA256SUMS.txt"
$sumsPath = Join-Path $outRoot $sumsName
$sumsLines = foreach ($r in $results) { "$($r.Sha256)  $($r.ZipName)" }
[IO.File]::WriteAllText($sumsPath, (($sumsLines -join "`n") + "`n"), [Text.UTF8Encoding]::new($false))

Write-Host ''
Write-Host '=== 发布完成 ==='
foreach ($r in $results) {
    Write-Host ("  {0,-42} ZIP {1,7:N2} MB (EXE {2,7:N2} MB)  SHA256={3}" -f `
        $r.ZipName, ($r.ZipBytes / 1MB), ($r.ExeBytes / 1MB), $r.Sha256)
}
Write-Host "  校验文件: $sumsPath"
Write-Host "  GitHub Release 提示: tag 使用 mapextools-v$Version，请勿勾选 Set as the latest release（避免覆盖 Mod 主版本入口）。"
