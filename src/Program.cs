namespace EvidenceHub;
internal static class Program
{
    [STAThread] static void Main()
    {
        ApplicationConfiguration.Initialize();
        using var guard = new Mutex(false, @"Local\EvidenceNeuterHub-InstallationGuard");
        bool acquired;
        try { acquired = guard.WaitOne(0); } catch (AbandonedMutexException) { acquired = true; }
        if (!acquired) { MessageBox.Show("Hub is already running or an installation is in progress. Close it or wait for installation to finish.", "Evidence Neuter Hub"); return; }
        try { Application.Run(new HubWindow()); } finally { guard.ReleaseMutex(); }
    }
}
