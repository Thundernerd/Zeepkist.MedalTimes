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
dotnet publish Zeepkist.MedalTimes.csproj -c Release -o artifacts/package
./Verify-Package.ps1 artifacts/package
Compress-Archive -Path artifacts/package/* -DestinationPath artifacts/MedalTimes.zip -Force
```

## Regenerate GraphQL client

```powershell
./Generate-GraphQLClient.ps1
```

The script restores the tool, generates the client from checked-in schema and query,
then applies GTR's `System.Memory` alias adjustment for Unity's .NET Framework target.
No live server is needed for generation. To refresh schema deliberately:

```powershell
dotnet tool restore
dotnet graphql update
./Generate-GraphQLClient.ps1
```
