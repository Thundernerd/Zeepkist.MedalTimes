$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'GraphQL tool restore failed' }
    dotnet graphql generate
    if ($LASTEXITCODE -ne 0) { throw 'GraphQL generation failed' }
    $path = Join-Path $PSScriptRoot 'GraphQL/MedalTimesClient.Client.cs'
    $content = Get-Content -LiteralPath $path -Raw
    if ($content -notmatch '^extern alias MemoryAlias;') {
        $content = "extern alias MemoryAlias;`n" + $content
    }
    $content = $content -replace 'global::System\.ReadOnlySpan', 'MemoryAlias::System.ReadOnlySpan'
    [System.IO.File]::WriteAllText($path, $content, [System.Text.UTF8Encoding]::new($false))
} finally {
    Pop-Location
}
