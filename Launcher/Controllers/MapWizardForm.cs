namespace KnowYourRole.Launcher.Controllers;

//"map this controller": asks for one control after another and saves the result for this pad model
public sealed class MapWizardForm : Form
{
    private readonly PadService _pads;
    private readonly PadService.Pad _pad;
    private readonly PadService.RawCapture _capture;
    private readonly MappingWizard _wizard;
    private readonly Label _prompt = new() { AutoSize = true, Font = new Font(SystemFonts.DefaultFont.FontFamily, 13, FontStyle.Bold), MaximumSize = new Size(520, 0) };
    private readonly Label _done = new() { AutoSize = true, MaximumSize = new Size(520, 0), ForeColor = SystemColors.GrayText };
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 20 };

    public MapWizardForm(PadService pads, PadService.Pad pad)
    {
        _pads = pads;
        _pad = pad;
        _capture = pads.Capture(pad);
        _wizard = new MappingWizard(_capture);
        Text = $"Map this controller: {pad.Name}";
        ClientSize = new Size(560, 260);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = MinimizeBox = false;

        var skip = new Button { Text = "Skip (this pad has no such control)", AutoSize = true };
        var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        skip.Click += (_, _) =>
        {
            _wizard.Skip();
            Show(false);
        };
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(16) };
        var buttons = new FlowLayoutPanel { AutoSize = true };
        buttons.Controls.AddRange([skip, cancel]);
        flow.Controls.AddRange([
            new Label { AutoSize = true, MaximumSize = new Size(520, 0), Text = "Press the control on your pad that should be:" },
            _prompt, _done, buttons
        ]);
        Controls.Add(flow);
        CancelButton = cancel;
        _timer.Tick += (_, _) =>
        {
            if (_wizard.Poll()) Show(true);
        };
        Shown += (_, _) =>
        {
            Show(false);
            _timer.Start();
        };
        FormClosed += (_, _) =>
        {
            _timer.Stop();
            _capture.Dispose();
        };
    }

    private void Show(bool took)
    {
        if (took && _wizard.Binds.Count > 0)
        {
            var (t, s) = _wizard.Binds[^1];
            _done.Text = $"{t} = {s}";
        }

        if (!_wizard.Done)
        {
            _prompt.Text = $"{_wizard.Index + 1}/{MappingWizard.Targets.Length}: {_wizard.Current.Prompt}";
            return;
        }

        _timer.Stop();
        _pads.SaveMapping(_pad, _wizard.Binds);
        MessageBox.Show(this, $"Saved. The game uses this layout for every '{_pad.Name}' from now on.\n({Paths.UserMappings})", Text);
        DialogResult = DialogResult.OK;
        Close();
    }
}
