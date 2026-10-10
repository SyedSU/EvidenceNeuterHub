Unicode true
!include "MUI2.nsh"
!include "x64.nsh"
Function .onInit
 System::Call 'kernel32::CreateMutexW(p 0, i 0, w "Local\EvidenceNeuterHub-InstallationGuard") p .r0'
 System::Call 'kernel32::WaitForSingleObject(p r0, i 0) i .r1'
 ${If} $1 != 0
 ${AndIf} $1 != 128
  MessageBox MB_OK|MB_ICONSTOP "Close Evidence Neuter Hub before installing. Another Hub or installer holds the installation guard." /SD IDOK
  Abort
 ${EndIf}
 ${IfNot} ${RunningX64}
  MessageBox MB_OK|MB_ICONSTOP "Evidence Neuter Hub requires Windows x64."
  Abort
 ${EndIf}
FunctionEnd
Name "Evidence Neuter Hub"
OutFile "..\artifacts\EvidenceNeuterHub-Setup-0.3.0.exe"
InstallDir "$LOCALAPPDATA\Programs\EvidenceNeuterHub"
InstallDirRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\EvidenceNeuterHub" "InstallLocation"
RequestExecutionLevel user
!define MUI_ABORTWARNING
!define MUI_FINISHPAGE_RUN "$INSTDIR\EvidenceNeuterHub.exe"
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "English"
Section "Hub (required)"
 SectionIn RO
 SetOutPath "$INSTDIR"
 File /r "..\artifacts\app\*"
 WriteUninstaller "$INSTDIR\Uninstall.exe"
 CreateDirectory "$SMPROGRAMS\Evidence Neuter"
 CreateShortcut "$SMPROGRAMS\Evidence Neuter\Hub.lnk" "$INSTDIR\EvidenceNeuterHub.exe"
 WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\EvidenceNeuterHub" "DisplayName" "Evidence Neuter Hub"
 WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\EvidenceNeuterHub" "DisplayVersion" "0.3.0"
 WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\EvidenceNeuterHub" "UninstallString" '$\"$INSTDIR\Uninstall.exe$\"'
 WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\EvidenceNeuterHub" "InstallLocation" "$INSTDIR"
SectionEnd
Section /o "Desktop shortcut"
 CreateShortcut "$DESKTOP\Evidence Neuter Hub.lnk" "$INSTDIR\EvidenceNeuterHub.exe"
SectionEnd
Section "Uninstall"
 Delete "$SMPROGRAMS\Evidence Neuter\Hub.lnk"
 RMDir "$SMPROGRAMS\Evidence Neuter"
 Delete "$DESKTOP\Evidence Neuter Hub.lnk"
 DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\EvidenceNeuterHub"
 ; Delete only packaged files. Never recursively remove user-added files, settings or games.
 !include "packaged-uninstall.nsh"
 Delete "$INSTDIR\Uninstall.exe"
 RMDir "$INSTDIR"
SectionEnd
