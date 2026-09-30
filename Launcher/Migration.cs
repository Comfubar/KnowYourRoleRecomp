namespace KnowYourRole.Launcher;

//carries saves, settings and controller mappings over from an earlier install the first time this version runs:
//  - an earlier layout kept them in Game\ (next to its build program);
//  - another kept them in user\ (next to KnowYourRole.exe);
//  - this version keeps them in KnowYourRole-files\user\.
//Each is looked for in this folder (the new version unzipped over the old one) and in sibling KnowYourRole* folders
//(unzipped next to it). Nothing is overwritten: a file this install already has stays.
//The old folders themselves are left alone (the log names them so the player can delete them).
public static class Migration
{
    private static readonly string[] Files =
        ["carda.sav", "cardb.sav", "settings.json", "interface.ini", "gamecontrollerdb.user.txt", "launcher.json"];

    public static string? CarryOver(BuildLog? log = null)
    {
        if (File.Exists(Path.Combine(Paths.User, "carda.sav")) || File.Exists(Paths.Settings)) return null;
        var from = FindOld();
        if (from == null) return null;

        Directory.CreateDirectory(Paths.User);
        var copied = new List<string>();
        foreach (var f in Files)
        {
            var src = Path.Combine(from, f);
            var dst = Path.Combine(Paths.User, f);
            if (!File.Exists(src) || File.Exists(dst)) continue;
            File.Copy(src, dst);
            copied.Add(f);
        }

        if (copied.Count == 0) return null;
        var msg = $"carried over from {from}: {string.Join(", ", copied)}";
        var old = new[] { "Game", "runtime", "user" }.Select(d => Path.Combine(Paths.Root, d)).Where(Directory.Exists).ToList();
        if (old.Count > 0)
            log?.Write($"[Update] no longer used (can be deleted once the game runs): {string.Join(", ", old)}");
        log?.Write($"[Update] {msg}");
        return msg;
    }

    private static string? FindOld()
    {
        foreach (var here in new[] { Path.Combine(Paths.Root, "user"), Path.Combine(Paths.Root, "Game") })
            if (HasData(here)) return here;

        var parent = Path.GetDirectoryName(Paths.Root.TrimEnd(Path.DirectorySeparatorChar));
        if (parent == null || !Directory.Exists(parent)) return null;
        var self = Path.GetFullPath(Paths.Root).TrimEnd(Path.DirectorySeparatorChar);
        return Directory.GetDirectories(parent, "KnowYourRole*")
            .Where(d => !string.Equals(Path.GetFullPath(d).TrimEnd(Path.DirectorySeparatorChar), self, StringComparison.OrdinalIgnoreCase))
            .SelectMany(d => new[] { Path.Combine(d, Paths.FilesFolderName, "user"), Path.Combine(d, "user"), Path.Combine(d, "Game") })
            .Where(HasData)
            .OrderByDescending(d => File.GetLastWriteTimeUtc(Path.Combine(d, File.Exists(Path.Combine(d, "carda.sav")) ? "carda.sav" : "settings.json")))
            .FirstOrDefault();
    }

    private static bool HasData(string dir) =>
        Directory.Exists(dir) && (File.Exists(Path.Combine(dir, "carda.sav")) || File.Exists(Path.Combine(dir, "settings.json")));
}
