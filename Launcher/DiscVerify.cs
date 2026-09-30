using System.Security.Cryptography;
using RecompOne.Runtime.Cdrom;

namespace KnowYourRole.Launcher;

//is this the disc the port was made from? The two files the game code comes from must match exactly (the build
//refuses anything else); the disc's tracks are compared with the known dump of the release, and a difference there
//(another dump, a patched audio track) is only a warning.
public static class DiscVerify
{
    public sealed record Result(bool Ok, string Message, IReadOnlyDictionary<string, string> Hashes, IReadOnlyList<string> Warnings);

    //NTSC-U, SLUS-01234
    public static readonly (string File, string Sha256)[] Required =
    [
        ("SLUS_012.34", "E23ACB3165ED001B6D23558EF81624CEA70B97D98E68A2A93546E6466A3C2A7F"),
        ("EXE.PAC", "83CBDC663F1230B6C6B191E5572DB1A9EF22520F65D2C7641C52D6632BAE73A6")
    ];

    public static Result Check(string cue)
    {
        var hashes = new Dictionary<string, string>();
        var warnings = new List<string>();
        if (!File.Exists(cue)) return new Result(false, $"disc image not found: {cue}", hashes, warnings);

        DiscFs fs;
        try
        {
            fs = DiscFs.Open(cue);
        }
        catch (Exception e) when (e is IOException or InvalidDataException or FormatException or NotSupportedException
                                      or UnauthorizedAccessException)
        {
            return new Result(false, $"cannot read {Path.GetFileName(cue)} as a disc image: {e.Message}", hashes, warnings);
        }

        using (fs)
        {
            foreach (var (file, expected) in Required)
            {
                if (!fs.Exists(file))
                    return new Result(false, $"{file} is not on this disc: this is not the NTSC-U (SLUS-01234) release", hashes, warnings);
                var hash = Convert.ToHexString(SHA256.HashData(fs.ReadFile(file)));
                hashes[file] = hash;
                if (hash != expected)
                    return new Result(false, $"{file} on this disc is not the one the port was made from (sha256 {hash[..16]}...); " +
                                             "only the NTSC-U (SLUS-01234) release is supported", hashes, warnings);
            }
        }

        warnings.AddRange(TrackCheck.Compare(cue));
        var msg = "OK: this is the NTSC-U release (SLUS-01234)." +
                  (warnings.Count > 0 ? $" Note: {string.Join(" ", warnings)}" : "");
        return new Result(true, msg, hashes, warnings);
    }
}
