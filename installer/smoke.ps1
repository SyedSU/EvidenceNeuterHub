$ErrorActionPreference = 'Stop'
$smokeStage = 'silent installation'
trap { $detail = ("Stage: $smokeStage | " + $_.Exception.Message + " | line " + $_.InvocationInfo.ScriptLineNumber).Replace('%','%25').Replace("`r",'%0D').Replace("`n",'%0A'); Write-Output "::error title=Hub smoke failure::$detail"; throw }

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
using System.Text;
public static class HubCapture {
 [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);
 [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hwnd,IntPtr hdc,uint flags);
 [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd,out Rect rect);
 [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr hwnd,int x,int y,int w,int h,bool repaint);
 [DllImport("user32.dll")] public static extern IntPtr GetDlgItem(IntPtr dialog,int id);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern int GetWindowText(IntPtr window,StringBuilder text,int count);
 [DllImport("user32.dll")] public static extern bool IsWindowEnabled(IntPtr window);
 [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr window,uint message,IntPtr wparam,IntPtr lparam);
 public static string Caption(IntPtr window){var text=new StringBuilder(256);GetWindowText(window,text,text.Capacity);return text.ToString();}
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
$smokeStage = 'standalone bootstrap startup'
# An isolated copy of only the bootstrap EXE must reach our own fail-closed handler.
# Invalid DOTNET_ROOT bindings ensure this child cannot resolve the developer SDK path.
$bootstrapStage=Join-Path $env:TEMP ('hub-bootstrap-test-' + [guid]::NewGuid())
New-Item -ItemType Directory $bootstrapStage | Out-Null
Copy-Item "$target/EvidenceHubBootstrap.exe" "$bootstrapStage/EvidenceHubBootstrap.exe"
$bootstrapInfo=New-Object System.Diagnostics.ProcessStartInfo
$bootstrapInfo.FileName="$bootstrapStage/EvidenceHubBootstrap.exe"
$bootstrapInfo.UseShellExecute=$false
$bootstrapInfo.Environment['DOTNET_ROOT']="$bootstrapStage/no-runtime"
$bootstrapInfo.Environment['DOTNET_ROOT_X64']="$bootstrapStage/no-runtime"
$bootstrapInfo.Environment['DOTNET_MULTILEVEL_LOOKUP']='0'
$boot=[System.Diagnostics.Process]::Start($bootstrapInfo)
try {
 $marker=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'EvidenceNeuterHub/HubUpdates/bootstrap-last-error.json'
 $bootstrapReady=$false
 for($i=0;$i -lt 60;$i++){Start-Sleep -Milliseconds 250;if(Test-Path $marker){$errorRecord=Get-Content $marker -Raw | ConvertFrom-Json;if($errorRecord.processId -eq $boot.Id -and $errorRecord.message -eq 'Invalid Hub handoff.'){$bootstrapReady=$true;break}};if($boot.HasExited){break}}
 if(!$bootstrapReady){throw 'Standalone bootstrap did not reach its own fail-closed handler without SDK bindings'}
} finally {if(!$boot.HasExited){Stop-Process -Id $boot.Id};$boot.Dispose();Remove-Item $bootstrapStage -Recurse -Force}
$smokeStage = 'native pages, resize and focus'
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
# Exercise the real interactive Finish checkbox, not an artificial post-install Start.
$smokeStage='interactive Finish launches Hub after guard release'
$guiTarget=Join-Path $env:TEMP ('hub-finish-test-' + [guid]::NewGuid())
$wizard=Start-Process $exe -ArgumentList "/D=$guiTarget" -PassThru
$finishHub=$null
try {
 $finished=$false
 $wizardTrace=[System.Collections.Generic.HashSet[string]]::new()
 for($i=0;$i -lt 240;$i++) {
  Start-Sleep -Milliseconds 250;$wizard.Refresh()
  if($wizard.HasExited){if(!$finished){throw 'Installer exited before interactive Finish'};break}
  if($wizard.MainWindowHandle -eq 0){continue}
  $ui=[System.Windows.Automation.AutomationElement]::FromHandle($wizard.MainWindowHandle)
  [void]$wizardTrace.Add("window=$($ui.Current.Name),pid=$($ui.Current.ProcessId)")
  # NSIS native dialog buttons did not expose UIAutomation Button providers on the runner.
  # Read the real IDOK control caption and enabled state before invoking its native click.
  $next=[HubCapture]::GetDlgItem($wizard.MainWindowHandle,1)
  $name=[HubCapture]::Caption($next).Replace('&','').Trim()
  $enabled=[HubCapture]::IsWindowEnabled($next)
  [void]$wizardTrace.Add("native IDOK=$name,enabled=$enabled")
  if($enabled -and ($name -match '^Next\s*>?$' -or $name -in @('Install','Finish'))) {
   if($name -eq 'Finish'){$finished=$true}
   [void][HubCapture]::SetForegroundWindow($wizard.MainWindowHandle)
   [void][HubCapture]::SendMessage($next,0xF5,[IntPtr]::Zero,[IntPtr]::Zero)
  }
 }
 if(!$finished){
  if($wizard.MainWindowHandle -ne 0){Capture-Hub $wizard.MainWindowHandle 'installer-timeout'}
  $desktop=[System.Windows.Automation.AutomationElement]::RootElement.FindAll([System.Windows.Automation.TreeScope]::Children,[System.Windows.Automation.Condition]::TrueCondition)
  foreach($window in $desktop){[void]$wizardTrace.Add("desktop=$($window.Current.Name),pid=$($window.Current.ProcessId),handle=$($window.Current.NativeWindowHandle)")}
  $trace=$wizardTrace -join ' | '
  $trace | Out-File artifacts/installer-ui-trace.log
  throw "Interactive Finish button was not reached; process=$($wizard.Id),handle=$($wizard.MainWindowHandle); $trace"
 }
 for($i=0;$i -lt 60;$i++) {
  Start-Sleep -Milliseconds 250
  $finishHub=Get-Process -Name EvidenceNeuterHub -ErrorAction SilentlyContinue | Where-Object {$_.Path -eq "$guiTarget\EvidenceNeuterHub.exe"} | Select-Object -First 1
  if($finishHub){$finishHub.Refresh();if($finishHub.MainWindowHandle -ne 0){break}}
 }
 if(!$finishHub -or $finishHub.MainWindowHandle -eq 0){throw 'Finish page did not launch a native Hub window; installation guard may still be held'}
 Capture-Hub $finishHub.MainWindowHandle 'finish-launch'
 $smokeStage='uninstaller refuses running Hub'
 # NSIS _?= must be last: execute the actual uninstaller in place, so its guard exit
 # is observed rather than the temporary-copy launcher exit. The running Hub is real.
 $installedHash=(Get-FileHash "$guiTarget/EvidenceNeuterHub.exe" -Algorithm SHA256).Hash
 $blockedUninstall=Start-Process "$guiTarget/Uninstall.exe" -ArgumentList @('/S',"_?=$guiTarget") -Wait -PassThru
 if($blockedUninstall.ExitCode -eq 0 -or !(Test-Path "$guiTarget/EvidenceNeuterHub.exe")){throw "Uninstaller did not refuse the running Hub guard: exit=$($blockedUninstall.ExitCode), executablePresent=$(Test-Path "$guiTarget/EvidenceNeuterHub.exe")"}
 if((Get-FileHash "$guiTarget/EvidenceNeuterHub.exe" -Algorithm SHA256).Hash -ne $installedHash){throw 'Blocked uninstall changed the installed Hub executable'}
} finally {
 if($finishHub -and !$finishHub.HasExited){Stop-Process -Id $finishHub.Id}
 if(!$wizard.HasExited){Stop-Process -Id $wizard.Id}
}
$smokeStage='interactive installation cleanup'
$p=Start-Process "$guiTarget/Uninstall.exe" -ArgumentList '/S' -Wait -PassThru
for($i=0;$i -lt 30 -and (Test-Path "$guiTarget/EvidenceNeuterHub.exe");$i++){Start-Sleep -Milliseconds 500}
if(Test-Path "$guiTarget/EvidenceNeuterHub.exe"){throw 'Interactive installation cleanup failed'}
Write-Output 'PASS: native installer/bootstrap package, isolated standalone bootstrap without SDK bindings, running-install/uninstall guards, four-page keyboard navigation, resize/focus, genuine interactive Finish launch, uninstall and unrelated-file preservation. Laptop/high-DPI/gameplay acceptance remains separate.'
