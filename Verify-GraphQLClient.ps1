# Regenerates the GraphQL client and fails if it differs from the checked-in version.
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'Generate-GraphQLClient.ps1')
git -C $PSScriptRoot diff --exit-code -- GraphQL/MedalTimesClient.Client.cs
if ($LASTEXITCODE -ne 0) { throw 'Generated client differs from checked-in client. Run ./Generate-GraphQLClient.ps1 and commit the result.' }
