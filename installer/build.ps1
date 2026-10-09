$ErrorActionPreference = 'Stop'
Set-Location "$PSScriptRoot/.."
dotnet run --project tests/CoreTests.csproj -c Release
if ($LASTEXITCODE) { throw 'Tests failed' }
dotnet run --project tests/UpdaterTests/UpdaterTests.csproj -c Release
if ($LASTEXITCODE) { throw 'Updater tests failed' }
dotnet build tools/release/ReleaseTool.csproj -c Release
if ($LASTEXITCODE) { throw 'Release tool build failed' }
if ($env:GENESIS_RELEASE_PUBLIC_KEY_PEM) { Set-Content -Path release-public.pem -Value $env:GENESIS_RELEASE_PUBLIC_KEY_PEM }
dotnet publish src/Hub.csproj -c Release -r win-x64 --self-contained true -o artifacts/app
if ($LASTEXITCODE) { throw 'Publish failed' }
$root = (Resolve-Path artifacts/app).Path
$lines = Get-ChildItem $root -File -Recurse | ForEach-Object { 'Delete "$INSTDIR\' + $_.FullName.Substring($root.Length + 1) + '"' }
$dirs = Get-ChildItem $root -Directory -Recurse | Sort-Object { $_.FullName.Length } -Descending | ForEach-Object { 'RMDir "$INSTDIR\' + $_.FullName.Substring($root.Length + 1) + '"' }
@($lines) + @($dirs) | Set-Content installer/packaged-uninstall.nsh
& "${env:ProgramFiles(x86)}\NSIS\makensis.exe" /WX installer/Hub.nsi
if ($LASTEXITCODE) { throw 'Installer failed' }
Get-FileHash artifacts/EvidenceNeuterHub-Setup-0.2.0.exe -Algorithm SHA256 | Format-List | Out-File artifacts/SHA256.txt
