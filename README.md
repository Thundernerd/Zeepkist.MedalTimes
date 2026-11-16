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

Both release workflows use `.github/workflows/build-mod.yml`, which runs the GraphQL client check and the tests on the commit being released, then builds and checks the package.

- **Pre-release**: run the `pre-release` workflow with a branch and a label like `1.3.0-beta.1`. The tag and GitHub pre-release are created only after the build passes, at the commit that was built, with the zip attached. The csproj version is set to a four-part number for the mod loader (alpha `.1xx`, beta `.2xx`, rc `.3xx`). Pre-releases never go to mod.io.
- **Stable release**: bump `<Version>` in `Zeepkist.MedalTimes.csproj` and merge it, then publish a GitHub release tagged `vX.Y.Z` at that commit. The workflow fails if the tag isn't plain `X.Y.Z` or doesn't match the csproj. After the build passes, the zip goes to mod.io through the `modio` environment (add required reviewers there to approve uploads by hand).

## Regenerate GraphQL client

```powershell
dotnet tool restore
dotnet graphql update
./Generate-GraphQLClient.ps1
```
