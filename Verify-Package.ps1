param([Parameter(Mandatory)][string]$Path)
$ErrorActionPreference = 'Stop'
$package = (Resolve-Path -LiteralPath $Path).Path
foreach ($name in @('net.tnrd.zeepkist.medaltimes.dll', 'StrawberryShake.Transport.Http.dll',
    'StrawberryShake.Core.dll', 'Microsoft.Extensions.Http.dll', 'System.Memory.dll', 'System.ValueTuple.dll')) {
    if (!(Test-Path -LiteralPath (Join-Path $package $name))) { throw "Package missing $name" }
}
foreach ($pattern in @('ZeepSDK.dll', 'Zeepkist*.dll', 'Unity*.dll', 'BepInEx*.dll', '*WebSocket*.dll', 'medaltimes-ui')) {
    if (Get-ChildItem -LiteralPath $package -Filter $pattern -File) { throw "Package contains excluded files: $pattern" }
}
$assembly = [System.Reflection.Assembly]::LoadFile((Join-Path $package 'net.tnrd.zeepkist.medaltimes.dll'))
foreach ($name in @('Expert', 'Apex')) {
    $resource = "TNRD.Zeepkist.MedalTimes.RuntimeAssets.Medals.MED_$name.png"
    if ($assembly.GetManifestResourceNames() -notcontains $resource) { throw "Package missing embedded $name medal" }
}
Write-Output 'Package verified: HTTP dependencies, embedded medals, no game/SDK/WebSocket assemblies.'
