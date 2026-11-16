# Zeepkist.MedalTimes

Displays Apex, Expert, Author, Gold, Silver and Bronze medal times in Zeepkist.

## Chat commands

- `/at`: resolved Author time with local fallback.
- `/expert`: Expert time or availability.
- `/apex`: Apex time or availability.

## Build and test

```powershell
dotnet restore Zeepkist.MedalTimes.csproj
dotnet build Zeepkist.MedalTimes.csproj --no-restore -c Debug
dotnet build Zeepkist.MedalTimes.csproj --no-restore -c Release
dotnet test Zeepkist.MedalTimes.Tests/Zeepkist.MedalTimes.Tests.csproj -c Release
```

## Package locally

Releases ship a **Debug** build with Mono `.mdb` symbols. To reproduce a release package (needs Mono installed for `pdb2mdb`):

```powershell
dotnet publish Zeepkist.MedalTimes.csproj -c Debug -o artifacts/package
./Verify-Package.ps1 artifacts/package
& 'C:\Program Files\Mono\bin\pdb2mdb.bat' artifacts/package/net.tnrd.zeepkist.medaltimes.dll
Compress-Archive -Path artifacts/package/* -DestinationPath artifacts/MedalTimes.zip -Force
```

## Releasing

Go to **Actions → release → Run workflow** and pick a **bump** (patch, minor or major) and a **stage**. A `stable` release goes to GitHub and mod.io. An `alpha`, `beta` or `rc` stage only creates a GitHub pre-release. The version is worked out from the latest git tag (e.g. `5.3.1` + minor + beta → `5.4.0-beta.1`). Don't edit `<Version>` in the csproj; PRs that do are rejected.

The pipeline lives in [donderjoekel/zeepkist-mod-workflows](https://github.com/donderjoekel/zeepkist-mod-workflows). For this mod it also runs the tests, the GraphQL client check (`Verify-GraphQLClient.ps1`) and `Verify-Package.ps1` before anything is tagged.

## Regenerate GraphQL client

```powershell
dotnet tool restore
dotnet graphql update
./Generate-GraphQLClient.ps1
```
