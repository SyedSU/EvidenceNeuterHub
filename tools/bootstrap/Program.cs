using EvidenceHub.Updates;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

try {
    if(args.Length!=2 || !int.TryParse(args[0],out var pid) || !long.TryParse(args[1],out var ticks))throw new IOException("Invalid Hub handoff.");
    var updateRoot=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"EvidenceNeuterHub","HubUpdates");
    var stage=Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar);
    if(Path.GetDirectoryName(stage)!=updateRoot || !Guid.TryParseExact(Path.GetFileName(stage),"N",out _))throw new IOException("Bootstrapper is outside the Hub staging directory.");
    InstallManager.RejectLinks(stage);
    InstallManager.RejectLinks(Path.Combine(updateRoot,"update.lock"));
    using var guard=new FileStream(Path.Combine(updateRoot,"update.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
    using var parent=Process.GetProcessById(pid);
    var target=Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Uninstall\EvidenceNeuterHub","InstallLocation",null) as string ?? throw new IOException("Registered Hub installation missing.");
    target=Path.GetFullPath(target);InstallManager.RejectLinks(target);
    if(parent.StartTime.ToUniversalTime().Ticks!=ticks || !string.Equals(parent.MainModule?.FileName,Path.Combine(target,"EvidenceNeuterHub.exe"),StringComparison.OrdinalIgnoreCase))throw new IOException("Originating Hub identity does not match the installed application.");
    // Keep the process handle: PID reuse cannot substitute a different process after this check.
    using var deadline=new CancellationTokenSource(TimeSpan.FromMinutes(5));
    File.WriteAllText(Path.Combine(stage,"parent-verified.ready"),"Parent identity verified; installer not yet executed.");
    await parent.WaitForExitAsync(deadline.Token);
    foreach(var other in Process.GetProcessesByName("EvidenceNeuterHub")) using(other) {if(!other.HasExited && string.Equals(other.MainModule?.FileName,Path.Combine(target,"EvidenceNeuterHub.exe"),StringComparison.OrdinalIgnoreCase))throw new IOException("Another installed Hub is still running. Close it before retrying.");}
    var envelope=File.ReadAllText(Path.Combine(stage,"manifest.json"));
    var manifest=HubUpdate.Verify(envelope,HubUpdate.PublicKey(),HubUpdate.Publisher(),HubUpdate.Highest(updateRoot),Environment.OSVersion.Version.Build);
    var catalogSequencePath=Path.Combine(Path.GetDirectoryName(updateRoot)!,"catalog-sequence.json");InstallManager.RejectLinks(catalogSequencePath);
    var catalogSequence=File.Exists(catalogSequencePath)?System.Text.Json.JsonSerializer.Deserialize<long>(File.ReadAllText(catalogSequencePath)):0;
    var catalog=Catalog.Verify(File.ReadAllText(Path.Combine(stage,"catalog.json")),Catalog.Resource("EvidenceHub.CatalogKey"),catalogSequence);Catalog.MatchHub(catalog,manifest);
    var installer=Path.Combine(stage,"setup.exe");
    // Deny write/delete while authenticating and launching the exact installer bytes.
    using var bytes=new FileStream(installer,FileMode.Open,FileAccess.Read,FileShare.Read);
    await HubUpdate.VerifyInstallerAsync(manifest,installer,deadline.Token);
    // No silent install: the verified installer presents its own user-facing wizard.
    using var child=Process.Start(new ProcessStartInfo(installer){UseShellExecute=false}) ?? throw new IOException("Installer did not start.");
    await child.WaitForExitAsync();
    if(child.ExitCode!=0)throw new IOException("Installer exited with code "+child.ExitCode+". Existing Hub files were not verified as updated; retry with a trusted installer.");
    var installed=FileVersionInfo.GetVersionInfo(Path.Combine(target,"EvidenceNeuterHub.exe")).ProductVersion?.Split('+')[0];
    if(installed is null || Metadata.VersionOf(installed)!=Metadata.VersionOf(manifest.Version))throw new IOException("Installed Hub version does not match the verified release. No completion is claimed.");
    HubUpdate.SaveSequence(updateRoot,manifest.Sequence);
}catch(Exception ex){Native.MessageBox(IntPtr.Zero,"Hub update stopped: "+ex.Message+"\nNo successful update is claimed. Reopen the Hub or obtain a trusted installer.","Hub update",0x10);Environment.ExitCode=1;}
static class Native { [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int MessageBox(IntPtr handle,string text,string caption,uint flags); }
