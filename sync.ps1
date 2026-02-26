$baseDir = "d:\CS2.WorkSpace\CS2Mod\A.Mod\MapExt2\MapExtPDX\MapExt\ReBurstSystem\ReBurstEcoSystem"
$sourceFile = "$baseDir\EcoSystemModeA_57km\C1_HouseholdFindPropertySystemMod.cs"
$content = Get-Content $sourceFile -Raw

$modes = @(
    @{ Dir = "EcoSystemModeB_28km"; Namespace = "ReBurstEcoSystemModeB" },
    @{ Dir = "EcoSystemModeC_114km"; Namespace = "ReBurstEcoSystemModeC" },
    @{ Dir = "EcoSystemModeE_vanilla"; Namespace = "ReBurstEcoSystemModeE" }
)

foreach ($mode in $modes) {
    # 1. Update content namespace
    $newContent = $content -replace 'namespace MapExtPDX.MapExt.ReBurstEcoSystemModeA', ("namespace MapExtPDX.MapExt." + $mode.Namespace)
    
    # 2. Delete old A4_ file
    $oldFilePath = "$baseDir\$($mode.Dir)\A4_HouseholdFindPropertySystemMod.cs"
    if (Test-Path $oldFilePath) {
        Remove-Item $oldFilePath -Force
    }
    
    # 3. Save as new C1_ file
    $newFilePath = "$baseDir\$($mode.Dir)\C1_HouseholdFindPropertySystemMod.cs"
    Set-Content -Path $newFilePath -Value $newContent -Encoding UTF8
    Write-Host "Synced: $newFilePath"
}
