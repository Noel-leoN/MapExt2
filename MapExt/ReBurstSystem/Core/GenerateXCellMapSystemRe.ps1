param (
    [switch]$Force
)

$templatePath = Join-Path $PSScriptRoot "XCellMapSystemRe.Template.cs"

if (-Not (Test-Path $templatePath)) {
    Write-Error "Template file not found: $templatePath"
    exit 1
}

$templateContent = Get-Content $templatePath -Raw
$templateLastWrite = (Get-Item $templatePath).LastWriteTime

$modes = @(
    @{
        Dir       = "..\ReBurstCellSystem\ReBurstJobPak_ModeA_57km"
        Namespace = "ReBurstSystemModeA"
        MapSize   = "57344"
    },
    @{
        Dir       = "..\ReBurstCellSystem\ReBurstJobPak_ModeB_28km"
        Namespace = "ReBurstSystemModeB"
        MapSize   = "28672"
    },
    @{
        Dir       = "..\ReBurstCellSystem\ReBurstJobPak_ModeC_114km"
        Namespace = "ReBurstSystemModeC"
        MapSize   = "114688"
    },
    @{
        Dir       = "..\ReBurstEcoSystem\EcoSystemModeA_57km"
        Namespace = "ReBurstEcoSystemModeA"
        MapSize   = "57344"
    },
    @{
        Dir       = "..\ReBurstEcoSystem\EcoSystemModeB_28km"
        Namespace = "ReBurstEcoSystemModeB"
        MapSize   = "28672"
    },
    @{
        Dir       = "..\ReBurstEcoSystem\EcoSystemModeC_114km"
        Namespace = "ReBurstEcoSystemModeC"
        MapSize   = "114688"
    },
    @{
        Dir       = "..\ReBurstEcoSystem\EcoSystemModeE_vanilla"
        Namespace = "ReBurstEcoSystemModeE"
        MapSize   = "14336"
    }
)

$generatedCount = 0

foreach ($mode in $modes) {
    $targetDir = Join-Path $PSScriptRoot $mode.Dir
    $targetPath = Join-Path $targetDir "XCellMapSystemRe.cs"
    
    if (-Not $Force -and (Test-Path $targetPath)) {
        $targetLastWrite = (Get-Item $targetPath).LastWriteTime
        if ($targetLastWrite -gt $templateLastWrite) {
            continue
        }
    }
    
    if (-Not (Test-Path $targetDir)) {
        New-Item -ItemType Directory -Force -Path $targetDir | Out-Null
    }
    
    $content = $templateContent -replace "__NAMESPACE__", $mode.Namespace -replace "__KMAPSIZE__", $mode.MapSize
    
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
