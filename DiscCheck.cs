using System.Security.Cryptography;
using RecompOne.Runtime.Cdrom;

// The code was recompiled from one version of the disc (NTSC-U, SLUS-01234); another version would not match it.
// The disc picker keeps asking until the chosen disc holds that exact boot executable.
internal static class DiscCheck
{
    public const string BootFile = "SLUS_012.34";
    public const string BootSha256 = "E23ACB3165ED001B6D23558EF81624CEA70B97D98E68A2A93546E6466A3C2A7F";

    private static string? _lastPath;
    private static DateTime _lastWrite;
    private static string? _lastResult;

    // null when the disc is the right one, else the reason it is not; the startup loop calls this every frame
    public static string? Validate(string path)
    {
        var write = File.GetLastWriteTimeUtc(path);
        if (path == _lastPath && write == _lastWrite) return _lastResult;
        _lastPath = path;
        _lastWrite = write;
        _lastResult = Check(path);
        if (_lastResult != null) Console.WriteLine($"[Disc] {path}: {_lastResult}");
        return _lastResult;
    }

    private static string? Check(string path)
    {
        using var fs = DiscFs.Open(path);
        if (!fs.Exists(BootFile)) return $"{BootFile} is not on this disc: this is not the NTSC-U (SLUS-01234) disc";
        var hash = Convert.ToHexString(SHA256.HashData(fs.ReadFile(BootFile)));
        return hash == BootSha256
            ? null
            : $"{BootFile} on this disc is a different version (sha256 {hash}), the port needs the NTSC-U release";
    }
}
