$ErrorActionPreference = 'Stop'
Set-Location "$PSScriptRoot/.."
$target = Join-Path $env:TEMP ('hub-install-test-' + [guid]::NewGuid())
$exe = (Resolve-Path artifacts/EvidenceNeuterHub-Setup-0.2.0.exe).Path
$p = Start-Process $exe -ArgumentList @('/S', "/D=$target") -Wait -PassThru
if ($p.ExitCode -ne 0 -or !(Test-Path "$target/EvidenceNeuterHub.exe")) { throw 'Install failed' }
$shortcut = Join-Path ([Environment]::GetFolderPath('StartMenu')) 'Programs/Evidence Neuter/Hub.lnk'
if (!(Test-Path $shortcut)) { throw 'Start Menu shortcut missing' }
$hub = $null
try {
  $hub = Start-Process "$target/EvidenceNeuterHub.exe" -PassThru
  $ready = $false
  for ($i=0; $i -lt 30; $i++) {
    Start-Sleep -Milliseconds 500
    $hub.Refresh()
    if ($hub.HasExited) { throw 'Hub exited before showing window' }
    if ($hub.MainWindowHandle -ne 0) { $ready = $true; break }
  }
  if (!$ready) { throw 'Hub window did not appear' }
  Add-Type -AssemblyName System.Windows.Forms
  Add-Type -AssemblyName System.Drawing
  $bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
  $bitmap = New-Object System.Drawing.Bitmap($bounds.Width, $bounds.Height)
  $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
  $graphics.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)
  $bitmap.Save((Join-Path (Get-Location) 'artifacts/windows-smoke.png'))
  $graphics.Dispose(); $bitmap.Dispose()
} finally {
  if ($hub -and !$hub.HasExited) { Stop-Process -Id $hub.Id }
}
# An unrelated file must survive uninstall. Genesis saves are never in this directory.
Set-Content "$target/preserve-user-file.txt" 'preserve'
$p = Start-Process "$target/Uninstall.exe" -ArgumentList '/S' -Wait -PassThru
for ($i=0; $i -lt 30 -and (Test-Path "$target/EvidenceNeuterHub.exe"); $i++) { Start-Sleep -Milliseconds 500 }
if (Test-Path "$target/EvidenceNeuterHub.exe") { throw 'Uninstall failed' }
if (!(Test-Path "$target/preserve-user-file.txt")) { throw 'Uninstaller removed unrelated data' }
if (Test-Path $shortcut) { throw 'Uninstaller left shortcut' }
Remove-Item "$target/preserve-user-file.txt"
Write-Output 'PASS: install, Start Menu shortcut, native window startup, uninstall and unrelated-file preservation.'
