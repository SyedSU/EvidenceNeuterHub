using System.Diagnostics;

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
        sidebar.Controls.Add(new Label { Text = "HUB 0.1.0 • PREVIEW\nLocal first. Built with intent.", ForeColor = cream, Size = new Size(170, 70), Margin = new Padding(0, 60, 0, 0) });
        Controls.Add(content); Controls.Add(sidebar);
        try { settings = store.Load(); } catch (Exception ex) { settingsReadable = false; problem = "Could not read settings. Your file is preserved. Close the Hub and repair or rename settings.json. " + ex.Message; }
        RefreshInstallation(); Render();
        content.SizeChanged += (_, _) => { foreach (Control c in content.Controls) c.Width = Math.Max(440, content.ClientSize.Width - 80); };
    }
    void RefreshInstallation()
    {
        installation = null;
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
        button.Click += action; content.Controls.Add(button); return button;
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
            Copy(installation is null ? "Not connected • Development preview" : "Installation detected • Version not verified\n" + installation.Folder);
            if (installation is not null)
            {
                if (!installation.HasRuntime || !installation.HasModel) Copy("AI files are incomplete: keep brain/llama-server.exe and model/genesis.gguf beside the game. Template mode may remain available.");
                Action("Play Genesis", Play);
            }
            Action(installation is null ? "Locate Genesis folder…" : "Change Genesis folder…", Locate).Enabled = settingsReadable;
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
            Copy("No downloads", 22, true);
            Copy("This foundation connects an already-installed Genesis game. Downloads and updates are not available in this version. Nothing is queued or downloading.");
            Action("Locate existing Genesis…", Locate).Enabled = settingsReadable;
        }
        else
        {
            Copy("Local preferences", 22, true);
            var minimize = new CheckBox { Text = "Minimize Hub after starting Genesis", Checked = settings.MinimizeOnPlay, AutoSize = true, Enabled = settingsReadable, Margin = new Padding(0, 0, 0, 24) };
            minimize.CheckedChanged += (_, _) => Persist(settings with { MinimizeOnPlay = minimize.Checked }); content.Controls.Add(minimize);
            Copy("Settings are stored in your Windows local application data. Game files and Genesis saves remain untouched. No account or network connection is required.");
            Action("Recheck installation", (_, _) => { RefreshInstallation(); Render(); });
            if (settings.GenesisFolder is not null) Action("Remove from library", (_, _) => { if (Persist(settings with { GenesisFolder = null })) { installation = null; problem = ""; Render(); } }).Enabled = settingsReadable;
            Copy("Removing a library entry never deletes a game, AI model or save. Hub updates are not implemented.");
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
            installation = found; problem = ""; Render();
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Could not connect Genesis", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
    void Play(object? sender, EventArgs e)
    {
        try
        {
            installation = Genesis.Detect(settings.GenesisFolder!);
            using var process = Process.Start(Genesis.LaunchInfo(installation)) ?? throw new IOException("Windows did not create a game process.");
            if (settings.MinimizeOnPlay) WindowState = FormWindowState.Minimized;
        }
        catch (Exception ex) { problem = "Could not start Genesis: " + ex.Message; Render(); MessageBox.Show(this, problem, "Launch error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
}
