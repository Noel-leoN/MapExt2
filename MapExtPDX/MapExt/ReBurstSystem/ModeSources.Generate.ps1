#Requires -Version 7
<#
.SYNOPSIS
    从 ModeA 生成各地图尺寸模式的副本。生成物不入版控，由 MapExt2.csproj 的
    GenerateModeSources 目标在编译前调用，也可手动执行。

.DESCRIPTION
    生成范围（路径相对本脚本所在的 ReBurstSystem/）：
      CellSystems/ModeA_57km/**/*.cs  → CellSystems/Mode{B,C,D}_*/
      EcoSystems/ModeA_57km/**/*.cs   → EcoSystems/Mode{B,C,D,E}_*/
      CellSystems/ModeA_57km/CellMapClosed/LandValueSystemMod.cs → EcoSystems/ModeE_vanilla/D2_LandValueSystemMod.cs
      ReBurstCore/XCellMapSystemRe.cs（模板，自身也参与编译） → ReBurstCore/XCellMapSystemRe_Mode{A-E}.cs

    转换规则按字节严格保留编码与格式：
    PDB 记录每个源文件的校验和，差一个 BOM，DLL 就不再逐字节一致：
      - 副本与 D2：保留源文件的 BOM 与换行，严格 UTF-8 解码，MapExtPDX.ModeA 换成目标命名空间；
      - XCellMapSystemRe：替换三行头注释、命名空间与 kMapSize，换行统一为 LF，写成带 BOM 的 UTF-8。

    先在内存中算出全部输出、完成全部检查，再开始写；任何检查失败都在写入前退出。
    内容相同的文件不写，保留时间戳，没有改动的建置才能跳过 CoreCompile。
    目标目录归本脚本所有：不在本次输出中的文件与空目录一律删除。生成物的改动会被下次建置覆盖，只改 ModeA。
    不调用 Git：目标本身就是生成物，源码包没有 .git 也能建置。
    输出用简练英文：MSBuild 的 Exec 按系统 OEM 代码页解码子进程输出，简体中文可能变成问号。

.PARAMETER ReBurstRoot
    ReBurstSystem 目录，默认为本脚本所在目录；夹具测试时另行指定。

.PARAMETER Check
    只比较不写；有待写或待删时 exit 1。

.EXAMPLE
    pwsh -NoProfile -File MapExtPDX/MapExt/ReBurstSystem/ModeSources.Generate.ps1
    pwsh -NoProfile -File MapExtPDX/MapExt/ReBurstSystem/ModeSources.Generate.ps1 -Check
#>
[CmdletBinding()]
param(
    [string]$ReBurstRoot = $PSScriptRoot,
    [switch]$Check
)
$ErrorActionPreference = 'Stop'

# === 模式表（全仓唯一一份；模式与 CV 的对应见 PatchManager、SystemReplacer） ===
$modes = [ordered]@{
    A = @{ Dir = 'ModeA_57km'; MapSize = 57344 }
    B = @{ Dir = 'ModeB_28km'; MapSize = 28672 }
    C = @{ Dir = 'ModeC_114km'; MapSize = 114688 }
    D = @{ Dir = 'ModeD_43km'; MapSize = 43008 }
    E = @{ Dir = 'ModeE_vanilla'; MapSize = 14336 }
}
# CellSystems 没有 ModeE：原版尺寸模式只替换地价系统，即 D2
$families = @(
    @{ Name = 'CellSystems'; Targets = @('B', 'C', 'D') }
    @{ Name = 'EcoSystems'; Targets = @('B', 'C', 'D', 'E') }
)
$strictUtf8 = [Text.UTF8Encoding]::new($false, $true)

try {
    $root = [IO.Path]::GetFullPath($ReBurstRoot).TrimEnd('\', '/')
    $rootItem = Get-Item -LiteralPath $root -Force -ErrorAction SilentlyContinue
    if (-not $rootItem -or -not $rootItem.PSIsContainer) { throw "ReBurstRoot is not a directory: $root" }
    if ($rootItem.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "ReBurstRoot must not be a link: $root" }

    function Get-RelPath([string]$Path) { [IO.Path]::GetRelativePath($root, $Path).Replace('\', '/') }

    # --- 路径守卫：不得越出根目录；路径的任一段、目录的任一后代都不得是 Junction／符号链接 ---
    function Assert-InRoot([string]$Path) {
        if (-not ([IO.Path]::GetFullPath($Path)).StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            throw "path escapes ReBurstRoot: $Path"
        }
    }
    function Assert-SafeTree([string]$Dir) {
        Assert-InRoot $Dir
        $cursor = $root
        foreach ($part in ((Get-RelPath $Dir) -split '/')) {
            $cursor = Join-Path $cursor $part
            $item = Get-Item -LiteralPath $cursor -Force -ErrorAction SilentlyContinue
            if ($item -and ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw "links are not supported: $(Get-RelPath $cursor)" }
        }
        if (-not (Test-Path -LiteralPath $Dir)) { return }
        if (-not (Test-Path -LiteralPath $Dir -PathType Container)) { throw "not a directory: $(Get-RelPath $Dir)" }
        foreach ($item in Get-ChildItem -LiteralPath $Dir -Recurse -Force) {
            if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "links are not supported: $(Get-RelPath $item.FullName)" }
        }
    }

    # --- 字节级读写 ---
    function Read-Utf8([string]$Path) {
        $bytes = [IO.File]::ReadAllBytes($Path)
        $bom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
        $offset = if ($bom) { 3 } else { 0 }
        try { $text = $strictUtf8.GetString($bytes, $offset, $bytes.Length - $offset) }
        catch { throw "not valid UTF-8: $(Get-RelPath $Path)" }
        if ($text.Contains([char]0)) { throw "not a text file (contains NUL): $(Get-RelPath $Path)" }
        [pscustomobject]@{ Text = $text; Bom = $bom }
    }
    function ConvertTo-Bytes([string]$Text, [bool]$Bom) {
        $body = $strictUtf8.GetBytes($Text)
        if (-not $Bom) { return , $body }
        $bytes = [byte[]]::new($body.Length + 3)
        $bytes[0] = 0xEF; $bytes[1] = 0xBB; $bytes[2] = 0xBF
        [Array]::Copy($body, 0, $bytes, 3, $body.Length)
        return , $bytes
    }

    # 期望输出：完整路径 → 字节。Windows 文件系统不区分大小写，键也不区分
    $outputs = [Collections.Generic.Dictionary[string, byte[]]]::new([StringComparer]::OrdinalIgnoreCase)
    function Add-Output([string]$Path, [byte[]]$Bytes) {
        Assert-InRoot $Path
        $full = [IO.Path]::GetFullPath($Path)
        if ($outputs.ContainsKey($full)) { throw "duplicate output: $(Get-RelPath $full)" }
        $outputs[$full] = $Bytes
    }

    # === 副本与 D2 ===
    function Add-ModeCopy([string]$Source, [string]$Target, [string]$Mode) {
        $src = Read-Utf8 $Source
        $text = $src.Text -creplace 'MapExtPDX\.ModeA\b', "MapExtPDX.Mode$Mode"
        if ($text -cmatch 'MapExtPDX\.ModeA\b') { throw "ModeA reference left after replacement: $(Get-RelPath $Source)" }
        Add-Output $Target (ConvertTo-Bytes $text $src.Bom)
    }

    $targetRoots = [Collections.Generic.List[string]]::new()
    foreach ($family in $families) {
        $source = Join-Path $root "$($family.Name)/$($modes.A.Dir)"
        if (-not (Test-Path -LiteralPath $source -PathType Container)) { throw "source directory missing: $(Get-RelPath $source)" }
        Assert-SafeTree $source
        $files = @(Get-ChildItem -LiteralPath $source -File -Recurse -Force | Where-Object Extension -EQ '.cs' | Sort-Object FullName)
        if (-not $files.Count) { throw "source directory has no .cs files: $(Get-RelPath $source)" }
        foreach ($mode in $family.Targets) {
            $target = Join-Path $root "$($family.Name)/$($modes[$mode].Dir)"
            Assert-SafeTree $target
            $targetRoots.Add($target)
            foreach ($file in $files) {
                Add-ModeCopy $file.FullName (Join-Path $target ([IO.Path]::GetRelativePath($source, $file.FullName))) $mode
            }
        }
    }

    # D2 的标准来源在 CellSystems；EcoSystems/ModeA 里出现 D2 说明有人改错了地方，不能当来源传播
    $wrongD2 = Join-Path $root "EcoSystems/$($modes.A.Dir)/D2_LandValueSystemMod.cs"
    if (Test-Path -LiteralPath $wrongD2) {
        throw "EcoSystems/$($modes.A.Dir) must not contain a D2 copy; move its changes to CellSystems/$($modes.A.Dir)/CellMapClosed/LandValueSystemMod.cs"
    }
    $landValue = Join-Path $root "CellSystems/$($modes.A.Dir)/CellMapClosed/LandValueSystemMod.cs"
    if (-not (Test-Path -LiteralPath $landValue -PathType Leaf)) { throw "D2 source missing: $(Get-RelPath $landValue)" }
    Add-ModeCopy $landValue (Join-Path $root "EcoSystems/$($modes.E.Dir)/D2_LandValueSystemMod.cs") 'E'

    # === XCellMapSystemRe（-replace 不区分大小写） ===
    $coreDir = Join-Path $root 'ReBurstCore'
    Assert-SafeTree $coreDir
    $template = Join-Path $coreDir 'XCellMapSystemRe.cs'
    if (-not (Test-Path -LiteralPath $template -PathType Leaf)) { throw "template missing: $(Get-RelPath $template)" }
    $templateText = (Read-Utf8 $template).Text
    $ignoreCase = [Text.RegularExpressions.RegexOptions]::IgnoreCase
    # 每处替换必须恰好命中一次：模板改了行首而生成器没跟上时，直接失败而不是静默漏换
    function Invoke-ReplaceOnce([string]$Text, [string]$Pattern, [string]$Replacement) {
        $hits = [regex]::Matches($Text, $Pattern, $ignoreCase).Count
        if ($hits -ne 1) { throw "template pattern must match exactly once (got $hits): $Pattern" }
        return $Text -replace $Pattern, $Replacement
    }
    foreach ($mode in $modes.Keys) {
        $name = "Mode$mode"
        $size = $modes[$mode].MapSize
        $text = $templateText
        $text = Invoke-ReplaceOnce $text '(?m)^// \[SOURCE\].*$' '// [AUTO-GENERATED] 由 ReBurstSystem/ModeSources.Generate.ps1 从 XCellMapSystemRe.cs 生成，不入版控；请改模板，勿手动编辑'
        $text = Invoke-ReplaceOnce $text '(?m)^// 构建时由.*$' "// Mode: $name, kMapSize: $size"
        $text = Invoke-ReplaceOnce $text '(?m)^// 注意: 本文件.*$' '// kTextureSize 倍率由 CellMapTextureSizeMultiplier 在编译时自动计算'
        $text = Invoke-ReplaceOnce $text 'namespace MapExtPDX\.MapExt\.ReBurstSystemCore' "namespace MapExtPDX.$name"
        $text = Invoke-ReplaceOnce $text '(public const int kMapSize\s*=\s*)\d+(;)' "`${1}$size`${2}"
        $text = $text -replace "`r`n", "`n"
        Add-Output (Join-Path $coreDir "XCellMapSystemRe_$name.cs") (ConvertTo-Bytes $text $true)
    }

    # === 与磁盘比对：待写、过期文件、过期目录 ===
    $writes = [Collections.Generic.List[string]]::new()
    $unchanged = 0
    foreach ($entry in $outputs.GetEnumerator()) {
        if ([IO.File]::Exists($entry.Key) -and [Linq.Enumerable]::SequenceEqual([IO.File]::ReadAllBytes($entry.Key), $entry.Value)) { $unchanged++ }
        else { $writes.Add($entry.Key) }
    }

    $deletes = [Collections.Generic.List[string]]::new()
    foreach ($target in $targetRoots) {
        if (-not (Test-Path -LiteralPath $target)) { continue }
        foreach ($file in Get-ChildItem -LiteralPath $target -File -Recurse -Force) {
            if (-not $outputs.ContainsKey($file.FullName)) { $deletes.Add($file.FullName) }
        }
    }
    # ReBurstCore 只管 XCellMapSystemRe_Mode?.cs，同目录的其他文件都是手写源码
    foreach ($file in Get-ChildItem -LiteralPath $coreDir -File -Force -Filter 'XCellMapSystemRe_Mode*.cs') {
        if ($file.Name -match '^XCellMapSystemRe_Mode.\.cs$' -and -not $outputs.ContainsKey($file.FullName)) { $deletes.Add($file.FullName) }
    }

    $keptDirs = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($path in $outputs.Keys) {
        $dir = [IO.Path]::GetDirectoryName($path)
        while ($dir.Length -gt $root.Length -and $keptDirs.Add($dir)) { $dir = [IO.Path]::GetDirectoryName($dir) }
    }
    $staleDirs = [Collections.Generic.List[string]]::new()
    foreach ($target in $targetRoots) {
        if (-not (Test-Path -LiteralPath $target)) { continue }
        foreach ($dir in Get-ChildItem -LiteralPath $target -Directory -Recurse -Force) {
            if (-not $keptDirs.Contains($dir.FullName)) { $staleDirs.Add($dir.FullName) }
        }
    }

    $deleteCount = $deletes.Count + $staleDirs.Count
    if ($Check) {
        foreach ($path in $writes) { Write-Output "  would write: $(Get-RelPath $path)" }
        foreach ($path in $deletes) { Write-Output "  would delete: $(Get-RelPath $path)" }
        foreach ($dir in $staleDirs) { Write-Output "  would delete dir: $(Get-RelPath $dir)" }
        Write-Output "ModeSources (check): $($writes.Count) to write, $deleteCount to delete, $unchanged unchanged"
        exit ([int]($writes.Count + $deleteCount -gt 0))
    }

    # === 写入（全部检查已通过） ===
    foreach ($path in $writes) {
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($path)) | Out-Null
        [IO.File]::WriteAllBytes($path, $outputs[$path])
        Write-Output "  written: $(Get-RelPath $path)"
    }
    foreach ($path in $deletes) {
        [IO.File]::Delete($path)
        Write-Output "  deleted: $(Get-RelPath $path)"
    }
    # 由深到浅逐个删除；Directory.Delete 不递归，目录非空时报错而不是连带删除
    foreach ($dir in ($staleDirs | Sort-Object Length -Descending)) {
        [IO.Directory]::Delete($dir)
        Write-Output "  deleted dir: $(Get-RelPath $dir)"
    }
    Write-Output "ModeSources: $($writes.Count) written, $deleteCount deleted, $unchanged unchanged"
} catch {
    # MSBuild 的 Exec 能识别"来源 : error : 消息"格式，并把它报告为建置错误
    [Console]::Error.WriteLine("ModeSources.Generate.ps1 : error : $($_.Exception.Message)")
    exit 1
}
