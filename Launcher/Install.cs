using System.Diagnostics;
using System.Text.Json.Nodes;

namespace KnowYourRole.Launcher;

public enum InstallState
{
    Ready,
    NotBuilt,
    //built by another version (an update was unzipped over the old one): the game is rebuilt from the same disc
    OtherVersion,
    //the disc image is no longer where it was when the game was built
    DiscMissing
}

public static class Install
{
    public static InstallState Check()
    {
        if (!File.Exists(Path.Combine(Paths.Runtime, "Recompiled.Entry.dll")) || !File.Exists(Paths.BuildStamp))
            return InstallState.NotBuilt;
        var version = (string?)JsonNode.Parse(File.ReadAllText(Paths.BuildStamp))?["Version"];
        if (version != Program.Version) return InstallState.OtherVersion;
        var cue = GameSettings.Load().CdPath;
        if (cue.Length == 0 || !File.Exists(cue)) return InstallState.DiscMissing;
        return InstallState.Ready;
    }

    public static string Describe(InstallState s) => s switch
    {
        InstallState.Ready => "ready",
        InstallState.NotBuilt => "the game has not been built yet",
        InstallState.OtherVersion => "the game was built by another version and has to be built again",
        _ => "the disc image was moved or deleted"
    };

    public static string BuiltFrom() => GameSettings.Load().CdPath;
}

//the game runs as its own process from runtime\, with its settings, memory cards and logs in user\
public static class GameRunner
{
    public static Process Start()
    {
        if (SystemCheck.MissingVcRuntime() is { } missing)
            throw new VcRuntimeMissingException(missing);
        Directory.CreateDirectory(Paths.Logs);
        var psi = new ProcessStartInfo(Paths.GameExe)
        {
            WorkingDirectory = Paths.Runtime,
            UseShellExecute = false
        };
        //a test run may have pointed these elsewhere already
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RECOMPONE_DATA_DIR"))) psi.Environment["RECOMPONE_DATA_DIR"] = Paths.User;
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RECOMPONE_LOG_DIR"))) psi.Environment["RECOMPONE_LOG_DIR"] = Paths.Logs;
        try
        {
            return Process.Start(psi) ?? throw new InvalidOperationException($"{Paths.GameExe} did not start");
        }
        catch (System.ComponentModel.Win32Exception e)
        {
            throw new InvalidOperationException($"the game could not be started ({Paths.GameExe}): {e.Message}", e);
        }
    }

    public static int RunAndWait()
    {
        using var p = Start();
        p.WaitForExit();
        return p.ExitCode;
    }
}
