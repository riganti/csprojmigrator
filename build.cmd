cd src\CsprojMigrator
dotnet restore
dotnet build
copy bin\debug\net461\CsprojMigrator.exe ..\..\dist\CsprojMigrator.exe