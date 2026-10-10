$ErrorActionPreference = 'Stop'
Set-Location "$PSScriptRoot/.."
$target = Join-Path $env:TEMP ('hub-install-test-' + [guid]::NewGuid())
$exe = (Resolve-Path artifacts/EvidenceNeuterHub-Setup-0.3.0.exe).Path
$p = Start-Process $exe -ArgumentList @('/S', "/D=$target") -Wait -PassThru
if ($p.ExitCode -ne 0 -or !(Test-Path "$target/EvidenceNeuterHub.exe") -or !(Test-Path "$target/EvidenceHubBootstrap.exe")) { throw 'Install/bootstrap packaging failed' }
$shortcut = Join-Path ([Environment]::GetFolderPath('StartMenu')) 'Programs/Evidence Neuter/Hub.lnk'
if (!(Test-Path $shortcut)) { throw 'Start Menu shortcut missing' }
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class HubCapture {
 [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);
 [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hwnd,IntPtr hdc,uint flags);
 [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd,out Rect rect);
 [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr hwnd,int x,int y,int w,int h,bool repaint);
 public struct Rect {public int Left,Top,Right,Bottom;}
}
'@
function Capture-Hub($window,$name) {
 $rect = New-Object HubCapture+Rect
 if (!( [HubCapture]::GetWindowRect($window,[ref]$rect))) { throw 'Window bounds missing' }
 $bitmap = New-Object System.Drawing.Bitmap(($rect.Right-$rect.Left),($rect.Bottom-$rect.Top))
 $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
 $dc = $graphics.GetHdc()
 try { if (!( [HubCapture]::PrintWindow($window,$dc,2))) { throw 'PrintWindow failed' } } finally { $graphics.ReleaseHdc($dc) }
 $bitmap.Save((Join-Path (Get-Location) "artifacts/windows-$name.png"))
 $graphics.Dispose();$bitmap.Dispose()
}
$hub = $null
try {
 $hub = Start-Process "$target/EvidenceNeuterHub.exe" -PassThru
 $ready = $false
 for ($i=0;$i -lt 30;$i++) { Start-Sleep -Milliseconds 500;$hub.Refresh();if($hub.HasExited){throw 'Hub exited before window'};if($hub.MainWindowHandle -ne 0){$ready=$true;break} }
 if(!$ready){throw 'Hub window missing'}
 $beforeHash=(Get-FileHash "$target/EvidenceNeuterHub.exe" -Algorithm SHA256).Hash
 $blocked=Start-Process $exe -ArgumentList @('/S',"/D=$target") -Wait -PassThru
 if($blocked.ExitCode -eq 0 -or (Get-FileHash "$target/EvidenceNeuterHub.exe" -Algorithm SHA256).Hash -ne $beforeHash){throw 'Installer did not refuse the running Hub installation guard'}
 [HubCapture]::SetForegroundWindow($hub.MainWindowHandle) | Out-Null
 for($i=0;$i -lt 4;$i++) {
  [System.Windows.Forms.SendKeys]::SendWait('^'+($i+1))
  Start-Sleep -Milliseconds 400
  $ui=[System.Windows.Automation.AutomationElement]::FromHandle($hub.MainWindowHandle)
  $page=@('Discover','My Library','Downloads','Settings')[$i]
  $heading=$ui.FindFirst([System.Windows.Automation.TreeScope]::Descendants,(New-Object System.Windows.Automation.AndCondition((New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty,$page)),(New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Text)))))
  if(!$heading){throw "Keyboard navigation failed: $page"}
  $captureName=@('discover','library','downloads','settings')[$i]
  Capture-Hub $hub.MainWindowHandle $captureName
 }
 [HubCapture]::MoveWindow($hub.MainWindowHandle,10,10,800,600,$true) | Out-Null
 [System.Windows.Forms.SendKeys]::SendWait('^1');Start-Sleep -Milliseconds 400
 Capture-Hub $hub.MainWindowHandle 'resized'
 # Tab/Shift+Tab moves through real keyboard-focusable native controls.
 [System.Windows.Forms.SendKeys]::SendWait('{TAB}');Start-Sleep -Milliseconds 100
 if(![System.Windows.Automation.AutomationElement]::FocusedElement){throw 'Keyboard focus missing'}
} finally { if($hub -and !$hub.HasExited){Stop-Process -Id $hub.Id} }
Set-Content "$target/preserve-user-file.txt" 'preserve'
$p=Start-Process "$target/Uninstall.exe" -ArgumentList '/S' -Wait -PassThru
for($i=0;$i -lt 30 -and (Test-Path "$target/EvidenceNeuterHub.exe");$i++){Start-Sleep -Milliseconds 500}
if(Test-Path "$target/EvidenceNeuterHub.exe"){throw 'Uninstall failed'}
if(!(Test-Path "$target/preserve-user-file.txt")){throw 'Uninstaller removed unrelated data'}
if(Test-Path $shortcut){throw 'Uninstaller left shortcut'}
Remove-Item "$target/preserve-user-file.txt"
Write-Output 'PASS: installer/bootstrap package, native keyboard navigation on four pages, resized capture, focus, uninstall and unrelated-file preservation. Screenshot artifacts are Windows captures; real laptop/high-DPI/gameplay acceptance remains required.'
