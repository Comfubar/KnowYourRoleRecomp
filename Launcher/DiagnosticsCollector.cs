using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace KnowYourRole.Launcher;

//packs what a bug report needs into one zip: the build logs, the game's session logs and crash reports, the settings
//and a description of the PC. Saves, the disc and the built game are never included. The Windows user name, the user
//folder and the PC name are replaced in every file.
public static class DiagnosticsCollector
{
    private const int MaxGameLogs = 30;

    public static string Collect(string? zipPath = null)
    {
        Directory.CreateDirectory(Paths.User);
        zipPath ??= Path.Combine(Paths.User, $"diagnostics_{DateTime.Now:yyyy-MM-dd_HHmmss}.zip");
        using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);

        Add(zip, "system.txt", SystemReport());
        foreach (var pattern in new[] { "builder_*.log", "game_*.log", "crash_*.txt", "error_*.txt", "hang_*.txt" })
        foreach (var f in Newest(Paths.Logs, pattern, MaxGameLogs))
            AddFile(zip, "logs/" + Path.GetFileName(f), f);
        foreach (var f in new[] { Paths.Settings, Paths.InterfaceIni, Paths.LauncherConfig, Paths.UserMappings, Paths.BuildStamp })
            if (File.Exists(f)) AddFile(zip, "user/" + Path.GetFileName(f), f);

        return zipPath;
    }

    private static string SystemReport()
    {
        var sb = new StringBuilder();
        var (total, free) = SystemCheck.Ram();
        sb.AppendLine($"collected {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"launcher {Program.Version}");
        sb.AppendLine($"os {Environment.OSVersion} ({(Environment.Is64BitOperatingSystem ? "64" : "32")} bit)");
        sb.AppendLine($".net {Environment.Version}");
        sb.AppendLine($"cpus {Environment.ProcessorCount}");
        sb.AppendLine($"ram {total} MB, free {free} MB");
        sb.AppendLine($"folder {Paths.Root}, free disk {SystemCheck.FreeDiskMB(Paths.Root)} MB");
        sb.AppendLine($"install state: {Install.Describe(Install.Check())}");
        if (!Directory.Exists(Paths.Runtime)) return sb.ToString();
        sb.AppendLine("runtime files (name, size, modified):");
        foreach (var f in Directory.GetFiles(Paths.Runtime).Select(f => new FileInfo(f)).OrderBy(f => f.Name))
            sb.AppendLine($"  {f.Name} {f.Length} {f.LastWriteTime:yyyy-MM-dd HH:mm}");
        return sb.ToString();
    }

    private static IEnumerable<string> Newest(string folder, string pattern, int count)
    {
        return Directory.Exists(folder)
            ? Directory.GetFiles(folder, pattern).OrderByDescending(File.GetLastWriteTimeUtc).Take(count)
            : [];
    }

    private static void AddFile(ZipArchive zip, string entry, string path)
    {
        string text;
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            text = reader.ReadToEnd();
        }
        catch (IOException e)
        {
            text = $"(could not read {Path.GetFileName(path)}: {e.Message})";
        }

        Add(zip, entry, text);
    }

    private static void Add(ZipArchive zip, string entry, string text)
    {
        using var writer = new StreamWriter(zip.CreateEntry(entry).Open(), new UTF8Encoding(false));
        writer.Write(Redact(text));
    }

    public static string Redact(string text)
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (profile.Length > 0)
        {
            text = text.Replace(profile, "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
            text = text.Replace(profile.Replace('\\', '/'), "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
            text = text.Replace(profile.Replace("\\", "\\\\"), "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
        }

        foreach (var secret in new[] { Environment.UserName, Environment.MachineName })
            if (secret.Length >= 3)
                text = Regex.Replace(text, $@"\b{Regex.Escape(secret)}\b", "<redacted>", RegexOptions.IgnoreCase);
        return text;
    }
}
