using System.Diagnostics;
using EvidenceHub.Updates;

namespace EvidenceHub;
public sealed class HubWindow : Form
{
    readonly Color forest = Color.FromArgb(22, 48, 39), cream = Color.FromArgb(246, 243, 234);
    readonly FlowLayoutPanel content = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(32) };
    readonly SettingsStore store = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EvidenceNeuterHub", "settings.json"));
    HubSettings settings = new();
    Installation? installation;
    string page = "Discover", problem = "";
    bool settingsReadable = true;
    readonly DownloadClient downloads = new();
    readonly InstallManager manager;
    CancellationTokenSource? operation;
    VerifiedRelease? available;
    string transferText = "No active download.";
    Label? transferLabel;
    ProgressBar? transferBar;
    int transferPercent;
    readonly Dictionary<string, Button> navigation = new();
    static readonly (string Name, string Description, string Mark)[] products = {
        ("Evidence Lab", "Your local voice, speech and audio studio.", "LAB"),
        ("Evidence Bot", "A wake-word companion for your device.", "BOT"),
        ("Evidence Academy", "Learn through explorable worlds and practice.", "ACA"),
        ("Evidence Flow", "Connect ideas into useful automations.", "FLW"),
        ("Evidence Council", "Research, write and review with a team of AI roles.", "COU")
    };
    public HubWindow()
    {
        var data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EvidenceNeuterHub");
        manager = new InstallManager(Path.Combine(data, "Genesis"), ReleaseTrust.PublicKey(), downloads,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EvidenceGenesis"));
        FormClosing += (_, e) => { if (operation is not null) { operation.Cancel(); e.Cancel = true; MessageBox.Show(this, "Canceling the operation. Wait for cleanup before closing."); } };
        FormClosed += (_, _) => downloads.Dispose();
        Text = "Evidence Neuter Hub"; Size = new Size(1080, 760); MinimumSize = new Size(800, 600);
        BackColor = cream; Font = new Font("Segoe UI", 10); StartPosition = FormStartPosition.CenterScreen;
        var sidebar = new FlowLayoutPanel { Dock = DockStyle.Left, Width = 210, BackColor = forest, Padding = new Padding(20, 32, 20, 20), FlowDirection = FlowDirection.TopDown, WrapContents = false };
        var brand = new Label { Text = "EVIDENCE\nNEUTER", ForeColor = cream, Font = new Font(Font.FontFamily, 20, FontStyle.Bold), Size = new Size(170, 100) };
        sidebar.Controls.Add(brand);
        foreach (var name in new[] { "Discover", "My Library", "Downloads", "Settings" })
        {
            var button = new Button { Text = name, Size = new Size(170, 48), FlatStyle = FlatStyle.Flat, ForeColor = cream, BackColor = forest, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 4, 0, 4) };
            button.FlatAppearance.BorderSize = 0; button.Click += (_, _) => { page = name; Render(); }; navigation.Add(name, button); sidebar.Controls.Add(button);
        }
        sidebar.Controls.Add(new Label { Text = "HUB 0.2.0 • PREVIEW\nLocal first. Built with intent.", ForeColor = cream, Size = new Size(170, 70), Margin = new Padding(0, 60, 0, 0) });
        Controls.Add(content); Controls.Add(sidebar);
        try { settings = store.Load(); } catch (Exception ex) { settingsReadable = false; problem = "Could not read settings. Your file is preserved. Close the Hub and repair or rename settings.json. " + ex.Message; }
        RefreshInstallation(); Render();
        content.SizeChanged += (_, _) => { foreach (Control c in content.Controls) c.Width = Math.Max(440, content.ClientSize.Width - 80); };
    }
    void RefreshInstallation()
    {
        installation = null;
        try
        {
            var state = manager.State();
            if (state.Current is not null) { installation = Genesis.Detect(manager.DirectoryFor(state.Current)); return; }
        }
        catch (Exception ex) { problem = "Managed installation needs attention: " + ex.Message; return; }
        if (settings.GenesisFolder is null) return;
        try { installation = Genesis.Detect(settings.GenesisFolder); problem = ""; }
        catch (Exception ex) { problem = "Genesis installation needs attention: " + ex.Message; }
    }
    void Copy(string text, int size = 10, bool bold = false)
    {
        content.Controls.Add(new Label { Text = text, AutoSize = false, Size = new Size(Math.Max(440, content.ClientSize.Width - 80), size > 20 ? 65 : 64), ForeColor = forest, Font = new Font(Font.FontFamily, size, bold ? FontStyle.Bold : FontStyle.Regular), Margin = new Padding(0, 0, 0, 12) });
    }
    Button Action(string text, EventHandler action)
    {
        var button = new Button { Text = text, Size = new Size(240, 42), BackColor = forest, ForeColor = cream, FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 0, 0, 16) };
        button.Click += action; button.Enabled = operation is null; content.Controls.Add(button); return button;
    }
    void Render()
    {
        content.SuspendLayout();
        foreach (Control control in content.Controls.Cast<Control>().ToArray()) control.Dispose();
        foreach (var item in navigation) item.Value.BackColor = item.Key == page ? Color.FromArgb(49, 77, 62) : forest;
        Copy(page, 30, true);
        if (problem.Length > 0) Copy(problem);
        if (page is "Discover" or "My Library")
        {
            if (page == "Discover") Copy("Independent intelligence. One home for what comes next.");
            if (page == "Discover") content.Controls.Add(new GenesisArtwork());
            Copy("GEN / Evidence Genesis", 22, true);
            Copy("A teachable 2.5D AI companion game. Bring your existing Windows installation into the Hub.");
            string version = "Imported • version unverified";
            try { if (manager.State().Current is { } current) version = "Managed Genesis " + Metadata.Verify(current.Envelope, ReleaseTrust.PublicKey(), offlineInstalled: true).Manifest.Version; }
            catch (Exception ex) { problem = ex.Message; }
            Copy(installation is null ? "Not connected • Development preview" : version + "\n" + installation.Folder);
            if (installation is not null)
            {
                if (!installation.HasRuntime || !installation.HasModel) Copy("AI files are incomplete: keep brain/llama-server.exe and model/genesis.gguf beside the game. Template mode may remain available.");
                Action("Play Genesis", Play);
            }
            Action(installation is null ? "Locate Genesis folder…" : "Import Genesis folder…", Locate).Enabled = settingsReadable && operation is null;
            Action("Check Genesis releases", CheckRelease);
            if (available is not null) Action("Install / update Genesis " + available.Manifest.Version, InstallRelease);
            if (page == "Discover")
                foreach (var product in products)
                {
                    Copy(product.Mark + " / " + product.Name, 16, true);
                    Copy(product.Description + "\nIn development • Installation unavailable");
                }
            else if (installation is null) Copy("Your library is empty. Locate an extracted Genesis installation to play offline.");
        }
        else if (page == "Downloads")
        {
            Copy("Genesis installation & updates", 22, true);
            Copy("Signed public releases only. Existing games remain playable offline. Game, AI runtime and model packages are verified separately.");
            transferLabel = new Label { Text = transferText, Width = Math.Max(440, content.ClientSize.Width - 80), Height = 64, ForeColor = forest };
            content.Controls.Add(transferLabel);
            transferBar = new ProgressBar { Width = 440, Height = 18, Value = transferPercent, AccessibleName = "Current component download progress" };
            content.Controls.Add(transferBar);
            if (operation is not null) Action("Cancel operation", (_, _) => operation?.Cancel()).Enabled = true;
            else
            {
                Action("Check Genesis releases", CheckRelease);
                if (available is not null)
                {
                    Copy("Available: Genesis " + available.Manifest.Version + "\nPackages: " + string.Join(", ", available.Manifest.Packages.Select(p => p.Kind + " " + (p.Size / 1048576d).ToString("N1") + " MiB")));
                    Copy("Verified unchanged installed or imported files are reused. Staging retains the previous version; sufficient space is required for copies. Close Genesis before installation.");
                    Action("Install / update " + available.Manifest.Version, InstallRelease);
                }
                try { if (manager.State().Previous is not null) Action("Roll back to previous version", Rollback); } catch (Exception ex) { Copy(ex.Message); }
            }
        }
        else
        {
            Copy("Local preferences", 22, true);
            var minimize = new CheckBox { Text = "Minimize Hub after starting Genesis", Checked = settings.MinimizeOnPlay, AutoSize = true, Enabled = settingsReadable && operation is null, Margin = new Padding(0, 0, 0, 24) };
            minimize.CheckedChanged += (_, _) => Persist(settings with { MinimizeOnPlay = minimize.Checked }); content.Controls.Add(minimize);
            Copy("Settings are stored in your Windows local application data. Game files and Genesis saves remain untouched. No account or network connection is required.");
            Action("Recheck installation", (_, _) => { RefreshInstallation(); Render(); });
            if (settings.GenesisFolder is not null) Action("Remove from library", (_, _) => { if (Persist(settings with { GenesisFolder = null })) { installation = null; problem = ""; RefreshInstallation(); Render(); } }).Enabled = settingsReadable && operation is null;
            Copy("Removing an imported library entry never deletes files. Managed versions and save backups remain in local application data. Hub self-updates are not implemented.");
        }
        content.ResumeLayout();
    }
    bool Persist(HubSettings next)
    {
        try { store.Save(next); settings = next; return true; }
        catch (Exception ex) { MessageBox.Show(this, "Could not save settings. Previous preferences remain active.\n" + ex.Message, "Settings error", MessageBoxButtons.OK, MessageBoxIcon.Error); return false; }
    }
    void Locate(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog { Description = "Choose the Genesis folder containing EvidenceGenesis.exe", UseDescriptionForTitle = true, ShowNewFolderButton = false };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var found = Genesis.Detect(dialog.SelectedPath);
            if (MessageBox.Show(this, "Only connect software you trust. The Hub detects a Windows x64 executable but does not authenticate this installation.\n\n" + found.Executable, "Connect Genesis?", MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK) return;
            if (!Persist(settings with { GenesisFolder = found.Folder })) return;
            installation = found; problem = "";
            if (manager.State().Current is not null) problem = "Import folder saved for verified file reuse. Play continues to use the managed installation.";
            RefreshInstallation(); Render();
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Could not connect Genesis", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
    async void Play(object? sender, EventArgs e)
    {
        await RunOperation(async token =>
        {
            transferText = "Verifying installed files before launch…";
            using var process = await manager.LaunchAsync(settings.GenesisFolder, token);
            if (settings.MinimizeOnPlay) WindowState = FormWindowState.Minimized;
            transferText = "Genesis process started. Game initialization is managed by Genesis.";
        });
    }
    async Task RunOperation(Func<CancellationToken, Task> action)
    {
        if (operation is not null) return;
        operation = new CancellationTokenSource(); Render();
        try { await action(operation.Token); }
        catch (OperationCanceledException) { transferText = "Canceled. The active installation was not changed unless activation had already completed."; }
        catch (Exception ex) { problem = ex.Message; transferText = "Operation failed: " + ex.Message; }
        finally { operation.Dispose(); operation = null; RefreshInstallation(); Render(); }
    }
    async void CheckRelease(object? sender, EventArgs e)
    {
        page = "Downloads";
        await RunOperation(async token =>
        {
            available = null;
            // Fail closed before making a network request when no production key is pinned.
            if (string.IsNullOrWhiteSpace(ReleaseTrust.PublicKey())) throw new IOException("Release signing public key is not configured in this build. Existing imported games remain playable.");
            transferText = "Checking signed public release metadata…";
            var envelope = await downloads.MetadataAsync(token);
            available = manager.VerifyNew(envelope);
            transferText = "Verified release metadata for Genesis " + available.Manifest.Version + ". No packages downloaded yet.";
            problem = "";
        });
    }
    async void InstallRelease(object? sender, EventArgs e)
    {
        if (available is null) return;
        var envelope = available.Envelope;
        page = "Downloads";
        await RunOperation(async token =>
        {
            var progress = new Progress<TransferProgress>(p =>
            {
                if (token.IsCancellationRequested || operation is null) return;
                transferText = p.Component + " • " + p.Phase + (p.Total > 0 ? " • " + (p.Bytes / 1048576d).ToString("N1") + " / " + (p.Total / 1048576d).ToString("N1") + " MiB" : "");
                transferPercent = p.Total > 0 ? (int)Math.Clamp(p.Bytes * 100 / p.Total, 0, 100) : 0;
                if (transferLabel is { IsDisposed: false }) transferLabel.Text = transferText;
                if (transferBar is { IsDisposed: false }) transferBar.Value = transferPercent;
            });
            await manager.InstallAsync(envelope, settings.GenesisFolder, progress, token);
            transferText = "Genesis installed and file integrity verified. Play is available offline.";
            problem = ""; available = null;
        });
    }
    async void Rollback(object? sender, EventArgs e)
    {
        if (MessageBox.Show(this, "Switch to the previous verified game version? Saves will be backed up and preserved, not restored or deleted. Rollback is limited to the same save schema.", "Roll back Genesis", MessageBoxButtons.OKCancel) != DialogResult.OK) return;
        await RunOperation(async token => { await manager.RollbackAsync(token); transferText = "Previous version activated. Saves preserved."; problem = ""; });
    }
}
