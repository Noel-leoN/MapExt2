param (
    [switch]$Force
)

$sourcePath = Join-Path $PSScriptRoot "XCellMapSystemRe.cs"

if (-Not (Test-Path $sourcePath)) {
    Write-Error "Source file not found: $sourcePath"
    exit 1
}

$sourceContent = Get-Content $sourcePath -Raw
$sourceLastWrite = (Get-Item $sourcePath).LastWriteTime

$modes = @(
    @{
        Dir       = "."
        Namespace = "ModeA"
        MapSize   = "57344"
    },
    @{
        Dir       = "."
        Namespace = "ModeB"
        MapSize   = "28672"
    },
    @{
        Dir       = "."
        Namespace = "ModeC"
        MapSize   = "114688"
    },
    @{
        Dir       = "."
        Namespace = "ModeA"
        MapSize   = "57344"
    },
    @{
        Dir       = "."
        Namespace = "ModeB"
        MapSize   = "28672"
    },
    @{
        Dir       = "."
        Namespace = "ModeC"
        MapSize   = "114688"
    },
    @{
        Dir       = "."
        Namespace = "ModeE"
        MapSize   = "14336"
    }
)

$generatedCount = 0

foreach ($mode in $modes) {
    $targetDir = Join-Path $PSScriptRoot $mode.Dir
    $targetPath = Join-Path $targetDir "XCellMapSystemRe_$($mode.Namespace).cs"
    
    if (-Not $Force -and (Test-Path $targetPath)) {
        $targetLastWrite = (Get-Item $targetPath).LastWriteTime
        if ($targetLastWrite -gt $sourceLastWrite) {
            continue
        }
    }
    
    if (-Not (Test-Path $targetDir)) {
        New-Item -ItemType Directory -Force -Path $targetDir | Out-Null
    }
    
    # Replace header comment (3 lines starting with // [SOURCE])
    $content = $sourceContent
    $content = $content -replace '(?m)^// \[SOURCE\].*$', "// [AUTO-GENERATED] 由 XCellMapSystemRe.Generate.ps1 从 XCellMapSystemRe.cs 自动生成，请勿手动编辑"
    $content = $content -replace '(?m)^// 构建时由.*$', "// Mode: $($mode.Namespace), kMapSize: $($mode.MapSize)"
    $content = $content -replace '(?m)^// 注意: 本文件.*$', "// kTextureSize 倍率由 CellMapTextureSizeMultiplier 在编译时自动计算"
    
    # Replace namespace
    $content = $content -replace `
        'namespace MapExtPDX\.MapExt\.ReBurstSystemCore', `
        "namespace MapExtPDX.MapExt.$($mode.Namespace)"
    
    # Replace kMapSize value (match the const declaration line)
    $content = $content -replace `
        '(public const int kMapSize\s*=\s*)\d+(;)', `
        "`${1}$($mode.MapSize)`${2}"
    
    # Normalize line endings to CRLF
    $content = $content -replace "`r`n", "`n"
    $content = $content -replace "`n", "`r`n"
    
    [System.IO.File]::WriteAllText($targetPath, $content, [System.Text.Encoding]::UTF8)
    $generatedCount++
    Write-Host "Generated: $targetPath"
}

if ($generatedCount -eq 0) {
    Write-Host "XCellMapSystemRe up to date."
}
else {
    Write-Host "Successfully generated $generatedCount XCellMapSystemRe.cs files."
}



