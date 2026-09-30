using System.Reflection;
using RecompOne.Runtime.Memory;

// In the player's folder the game runtime sits in runtime\ next to KnowYourRole.exe (the launcher) and keeps its
// settings, memory cards and logs in user\. The launcher says so through the environment; started directly, the game
// finds that layout by itself.
UsePlayerFolder();

// The recompiled game lives in the Recompiled.*.dll libraries next to the exe (built by the port's project, or by the
// launcher from the player's own disc); Recompiled.Entry registers them all and starts the game. It is loaded here at
// startup, so this exe carries no game code.
var entry = LoadEntry();
if (entry == null) return 2;
RecompOne.Runtime.Runtime.DiscValidator = DiscCheck.Validate;

// Runtime.Run owns the main thread for the host window: it pumps window events and composes/presents the
// frames the game publishes on every VSync. The game itself runs on a worker thread with a 64MB stack,
// since recompiled PS1 code turns guest calls into nested C# calls and can outgrow the default 1MB.
// The exit code is 1 when the game crashed; the crash report is in logs\crash_<timestamp>.txt next to the exe.
return RecompOne.Runtime.Runtime.Run(() =>
{
    var m = new PSMemory();
    entry(m, args.Length > 0 ? args[0] : null, "Know Your Role Recomp");
}, 64 * 1024 * 1024);

static void UsePlayerFolder()
{
    var here = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
    var root = Path.GetDirectoryName(here);
    if (root == null || !Path.GetFileName(here).Equals("runtime", StringComparison.OrdinalIgnoreCase) ||
        !File.Exists(Path.Combine(root, "KnowYourRole.exe"))) return;
    var user = Path.Combine(root, "user");
    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RECOMPONE_DATA_DIR")))
        Environment.SetEnvironmentVariable("RECOMPONE_DATA_DIR", user);
    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RECOMPONE_LOG_DIR")))
        Environment.SetEnvironmentVariable("RECOMPONE_LOG_DIR", Path.Combine(user, "logs"));
}

static Action<IMemory, string?, string?>? LoadEntry()
{
    var path = Path.Combine(AppContext.BaseDirectory, "Recompiled.Entry.dll");
    if (!File.Exists(path))
    {
        Console.Error.WriteLine($"{path} is missing: the recompiled game has not been built yet. Start KnowYourRole.exe " +
                                "and pick your own disc, or build the port project after recompiling.");
        return null;
    }

    // LoadFrom resolves the other Recompiled.*.dll from the same folder
    var run = Assembly.LoadFrom(path).GetType("Recompiled.Entry", true)!.GetMethod("Run")!;
    return run.CreateDelegate<Action<IMemory, string?, string?>>();
}
