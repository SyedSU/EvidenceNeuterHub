$ErrorActionPreference = 'Stop'
$buildStage = 'initialization'
trap {
  $detail = ("Stage: $buildStage | " + $_.Exception.Message + " | line " + $_.InvocationInfo.ScriptLineNumber).Replace('%','%25').Replace("`r",'%0D').Replace("`n",'%0A')
  Write-Output "::error title=Hub build failure::$detail"
  throw
}
Set-Location "$PSScriptRoot/.."
New-Item -ItemType Directory artifacts -Force | Out-Null
$buildStage = 'core tests'
dotnet run --project tests/CoreTests.csproj -c Release 2>&1 | Tee-Object artifacts/core-tests.log
if ($LASTEXITCODE) { throw 'Tests failed' }
$buildStage = 'running-game fixture'
dotnet publish tests/RunningGame/RunningGame.csproj -c Release -r win-x64 --self-contained false -o artifacts/gamefixture
if ($LASTEXITCODE) { throw 'Running-game fixture publish failed' }
$buildStage = 'updater tests'
dotnet run --project tests/UpdaterTests/UpdaterTests.csproj -c Release 2>&1 | Tee-Object -FilePath artifacts/updater-tests.log -Variable updaterOutput
if ($LASTEXITCODE) {
  $detail = (($updaterOutput | Select-Object -Last 20) -join ' | ').Replace('%', '%25').Replace("`r", '%0D').Replace("`n", '%0A')
  Write-Output "::error title=Updater test failure::$detail"
  throw 'Updater tests failed'
}
$buildStage = 'release tool'
dotnet build tools/release/ReleaseTool.csproj -c Release
if ($LASTEXITCODE) { throw 'Release tool build failed' }
if ($env:GENESIS_RELEASE_PUBLIC_KEY_PEM) { Set-Content -Path release-public.pem -Value $env:GENESIS_RELEASE_PUBLIC_KEY_PEM }
if ($env:HUB_RELEASE_PUBLIC_KEY_PEM) { Set-Content hub-release-public.pem $env:HUB_RELEASE_PUBLIC_KEY_PEM }
if ($env:HUB_PUBLISHER_SHA256) { Set-Content hub-publisher.txt $env:HUB_PUBLISHER_SHA256 }
if ($env:CATALOG_RELEASE_PUBLIC_KEY_PEM) { Set-Content catalog-public.pem $env:CATALOG_RELEASE_PUBLIC_KEY_PEM }
if ($env:CATALOG_URL) { Set-Content catalog-url.txt $env:CATALOG_URL }
$buildStage = 'Hub publish'
dotnet publish src/Hub.csproj -c Release -r win-x64 --self-contained true -o artifacts/app
if ($LASTEXITCODE) { throw 'Publish failed' }
$buildStage = 'bootstrap publish'
dotnet publish tools/bootstrap/Bootstrap.csproj -c Release -r win-x64 --self-contained true -o artifacts/bootstrap
if ($LASTEXITCODE) { throw 'Bootstrapper publish failed' }
Copy-Item artifacts/bootstrap/EvidenceHubBootstrap.exe artifacts/app/EvidenceHubBootstrap.exe
$root = (Resolve-Path artifacts/app).Path
$lines = Get-ChildItem $root -File -Recurse | ForEach-Object { 'Delete "$INSTDIR\' + $_.FullName.Substring($root.Length + 1) + '"' }
$dirs = Get-ChildItem $root -Directory -Recurse | Sort-Object { $_.FullName.Length } -Descending | ForEach-Object { 'RMDir "$INSTDIR\' + $_.FullName.Substring($root.Length + 1) + '"' }
@($lines) + @($dirs) | Set-Content installer/packaged-uninstall.nsh
$buildStage = 'NSIS'
& "${env:ProgramFiles(x86)}\NSIS\makensis.exe" /WX installer/Hub.nsi
if ($LASTEXITCODE) { throw 'Installer failed' }
Get-FileHash artifacts/EvidenceNeuterHub-Setup-0.3.0.exe -Algorithm SHA256 | Format-List | Out-File artifacts/SHA256.txt
