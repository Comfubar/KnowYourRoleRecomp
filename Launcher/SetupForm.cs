using System.Diagnostics;

namespace KnowYourRole.Launcher;

//first run (or after an update / a moved disc): pick the disc, check it and this PC, build the game, then the launcher
public sealed class SetupForm : Form
{
    private readonly InstallState _state;
    private readonly TextBox _cue = new() { Dock = DockStyle.Fill };
    private readonly Label _discStatus = new() { AutoSize = true, MaximumSize = new Size(640, 0) };
    private readonly Label _pcStatus = new() { AutoSize = true, MaximumSize = new Size(640, 0) };
    private readonly Button _vcInstall = new() { Text = "Install it", AutoSize = true, Visible = false, Anchor = AnchorStyles.Top };
    private readonly Label _oneDrive = new() { AutoSize = true, MaximumSize = new Size(640, 0), ForeColor = Color.DarkOrange, Visible = false };
    private readonly Button _build = new() { Text = "Build the game", AutoSize = true, Enabled = false };
    private readonly Button _collect = new() { Text = "Collect diagnostics", AutoSize = true, Visible = false };
    private readonly ProgressBar _bar = new() { Dock = DockStyle.Fill, Height = 22 };
    private readonly Label _status = new() { AutoSize = true, MaximumSize = new Size(640, 0) };
    private readonly TextBox _log = new()
    {
        Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false,
        Font = new Font(FontFamily.GenericMonospace, 8.5f), Visible = false
    };
    private bool _discOk;

    internal Button VcInstallButton => _vcInstall;
    internal Label OneDriveNote => _oneDrive;

    public SetupForm(InstallState state, string? carried = null)
    {
        _state = state;
        Text = $"Know Your Role Recomp {Program.Version} - setup";
        Icon = LauncherIcon.Load();
        ClientSize = new Size(720, 520);
        MinimumSize = new Size(600, 420);
        StartPosition = FormStartPosition.CenterScreen;

        var intro = new Label
        {
            AutoSize = true, MaximumSize = new Size(680, 0), Margin = new Padding(3, 3, 3, 12),
            Text = state switch
            {
                InstallState.OtherVersion =>
                    "This version builds the game again from your disc (about three minutes). Your saves and settings stay.",
                InstallState.DiscMissing =>
                    "The disc image the game was built from is not where it was. Pick it again (the same disc).",
                _ => "Welcome. Know Your Role Recomp builds the game on this PC from your own copy of the NTSC-U disc " +
                     "(SLUS-01234). Pick the disc image (.cue file); nothing from the disc leaves this PC."
            } + (carried != null ? $"\n\nYour saves and settings were {carried}." : "")
        };

        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Padding = new Padding(12) };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.Controls.Add(intro, 0, 0);
        grid.SetColumnSpan(intro, 3);
        grid.Controls.Add(new Label { Text = "Disc (.cue)", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        grid.Controls.Add(_cue, 1, 1);
        var browse = new Button { Text = "Browse...", AutoSize = true };
        grid.Controls.Add(browse, 2, 1);
        grid.Controls.Add(_discStatus, 1, 2);
        var pc = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        pc.Controls.Add(_pcStatus);
        pc.Controls.Add(_oneDrive);
        grid.Controls.Add(pc, 1, 3);
        grid.Controls.Add(_vcInstall, 2, 3);
        _vcInstall.Click += (_, _) => SystemCheck.OpenUrl(SystemCheck.VcRuntimeUrl);
        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        buttons.Controls.AddRange([_build, _collect]);
        grid.Controls.Add(buttons, 1, 4);
        grid.Controls.Add(_bar, 1, 5);
        grid.Controls.Add(_status, 1, 6);
        grid.Controls.Add(_log, 0, 7);
        grid.SetColumnSpan(_log, 3);
        for (var i = 0; i < 7; i++) grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(grid);

        var known = GameSettings.Load().CdPath;
        if (known.Length > 0 && File.Exists(known)) _cue.Text = known;
        browse.Click += (_, _) =>
        {
            using var d = new OpenFileDialog { Filter = "Disc image (*.cue, *.bin)|*.cue;*.bin|All files (*.*)|*.*", Title = "Your disc image" };
            if (d.ShowDialog(this) == DialogResult.OK) _cue.Text = d.FileName;
        };
        _cue.TextChanged += async (_, _) => await CheckDiscAsync();
        _build.Click += async (_, _) => await BuildAsync();
        _collect.Click += (_, _) =>
        {
            var zip = DiagnosticsCollector.Collect();
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{zip}\""));
        };
        Shown += async (_, _) =>
        {
            CheckPc();
            if (_cue.Text.Length > 0) await CheckDiscAsync();
        };
    }

    private void CheckPc()
    {
        var (total, free) = SystemCheck.Ram();
        var disk = SystemCheck.FreeDiskMB(Paths.Root);
        var ok = total == 0 || total >= SystemCheck.MinTotalRamMB;
        _pcStatus.Text = $"{(ok && disk >= SystemCheck.MinFreeDiskMB ? "OK" : "PROBLEM")}: memory {total / 1024.0:0.#} GB " +
                         $"({free / 1024.0:0.#} GB free), {disk / 1024.0:0.#} GB free disk space " +
                         $"(the build needs {SystemCheck.MinTotalRamMB / 1024} GB memory and {SystemCheck.MinFreeDiskMB / 1024.0:0.#} GB disk)";
        _pcStatus.ForeColor = ok ? SystemColors.ControlText : Color.DarkRed;
        if (SystemCheck.MissingVcRuntime() is { } missing)
        {
            //the build works without it, the game does not: say so now
            _pcStatus.Text += "\n\nPROBLEM: " + SystemCheck.VcRuntimeMessage(missing);
            _pcStatus.ForeColor = Color.DarkRed;
            _vcInstall.Visible = true;
        }

        //a warning only: the game works there, OneDrive just uploads it
        if (SystemCheck.OneDriveFolderOf(Paths.Runtime) is { } oneDrive)
        {
            _oneDrive.Text = SystemCheck.OneDriveWarning(oneDrive);
            _oneDrive.Visible = true;
        }
    }

    private async Task CheckDiscAsync()
    {
        _discOk = false;
        _build.Enabled = false;
        var cue = _cue.Text.Trim('"');
        //the .cue names the .bin; a player who picks the .bin gets the .cue beside it
        if (cue.EndsWith(".bin", StringComparison.OrdinalIgnoreCase) && File.Exists(Path.ChangeExtension(cue, ".cue")))
        {
            _cue.Text = Path.ChangeExtension(cue, ".cue");
            return;
        }

        if (!File.Exists(cue))
        {
            _discStatus.Text = cue.Length == 0 ? "" : "File not found.";
            return;
        }

        _discStatus.Text = "Checking the disc...";
        _discStatus.ForeColor = SystemColors.ControlText;
        var result = await Task.Run(() => DiscVerify.Check(cue));
        _discOk = result.Ok;
        _discStatus.Text = result.Message;
        _discStatus.ForeColor = result.Ok ? Color.DarkGreen : Color.DarkRed;
        if (!_discOk) return;
        if (_state == InstallState.DiscMissing)
        {
            //the game is built already: only the disc location changes
            var s = GameSettings.Load();
            s.CdPath = Path.GetFullPath(cue);
            s.Save();
            OpenLauncher();
            return;
        }

        _build.Enabled = true;
    }

    private async Task BuildAsync()
    {
        if (!_discOk) return;
        _build.Enabled = false;
        _cue.Enabled = false;
        _log.Visible = true;
        UseWaitCursor = true;
        var estimate = TimeSpan.FromSeconds(LauncherConfig.Load().LastBuildSeconds is > 0 and var s ? s : 160);
        var progress = new Progress<BuildProgress>(p =>
        {
            _bar.Maximum = p.Steps;
            _bar.Value = Math.Min(p.Step, p.Steps);
            var left = p.Estimate - p.Elapsed;
            _status.Text = $"{p.Text}  ({(left.TotalSeconds > 20 ? $"about {Math.Ceiling(left.TotalMinutes)} min left" : "almost done")})";
        });

        bool ok;
        string logPath;
        using (var log = new BuildLog())
        {
            logPath = log.Path;
            log.Line += line => BeginInvoke(() => _log.AppendText(line + Environment.NewLine));
            var job = new BuildJob(new BuildOptions(_cue.Text.Trim('"'), Paths.Runtime, false), log, progress) { Estimate = estimate };
            ok = await Task.Run(job.Run);
        }

        UseWaitCursor = false;
        if (ok)
        {
            Program.SaveBuildTime();
            //first run: the game starts as soon as it is built; closing it shows the launcher
            OpenLauncher(playNow: true);
            return;
        }

        _status.Text = "The build did not finish.";
        _collect.Visible = true;
        _build.Enabled = true;
        _cue.Enabled = true;
        MessageBox.Show(this,
            $"The game could not be built.\n\n{BuildLog.LastError ?? "See the log for the reason."}\n\nThe full log is " +
            $"{logPath}. Use \"Collect diagnostics\" and attach the zip to a bug report.",
            Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private void OpenLauncher(bool playNow = false)
    {
        Hide();
        new MainForm(playNow).ShowDialog();
        Close();
    }
}
