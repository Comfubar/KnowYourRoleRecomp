using KnowYourRole.Launcher.Controllers;

namespace KnowYourRole.Launcher;

//KnowYourRole.exe --selftest <window|controllers|paths|system>: drives the launcher without a person.
//Prints PASS/FAIL lines, exit code 0 when everything passed. Controllers are SDL virtual pads, no hardware needed.
public static class SelfTest
{
    private static int _failures;

    private static void Check(bool ok, string what)
    {
        Console.WriteLine($"[SelfTest] {(ok ? "PASS" : "FAIL")} {what}");
        if (!ok) _failures++;
    }

    public static int Run(string name)
    {
        switch (name)
        {
            case "window":
                Window();
                break;
            case "controllers":
                Controllers();
                break;
            case "paths":
                PathChecks();
                break;
            case "system":
                SystemChecks();
                break;
            default:
                Console.Error.WriteLine($"unknown self test '{name}' (window, controllers, paths, system)");
                return 2;
        }

        Console.WriteLine($"[SelfTest] {name}: {(_failures == 0 ? "all passed" : $"{_failures} failed")}");
        return _failures == 0 ? 0 : 1;
    }

    private static void Pump(MainForm form, int ms)
    {
        var until = Environment.TickCount64 + ms;
        while (Environment.TickCount64 < until)
        {
            Application.DoEvents();
            form.TickNow();
            Thread.Sleep(20);
        }
    }

    //where the game may live: Program Files and too deep paths are refused with a reason, a normal folder is fine
    private static void PathChecks()
    {
        var pf = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "KnowYourRole");
        var r1 = Paths.NotWritableReason(pf);
        Check(r1 != null && r1.Contains("Windows does not let programs write"), $"Program Files refused: {r1}");
        var deep = Path.Combine(Path.GetTempPath(), new string('d', 120), new string('e', 80));
        var r2 = Paths.NotWritableReason(deep);
        Check(r2 != null && r2.Contains("characters long"), $"a {deep.Length} character path refused: {r2}");
        var ok = Path.Combine(Path.GetTempPath(), $"kyr-selftest-{Environment.ProcessId}");
        var r3 = Paths.NotWritableReason(ok);
        Check(r3 == null, $"a normal folder accepted ({ok}): {r3 ?? "ok"}");
        Directory.Delete(ok, true);
    }

    //the Visual C++ runtime check (setup and PLAY) with its "Install it" button, and the OneDrive warning in the setup
    private static void SystemChecks()
    {
        var real = SystemCheck.MissingVcRuntime();
        Console.WriteLine($"[SelfTest] Visual C++ runtime on this PC: {real ?? "all present"}");
        const string absent = "kyr-selftest-absent140.dll";
        var saved = SystemCheck.VcRuntimeFiles;
        var opened = new List<string>();
        SystemCheck.OpenUrl = opened.Add;
        SystemCheck.VcRuntimeFiles = [.. saved, absent];
        try
        {
            Check(SystemCheck.MissingVcRuntime() == absent, $"a missing runtime file is reported: {SystemCheck.MissingVcRuntime()}");
            try
            {
                using var p = GameRunner.Start();
                Check(false, "PLAY started the game although a runtime file is missing");
                p.Kill();
            }
            catch (VcRuntimeMissingException e)
            {
                Check(e.Message.Contains(absent) && e.Message.Contains(SystemCheck.VcRuntimeUrl), $"PLAY stops with: {e.Message}");
            }

            using (var d = new VcRuntimeDialog(SystemCheck.VcRuntimeMessage(absent)) { ShowInTaskbar = false })
            {
                d.Show();
                Application.DoEvents();
                Check(d.InstallButton.Visible && d.InstallButton.Text == "Install it", $"PLAY dialog button: '{d.InstallButton.Text}'");
                d.InstallButton.PerformClick();
                Check(opened.LastOrDefault() == SystemCheck.VcRuntimeUrl, $"PLAY dialog 'Install it' opens {opened.LastOrDefault()}");
            }

            using (var setup = new SetupForm(InstallState.NotBuilt) { WindowState = FormWindowState.Minimized, ShowInTaskbar = false })
            {
                setup.Show();
                PumpForm(setup, 800);
                Check(setup.VcInstallButton.Visible, $"setup shows '{setup.VcInstallButton.Text}'");
                opened.Clear();
                setup.VcInstallButton.PerformClick();
                Check(opened.SingleOrDefault() == SystemCheck.VcRuntimeUrl, $"setup 'Install it' opens {opened.SingleOrDefault()}");
                setup.Close();
            }
        }
        finally
        {
            SystemCheck.VcRuntimeFiles = saved;
        }

        //OneDrive: this PC's folders, then one named the way OneDrive names it in the environment
        var roots = SystemCheck.OneDriveRoots().ToList();
        Console.WriteLine($"[SelfTest] OneDrive folders on this PC: {(roots.Count == 0 ? "none" : string.Join("; ", roots))}");
        var fake = Path.Combine(Path.GetTempPath(), $"kyr-selftest-onedrive-{Environment.ProcessId}");
        var outside = Path.Combine(Path.GetTempPath(), $"kyr-selftest-local-{Environment.ProcessId}");
        var savedEnv = Environment.GetEnvironmentVariable("OneDriveConsumer");
        var savedRuntime = Paths.Runtime;
        Environment.SetEnvironmentVariable("OneDriveConsumer", fake);
        try
        {
            var inside = Path.Combine(fake, "Games", "KnowYourRole", "runtime");
            Check(SystemCheck.OneDriveFolderOf(inside) == fake, $"a folder inside OneDrive is recognised: {SystemCheck.OneDriveFolderOf(inside)}");
            Check(SystemCheck.OneDriveFolderOf(fake + "-other") == null, "a folder that only starts with the same name is not");
            Check(SystemCheck.OneDriveFolderOf(outside) == null, $"a folder outside OneDrive is not: {outside}");
            using (var log = new BuildLog())
            {
                var lines = new List<string>();
                log.Line += lines.Add;
                var blocked = SystemCheck.Check(inside, log);
                var note = lines.FirstOrDefault(l => l.Contains("[System] Note: this folder is inside OneDrive"));
                Check(note != null && blocked == null, $"the build (window or --cue) logs the note and goes on: {note}");
            }

            Paths.Runtime = inside;
            using (var setup = new SetupForm(InstallState.NotBuilt) { WindowState = FormWindowState.Minimized, ShowInTaskbar = false })
            {
                setup.Show();
                PumpForm(setup, 800);
                Check(setup.OneDriveNote.Visible && setup.OneDriveNote.Text.StartsWith("Note: this folder is inside OneDrive"),
                    $"setup warns (one line, build not blocked by it): '{setup.OneDriveNote.Text}'");
                setup.Close();
            }

            Paths.Runtime = outside;
            using (var setup = new SetupForm(InstallState.NotBuilt) { WindowState = FormWindowState.Minimized, ShowInTaskbar = false })
            {
                setup.Show();
                PumpForm(setup, 800);
                Check(!setup.OneDriveNote.Visible, "no warning outside OneDrive");
                setup.Close();
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("OneDriveConsumer", savedEnv);
            Paths.Runtime = savedRuntime;
        }
    }

    private static void PumpForm(Form form, int ms)
    {
        var until = Environment.TickCount64 + ms;
        while (Environment.TickCount64 < until)
        {
            Application.DoEvents();
            Thread.Sleep(20);
        }
    }

    //a second start opens the launcher (not the setup): the game is built and the disc is where it was
    private static void Window()
    {
        var state = Install.Check();
        Check(state == InstallState.Ready, $"install state: {Install.Describe(state)}");
        using var form = new MainForm { WindowState = FormWindowState.Minimized, ShowInTaskbar = false };
        form.Show();
        Pump(form, 1500);
        Check(form.Visible && form.Text == "Know Your Role Recomp", $"launcher window open: '{form.Text}'");
        form.Close();
    }

    private static void Controllers()
    {
        //a mapping an earlier self test saved for the test pad would make it start mapped
        if (File.Exists(Paths.UserMappings))
            File.WriteAllLines(Paths.UserMappings, File.ReadAllLines(Paths.UserMappings).Where(l => !l.Contains(",Unknown Test Pad,")));
        using var form = new MainForm { WindowState = FormWindowState.Minimized, ShowInTaskbar = false };
        form.Show();
        Pump(form, 500);
        Console.WriteLine($"[SelfTest] {form.PadsInfo}");
        var pads = form.Pads ?? throw new InvalidOperationException($"SDL did not start in the launcher: {form.PadsInfo}");

        //cards list the pads with their family
        pads.AttachVirtual("ds", "dualsense");
        pads.AttachVirtual("xb", "xbox360");
        Pump(form, 1500);
        var c1 = form.Cards[0].Summary;
        var c2 = form.Cards[1].Summary;
        Console.WriteLine($"[SelfTest] P1 card: {c1}");
        Console.WriteLine($"[SelfTest] P2 card: {c2}");
        Check(c1.Contains("DualSense") && c1.Contains("PlayStation 5") && c1.Contains("Cross = Cross"), "P1 card shows the DualSense (PlayStation 5, PlayStation labels)");
        Check(c2.Contains("Xbox 360") && c2.Contains("Cross = A"), "P2 card shows the Xbox 360 pad (Xbox labels)");

        //live input
        pads.PressVirtual("ds", 0, true);
        Pump(form, 300);
        Check(form.Cards[0].Summary.Contains("Pressed: Cross"), $"live input on the P1 card: {form.Cards[0].Summary}");
        pads.PressVirtual("ds", 0, false);
        Pump(form, 200);

        //a duplicate (the same presses on both, like a remapper) is flagged and can be ignored
        //(more than the detector's history, so the single-pad presses above have left it)
        foreach (var b in new[] { 0, 1, 11, 2, 12, 3, 0, 1, 13, 14, 2, 3, 11, 0 })
        {
            pads.PressVirtual("ds", b, true);
            pads.PressVirtual("xb", b, true);
            Pump(form, 120);
            pads.PressVirtual("ds", b, false);
            pads.PressVirtual("xb", b, false);
            Pump(form, 120);
        }

        var flagged = form.Cards[1].IgnoreOffered;
        Check(flagged, $"the mirrored pad is flagged on its card: {form.Cards[1].Summary}");
        if (flagged && form.Cards[1].Current is { } twin)
        {
            form.IgnorePad(twin);
            Check(GameSettings.Load().IgnoredPads.Contains(twin.Guid), "Ignore this controller -> IgnoredPads in settings.json");
            var s = GameSettings.Load();
            s.IgnoredPads = [];
            s.Save();
        }

        //assignment round-trips through settings.json (the field the game reads)
        var dsGuid = pads.List().First(p => p.Name.Contains("DualSense")).Guid;
        form.ChoosePad(1, dsGuid);
        Check(GameSettings.Load().PadDevice(1) == dsGuid, $"P2 set to the DualSense -> settings.json PadDevice2 = {dsGuid}");
        form.ChoosePad(1, "");
        Check(GameSettings.Load().PadDevice(1) == "", "P2 back to automatic -> PadDevice2 empty");

        //an unknown pad is mapped with the wizard and then has a layout
        pads.AttachVirtual("un", "unmapped");
        Pump(form, 800);
        var unknown = pads.List().First(p => p.Name.StartsWith("Unknown Test Pad"));
        Check(!unknown.Mapped, "the unknown pad starts without a layout");
        using (var capture = pads.Capture(unknown))
        {
            var wizard = new MappingWizard(capture);
            var button = 0;
            while (!wizard.Done)
            {
                if (wizard.Current.Axis)
                {
                    wizard.Skip();
                    continue;
                }

                var b = button++;
                pads.PressVirtual("un", b, true);
                Pump(form, 60);
                var took = false;
                for (var i = 0; i < 20 && !took; i++)
                {
                    took = wizard.Poll();
                    Pump(form, 20);
                }

                pads.PressVirtual("un", b, false);
                Pump(form, 60);
                wizard.Poll();
                if (!took)
                {
                    Check(false, $"the wizard took no input for {wizard.Current.Sdl}");
                    return;
                }
            }

            pads.SaveMapping(unknown, wizard.Binds);
            Console.WriteLine($"[SelfTest] mapping: {string.Join(",", wizard.Binds.Select(b => $"{b.Target}:{b.Source}"))}");
        }

        var line = File.ReadAllLines(Paths.UserMappings).LastOrDefault() ?? "";
        Check(line.StartsWith(unknown.Guid) && line.Contains("a:b0") && line.Contains("dpdown:b5"), $"user mapping file: {line}");
        Pump(form, 500);
        Check(pads.List().First(p => p.Guid == unknown.Guid).Mapped, "the unknown pad has a layout now");
        form.Close();
    }
}
