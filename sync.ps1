$src = "MapExtPDX\MapExt\ReBurstSystem\EcoSystems\ModeA_57km\C1_HouseholdFindPropertySystemMod.cs"
$content = Get-Content $src -Raw -Encoding UTF8
$content.Replace("ModeA_57km", "ModeB_28km") | Set-Content "MapExtPDX\MapExt\ReBurstSystem\EcoSystems\ModeB_28km\C1_HouseholdFindPropertySystemMod.cs" -Encoding UTF8
$content.Replace("ModeA_57km", "ModeC_114km") | Set-Content "MapExtPDX\MapExt\ReBurstSystem\EcoSystems\ModeC_114km\C1_HouseholdFindPropertySystemMod.cs" -Encoding UTF8
$content.Replace("ModeA_57km", "ModeE_vanilla") | Set-Content "MapExtPDX\MapExt\ReBurstSystem\EcoSystems\ModeE_vanilla\C1_HouseholdFindPropertySystemMod.cs" -Encoding UTF8
