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
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem

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
