using System.Runtime.InteropServices;
using System.Text.Json.Nodes;

namespace KnowYourRole.Launcher;

internal static class Program
{
    public static string Version => typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    public const string Usage =
        """
        KnowYourRole                         first run: setup (pick the disc, build); later: the launcher
        KnowYourRole --play                  starts the game directly (what the desktop shortcut does)
        KnowYourRole --cue <disc.cue> [--out <runtime folder>] [--diagnostic]
                                             builds without a window
        KnowYourRole --collect-diagnostics [--zip <file>]
                                             packs the logs into a zip to attach to a bug report
        KnowYourRole --launcher              opens the launcher even when "skip the launcher" is on
        """;

    [STAThread]
    private static int Main(string[] args)
    {
        string? cue = null, output = null, zip = null, selftest = null;
        bool diagnostic = false, collect = false, play = false, launcher = false;
        for (var i = 0; i < args.Length; i++)
        {
            var more = i + 1 < args.Length;
            switch (args[i].ToLowerInvariant())
            {
                case "--cue" when more: cue = args[++i]; break;
                case "--out" when more: output = args[++i]; break;
                case "--zip" when more: zip = args[++i]; break;
                case "--diagnostic": diagnostic = true; break;
                case "--collect-diagnostics": collect = true; break;
                case "--play": play = true; break;
                case "--launcher": launcher = true; break;
                case "--selftest" when more: selftest = args[++i]; break;
                default:
                    AttachConsole(-1);
                    Console.Error.WriteLine($"unknown argument: {args[i]}");
                    Console.Error.WriteLine(Usage);
                    return 1;
            }
        }

        if (output != null) Paths.Runtime = Path.GetFullPath(output);

        if (collect)
        {
            AttachConsole(-1);
            var path = DiagnosticsCollector.Collect(zip);
            Console.WriteLine($"diagnostics written to {path}");
            return 0;
        }

        if (cue != null)
        {
            AttachConsole(-1);
            if (Paths.NotWritableReason(Paths.Root) is { } why)
            {
                Console.Error.WriteLine($"{why}. Extract the game to a folder you can write to (for example C:\\Games\\KnowYourRole) and start it from there.");
                return 1;
            }

            using var log = new BuildLog(Console.Out);
            Migration.CarryOver(log);
            var options = new BuildOptions(cue, output ?? BuildOptions.DefaultOutput, diagnostic);
            var job = new BuildJob(options, log, new Progress<BuildProgress>());
            var ok = job.Run();
            if (ok) SaveBuildTime();
            return ok ? 0 : 1;
        }

        if (play)
        {
            //straight into the game; the exit code is the game's (tests start the game this way)
            AttachConsole(-1);
            if (Paths.NotWritableReason(Paths.Root) is { } where)
            {
                Console.Error.WriteLine($"{where}. Extract the game to a folder you can write to and start it from there.");
                return 1;
            }

            var state = Install.Check();
            if (state != InstallState.Ready)
            {
                Console.Error.WriteLine($"the game is not ready ({Install.Describe(state)}); start KnowYourRole.exe without --play to set it up");
                return 2;
            }

            try
            {
                return GameRunner.RunAndWait();
            }
            catch (InvalidOperationException e)
            {
                Console.Error.WriteLine(e.Message);
                ApplicationConfiguration.Initialize();
                VcRuntimeDialog.ShowProblem(null, e);
                return 1;
            }
        }

        ApplicationConfiguration.Initialize();
        if (selftest != null)
        {
            AttachConsole(-1);
            return SelfTest.Run(selftest);
        }

        if (Paths.NotWritableReason(Paths.Root) is { } reason)
        {
            MessageBox.Show($"Know Your Role Recomp cannot run from here: {reason}.\n\nExtract the zip to a folder you can " +
                            @"write to (for example C:\Games\KnowYourRole) and start KnowYourRole.exe from there.",
                "Know Your Role Recomp", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return 1;
        }

        var carried = Migration.CarryOver();
        var ready = Install.Check();
        if (ready == InstallState.Ready && LauncherConfig.Load().SkipLauncher && !launcher)
        {
            try
            {
                return GameRunner.RunAndWait();
            }
            catch (InvalidOperationException e)
            {
                //the launcher opens instead, so the problem can be dealt with there
                VcRuntimeDialog.ShowProblem(null, e);
            }
        }

        Application.Run(ready == InstallState.Ready ? new MainForm() : new SetupForm(ready, carried));
        return 0;
    }

    public static void SaveBuildTime()
    {
        if (!File.Exists(Paths.BuildStamp)) return;
        var seconds = (int?)JsonNode.Parse(File.ReadAllText(Paths.BuildStamp))?["Seconds"] ?? 0;
        if (seconds <= 0) return;
        var cfg = LauncherConfig.Load();
        cfg.LastBuildSeconds = seconds;
        cfg.Save();
    }

    //a windowed executable has no console of its own; this writes to the one it was started from, if any
    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int processId);
}
