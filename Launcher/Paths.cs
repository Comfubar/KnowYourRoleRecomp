namespace KnowYourRole.Launcher;

//the player's folder: KnowYourRole.exe (this launcher) and README.txt, and one folder KnowYourRole-files\ with
//everything else: runtime\ (the game runtime and, once built, the recompiled game), user\ (settings, memory cards,
//custom controller mappings, logs) and the licence notices. Updating = unzipping a new version over the old one:
//runtime\ is replaced, user\ is kept.
public static class Paths
{
    public const string FilesFolderName = "KnowYourRole-files";
    public static string Root { get; set; } = AppContext.BaseDirectory;
    public static string Files => Path.Combine(Root, FilesFolderName);
    public static string Runtime { get; set; } = Path.Combine(AppContext.BaseDirectory, FilesFolderName, "runtime");

    public static string User => Path.Combine(Files, "user");
    public static string Logs => Path.Combine(User, "logs");
    public static string GameExe => Path.Combine(Runtime, GameExeName);
    public const string GameExeName = "KnowYourRole.Game.exe";
    public const string GameDll = "KnowYourRole.Game.dll";
    public static string PortConfig => Path.Combine(Runtime, "port", "config", "KnowYourRole.json");
    public static string BuildStamp => Path.Combine(Runtime, "build.json");
    public static string Settings => Path.Combine(User, "settings.json");
    public static string InterfaceIni => Path.Combine(User, "interface.ini");
    public static string UserMappings => Path.Combine(User, "gamecontrollerdb.user.txt");
    public static string BundledMappings => Path.Combine(Runtime, "gamecontrollerdb.txt");
    public static string LauncherConfig => Path.Combine(User, "launcher.json");

    //Windows starts no program whose path is longer than 260 characters, and the deepest file in the game folder is
    //about 85 characters below it (KnowYourRole-files\runtime\...)
    public const int MaxRootLength = 160;

    //a folder the game can live in: not inside Program Files or another place Windows keeps read-only, and not so deep
    //that the game's files end up past Windows' path length limit
    public static string? NotWritableReason(string folder)
    {
        var full = Path.GetFullPath(folder);
        if (full.TrimEnd(Path.DirectorySeparatorChar).Length > MaxRootLength)
            return $"the folder's path is {full.TrimEnd(Path.DirectorySeparatorChar).Length} characters long; Windows cannot start " +
                   $"programs that deep (keep it under {MaxRootLength}, for example C:\\Games\\KnowYourRole)";
        foreach (var special in new[]
                 {
                     Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86,
                     Environment.SpecialFolder.Windows
                 })
        {
            var p = Environment.GetFolderPath(special);
            if (p.Length > 0 && full.StartsWith(p + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return $"the game is in {p}, where Windows does not let programs write files";
        }

        try
        {
            Directory.CreateDirectory(folder);
            var probe = Path.Combine(folder, $".write-test-{Environment.ProcessId}");
            File.WriteAllText(probe, "");
            File.Delete(probe);
            return null;
        }
        catch (Exception e) when (e is UnauthorizedAccessException or IOException)
        {
            return $"this folder cannot be written to ({e.Message})";
        }
    }
}
