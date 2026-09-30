namespace KnowYourRole.Launcher;

//the disc's data track against the known dump of the NTSC-U release (one MODE2/2352 track, the CRC-32 Redump lists for
//it). A mismatch is a warning: the game code is checked exactly by DiscVerify, a different dump of the other data
//usually plays fine but is not what was tested.
public static class TrackCheck
{
    public const long KnownSize = 746_266_080;
    public const uint KnownCrc32 = 0x0368F8F7;

    public static List<string> Compare(string cue)
    {
        var warnings = new List<string>();
        var files = CueFiles(cue);
        if (files.Count != 1)
        {
            warnings.Add($"the disc image has {files.Count} track files, the known dump has one; it was not compared.");
            return warnings;
        }

        var bin = files[0];
        if (!File.Exists(bin))
        {
            warnings.Add($"{Path.GetFileName(bin)} named in the .cue was not found.");
            return warnings;
        }

        var size = new FileInfo(bin).Length;
        if (size != KnownSize)
        {
            warnings.Add($"the data track is {size} bytes, the known dump is {KnownSize}: another dump of the disc.");
            return warnings;
        }

        var crc = Crc32(bin);
        if (crc != KnownCrc32)
            warnings.Add($"the data track's CRC-32 is {crc:X8}, the known dump's is {KnownCrc32:X8}: another dump of the disc.");
        return warnings;
    }

    private static List<string> CueFiles(string cue)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(cue))!;
        var list = new List<string>();
        foreach (var raw in File.ReadAllLines(cue))
        {
            var l = raw.Trim();
            if (!l.StartsWith("FILE ", StringComparison.OrdinalIgnoreCase)) continue;
            var a = l.IndexOf('"');
            var b = l.LastIndexOf('"');
            var name = a >= 0 && b > a ? l[(a + 1)..b] : l.Split(' ', StringSplitOptions.RemoveEmptyEntries)[1];
            list.Add(Path.Combine(dir, name));
        }

        return list;
    }

    private static readonly uint[] Table = MakeTable();

    private static uint[] MakeTable()
    {
        var t = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            var c = i;
            for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            t[i] = c;
        }

        return t;
    }

    public static uint Crc32(string path)
    {
        var crc = 0xFFFFFFFFu;
        var buf = new byte[1 << 20];
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16, FileOptions.SequentialScan);
        int n;
        while ((n = fs.Read(buf, 0, buf.Length)) > 0)
            for (var i = 0; i < n; i++)
                crc = Table[(crc ^ buf[i]) & 0xFF] ^ (crc >> 8);
        return ~crc;
    }
}
