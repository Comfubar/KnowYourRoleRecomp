using System.Diagnostics;
using KnowYourRole.Launcher.Controllers;

namespace KnowYourRole.Launcher;

//the launcher: PLAY, the controllers (one card per player), display settings and the game files. Everything is saved
//to the files the game reads (user\settings.json, user\interface.ini, user\gamecontrollerdb.user.txt).
public sealed class MainForm : Form
{
    private readonly GameSettings _settings;
    private PadService? _pads;
    private readonly PlayerCard[] _cards = new PlayerCard[4];
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 50 };
    private readonly Label _padsInfo = new() { AutoSize = true, MaximumSize = new Size(820, 0) };
    private List<PadService.Pad> _list = [];

    //playNow: straight into the game once the window is up (the first run, right after the build)
    public MainForm(bool playNow = false)
    {
        _settings = GameSettings.Load();
        Text = "Know Your Role Recomp";
        Icon = LauncherIcon.Load();
        ClientSize = new Size(900, 640);
        MinimumSize = new Size(760, 560);
        StartPosition = FormStartPosition.CenterScreen;

        var play = new Button
        {
            Text = "PLAY", Font = new Font(Font.FontFamily, 18, FontStyle.Bold), Size = new Size(220, 64),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        play.Click += (_, _) => Play();
        var title = new Label
        {
            Text = $"Know Your Role Recomp {Program.Version}", Font = new Font(Font.FontFamily, 14, FontStyle.Bold),
            AutoSize = true, Location = new Point(16, 16)
        };
        var disc = new Label
        {
            Text = $"Disc: {Path.GetFileName(_settings.CdPath)}", AutoSize = true, Location = new Point(18, 50),
            ForeColor = SystemColors.GrayText
        };
        var header = new Panel { Dock = DockStyle.Top, Height = 90 };
        header.Controls.AddRange([title, disc, play]);
        header.Resize += (_, _) => play.Location = new Point(header.Width - play.Width - 16, 12);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(ControllersPage());
        tabs.TabPages.Add(DisplayPage());
        tabs.TabPages.Add(FilesPage());
        Controls.Add(tabs);
        Controls.Add(header);

        _timer.Tick += (_, _) => Tick();
        Shown += (_, _) =>
        {
            StartPads();
            _timer.Start();
            if (playNow) BeginInvoke(Play);
        };
        FormClosed += (_, _) =>
        {
            _timer.Stop();
            _pads?.Dispose();
        };
    }

    private void StartPads()
    {
        try
        {
            _pads = new PadService();
            _padsInfo.Text = $"SDL {_pads.Version}, {_pads.MappingsLoaded} controller mappings. Xbox and XInput pads, " +
                             "DualShock 4, DualSense (no DualSenseX or DS4Windows needed), Switch Pro and Joy-Con, " +
                             "8BitDo and most USB pads. Bluetooth: pair the pad in Windows Settings > Bluetooth first.";
        }
        catch (Exception e)
        {
            _padsInfo.Text = $"Controllers are not available: {e.Message}. The keyboard still works.";
        }
    }

    private TabPage ControllersPage()
    {
        var page = new TabPage("Controllers") { AutoScroll = true, Padding = new Padding(8) };
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        flow.Controls.Add(_padsInfo);
        var cards = new TableLayoutPanel { AutoSize = true, ColumnCount = 2 };
        for (var p = 0; p < 4; p++)
        {
            _cards[p] = new PlayerCard(p, this);
            cards.Controls.Add(_cards[p].Box, p % 2, p / 2);
        }

        flow.Controls.Add(cards);

        var options = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, MaximumSize = new Size(840, 0) };
        var dz = new TrackBar { Minimum = 0, Maximum = 60, TickFrequency = 10, Width = 160, Value = (int)Math.Round(_settings.StickDeadzone * 100) };
        var dzLabel = new Label { AutoSize = true, Margin = new Padding(3, 12, 3, 3), Text = $"Stick deadzone {dz.Value}%" };
        dz.ValueChanged += (_, _) =>
        {
            dzLabel.Text = $"Stick deadzone {dz.Value}%";
            _settings.StickDeadzone = dz.Value / 100f;
            _settings.Save();
        };
        var tap = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
        tap.Items.AddRange(["Multitap: automatic (3 or 4 players)", "Multitap: always on", "Multitap: off (2 players)"]);
        tap.SelectedIndex = Math.Clamp(_settings.Multitap, 0, 2);
        tap.SelectedIndexChanged += (_, _) =>
        {
            _settings.Multitap = tap.SelectedIndex;
            _settings.Save();
        };
        var vib = new CheckBox { Text = "Vibration", AutoSize = true, Checked = _settings.Vibration, Margin = new Padding(12, 10, 3, 3) };
        vib.CheckedChanged += (_, _) =>
        {
            _settings.Vibration = vib.Checked;
            _settings.Save();
        };
        var btRumble = new CheckBox
        {
            Text = "Vibration over Bluetooth for PlayStation pads", AutoSize = true, Checked = _settings.PlayStationBluetoothRumble,
            Margin = new Padding(12, 10, 3, 3)
        };
        new ToolTip().SetToolTip(btRumble, "A DualShock 4 or DualSense connected by Bluetooth only rumbles in an extended mode. Once on, " +
                                           "the pad stays in it until it is switched off, and some other programs then read it wrongly.");
        btRumble.CheckedChanged += (_, _) =>
        {
            _settings.PlayStationBluetoothRumble = btRumble.Checked;
            _settings.Save();
        };
        options.Controls.AddRange([dzLabel, dz, tap, vib, btRumble]);
        flow.Controls.Add(options);
        flow.Controls.Add(new Label
        {
            AutoSize = true, MaximumSize = new Size(840, 0), ForeColor = SystemColors.GrayText,
            Text = "Buttons go by position: the bottom face button is Cross on every pad (A on Xbox, B on Nintendo). " +
                   "The game uses the d-pad; the left stick works as a d-pad. Keyboard for player 1: arrows, Z Cross, X Circle, " +
                   "A Square, S Triangle, Q/W L1/R1, E/R L2/R2, Enter Start, right Shift Select. The mouse works in these " +
                   "menus and the in-game settings (F1), not in the game."
        });
        page.Controls.Add(flow);
        return page;
    }

    private TabPage DisplayPage()
    {
        var page = new TabPage("Display") { Padding = new Padding(12) };
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown };
        var full = new CheckBox { Text = "Fullscreen (F11 switches while playing)", AutoSize = true, Checked = InterfaceIni.Get("Fullscreen", false) };
        full.CheckedChanged += (_, _) => InterfaceIni.Set("Fullscreen", full.Checked);
        var vsync = new CheckBox { Text = "VSync (smooth, no tearing)", AutoSize = true, Checked = InterfaceIni.Get("VSync", true) };
        vsync.CheckedChanged += (_, _) => InterfaceIni.Set("VSync", vsync.Checked);
        flow.Controls.AddRange([full, vsync]);
        page.Controls.Add(flow);
        return page;
    }

    private TabPage FilesPage()
    {
        var page = new TabPage("Game files") { Padding = new Padding(12) };
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        Button B(string text, Action act)
        {
            var b = new Button { Text = text, AutoSize = true, MinimumSize = new Size(260, 30) };
            b.Click += (_, _) => act();
            return b;
        }

        var skip = new CheckBox { Text = "Start the game right away next time (skip this window; KnowYourRole.exe --launcher opens it)", AutoSize = true, Checked = LauncherConfig.Load().SkipLauncher };
        skip.CheckedChanged += (_, _) =>
        {
            var c = LauncherConfig.Load();
            c.SkipLauncher = skip.Checked;
            c.Save();
        };
        flow.Controls.AddRange([
            B("Change disc / rebuild the game", () =>
            {
                Hide();
                new SetupForm(InstallState.NotBuilt).ShowDialog();
                Close();
            }),
            B("Open the saves folder", () => Process.Start(new ProcessStartInfo(Paths.User) { UseShellExecute = true })),
            B("Collect diagnostics (for a bug report)", () =>
            {
                var zip = DiagnosticsCollector.Collect();
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{zip}\""));
            }),
            B("Create a desktop shortcut", () =>
            {
                var lnk = Shortcut.CreateOnDesktop();
                MessageBox.Show(this, $"Shortcut created: {lnk}\nIt starts the game directly.", Text);
            }),
            skip
        ]);
        page.Controls.Add(flow);
        return page;
    }

    private void Play()
    {
        _settings.Save();
        _timer.Stop();
        _pads?.Dispose();
        _pads = null;
        Hide();
        int code;
        try
        {
            code = GameRunner.RunAndWait();
        }
        catch (InvalidOperationException e)
        {
            VcRuntimeDialog.ShowProblem(this, e);
            code = 0;
        }
        finally
        {
            Show();
            StartPads();
            _timer.Start();
        }

        if (code != 0)
            MessageBox.Show(this, $"The game stopped with an error (exit code {code}). A report was saved in\n{Paths.Logs}\n\n" +
                                  "Use Game files > Collect diagnostics and attach the zip to a bug report.", Text,
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private int _ticks;

    private void Tick()
    {
        if (_pads == null) return;
        _pads.Update();
        if (_ticks++ % 20 == 0) _list = _pads.List();
        _pads.CheckMirrors(_list);
        var ignored = _settings.IgnoredPads;
        var usable = _list.Where(p => !ignored.Any(i => i.Equals(p.Guid, StringComparison.OrdinalIgnoreCase) ||
                                                       p.Name.Contains(i, StringComparison.OrdinalIgnoreCase))).ToList();
        foreach (var c in _cards) c.Refresh(_pads, _list, usable);
    }

    internal GameSettings Settings => _settings;
    internal PlayerCard[] Cards => _cards;
    internal string PadsInfo => _padsInfo.Text;
    internal void TickNow() => Tick();

    //the card's device list (tests)
    internal void ChoosePad(int player, string guid) => _cards[player].Choose(guid);
    internal PadService? Pads => _pads;

    //which pad a player gets, the way the game assigns them: chosen ones first, then the rest in order
    internal PadService.Pad? Assigned(int player, List<PadService.Pad> usable)
    {
        var chosen = Enumerable.Range(0, 4).Select(_settings.PadDevice).ToArray();
        var taken = new HashSet<string>();
        var result = new PadService.Pad?[4];
        for (var p = 0; p < 4; p++)
            if (chosen[p].Length > 0)
            {
                result[p] = usable.FirstOrDefault(d => d.Guid == chosen[p] && taken.Add(d.Guid + d.Index));
            }

        for (var p = 0; p < 4; p++)
            if (chosen[p].Length == 0)
                result[p] = usable.Where(d => d.Mapped).FirstOrDefault(d => taken.Add(d.Guid + d.Index));
        return result[player];
    }

    internal void MapController(PadService.Pad pad)
    {
        if (_pads == null) return;
        using var form = new MapWizardForm(_pads, pad);
        form.ShowDialog(this);
        _list = _pads.List();
    }

    internal void IgnorePad(PadService.Pad pad)
    {
        var list = _settings.IgnoredPads;
        if (!list.Contains(pad.Guid)) list.Add(pad.Guid);
        _settings.IgnoredPads = list;
        _settings.Save();
    }
}

//one player's controller: which pad (automatic or a chosen one), what it is, live input, and the fixes a pad may need
internal sealed class PlayerCard
{
    public readonly GroupBox Box;
    private readonly int _player;
    private readonly MainForm _form;
    private readonly ComboBox _device = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 380 };
    private readonly Label _what = new() { AutoSize = true, MaximumSize = new Size(390, 0) };
    private readonly Label _live = new() { AutoSize = true, MaximumSize = new Size(390, 0), Font = new Font(FontFamily.GenericMonospace, 9) };
    private readonly Label _warn = new() { AutoSize = true, MaximumSize = new Size(390, 0), ForeColor = Color.DarkRed };
    private readonly Button _map = new() { Text = "Map this controller", AutoSize = true, Visible = false };
    private readonly Button _ignore = new() { Text = "Ignore this controller", AutoSize = true, Visible = false };
    private readonly CheckBox _kb2 = new() { Text = "Keyboard as player 2 (arrows on the number pad, K Cross, L Circle, J Square, I Triangle, Backspace Start)", AutoSize = true, MaximumSize = new Size(390, 0) };
    private string _itemsKey = "";
    private PadService.Pad? _current;
    private bool _filling;

    public PlayerCard(int player, MainForm form)
    {
        _player = player;
        _form = form;
        Box = new GroupBox { Text = $"Player {player + 1}", Size = new Size(410, 210), Margin = new Padding(6) };
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        flow.Controls.AddRange([_device, _what, _live, _warn]);
        var row = new FlowLayoutPanel { AutoSize = true };
        row.Controls.AddRange([_map, _ignore]);
        flow.Controls.Add(row);
        if (player == 0)
            flow.Controls.Add(new Label { AutoSize = true, ForeColor = SystemColors.GrayText, Text = "The keyboard always works for player 1." });
        if (player == 1)
        {
            _kb2.Checked = form.Settings.Keyboard2;
            _kb2.CheckedChanged += (_, _) =>
            {
                form.Settings.Keyboard2 = _kb2.Checked;
                form.Settings.Save();
            };
            flow.Controls.Add(_kb2);
        }

        Box.Controls.Add(flow);
        _device.SelectedIndexChanged += (_, _) =>
        {
            if (_filling || _device.SelectedItem is not Choice c) return;
            form.Settings.SetPadDevice(player, c.Guid);
            form.Settings.Save();
        };
        _map.Click += (_, _) =>
        {
            if (_current != null) form.MapController(_current);
        };
        _ignore.Click += (_, _) =>
        {
            if (_current != null) form.IgnorePad(_current);
        };
    }

    //what the card shows (tests)
    public string Summary => $"{_what.Text.Replace('\n', ' ')} | {_live.Text.Replace('\n', ' ')} | {_warn.Text}";
    public bool IgnoreOffered => _ignore.Visible;
    public PadService.Pad? Current => _current;

    public void Choose(string guid)
    {
        var i = _device.Items.Cast<Choice>().ToList().FindIndex(c => c.Guid == guid);
        if (i < 0) throw new InvalidOperationException($"player {_player + 1}: no controller {guid} in the list");
        _device.SelectedIndex = i;
    }

    private sealed record Choice(string Guid, string Text)
    {
        public override string ToString() => Text;
    }

    public void Refresh(PadService pads, List<PadService.Pad> all, List<PadService.Pad> usable)
    {
        var key = string.Join("|", usable.Select(p => p.Guid + p.Index));
        if (key != _itemsKey)
        {
            _itemsKey = key;
            _filling = true;
            _device.Items.Clear();
            _device.Items.Add(new Choice("", "Automatic (the next free controller)"));
            foreach (var p in usable.DistinctBy(p => p.Guid))
                _device.Items.Add(new Choice(p.Guid, $"{p.Name} ({p.Family}{(p.Connection.Length > 0 ? ", " + p.Connection : "")})"));
            var want = _form.Settings.PadDevice(_player);
            var sel = _device.Items.Cast<Choice>().ToList().FindIndex(c => c.Guid == want);
            if (sel < 0 && want.Length > 0)
            {
                _device.Items.Add(new Choice(want, "the chosen controller (not connected)"));
                sel = _device.Items.Count - 1;
            }

            _device.SelectedIndex = Math.Max(0, sel);
            _filling = false;
        }

        _current = _form.Assigned(_player, usable);
        _map.Visible = _current != null || usable.Any(p => !p.Mapped);
        if (_current == null)
        {
            var unmapped = usable.FirstOrDefault(p => !p.Mapped);
            _what.Text = unmapped != null ? $"'{unmapped.Name}' has no button layout yet." : "No controller.";
            _current = unmapped;
            _live.Text = "";
            _warn.Text = unmapped != null ? "Map it once: press each button when asked." : "";
            _ignore.Visible = false;
            return;
        }

        var labels = RecompOne.Runtime.Input.PadInfo.FaceLabels(_current.Family);
        _what.Text = $"{_current.Name}\n{_current.Family}{(_current.Connection.Length > 0 ? ", " + _current.Connection : "")}. " +
                     $"Cross = {labels[0]}, Circle = {labels[1]}, Square = {labels[2]}, Triangle = {labels[3]}";
        var (held, lx, ly) = pads.State(_current);
        _live.Text = $"Pressed: {(held.Count > 0 ? string.Join(" ", held) : "-")}\nLeft stick {lx:+0.00;-0.00} {ly:+0.00;-0.00}";
        var mirror = pads.MirrorOf(_current);
        if (mirror != null)
        {
            var twin = all.FirstOrDefault(p => p.Instance == mirror);
            _warn.Text = $"This is the same pad as '{twin?.Name}' seen twice (a remapper such as DualSenseX, DS4Windows " +
                         "or Steam Input). Ignore one of them, or close the remapper.";
            _ignore.Visible = true;
        }
        else
        {
            _warn.Text = "";
            _ignore.Visible = false;
        }
    }
}
