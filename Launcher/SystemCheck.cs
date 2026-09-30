using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace KnowYourRole.Launcher;

//the build compiles one program at a time inside this process; measured on the release: peak working set about 850 MB
//(0.73 GB private). The game then uses about 0.8 GB, up to about 1.2 GB, so 4 GB in total is asked for and less than
//1.5 GB free gets a warning
public static class SystemCheck
{
    public const long MinTotalRamMB = 4096;
    public const long WarnFreeRamMB = 1536;
    public const long MinFreeDiskMB = 1536;

    //the game's window (GLFW), sound (OpenAL Soft) and file dialog libraries need the Microsoft Visual C++ runtime; most
    //PCs have it from other programs, a fresh Windows install may not
    //settable so the self test can ask for a file no PC has and see the setup and PLAY paths react
    public static IReadOnlyList<string> VcRuntimeFiles { get; set; } = ["vcruntime140.dll", "vcruntime140_1.dll", "msvcp140.dll"];
    public const string VcRuntimeUrl = "https://aka.ms/vs/17/release/vc_redist.x64.exe";

    //null when Windows can find all of them (in the game's runtime folder or the system)
    public static string? MissingVcRuntime() => MissingVcRuntime(VcRuntimeFiles);

    public static string? MissingVcRuntime(IEnumerable<string> files)
    {
        var missing = files.Where(f =>
        {
            if (File.Exists(Path.Combine(Paths.Runtime, f))) return false;
            if (!NativeLibrary.TryLoad(f, out var handle)) return true;
            NativeLibrary.Free(handle);
            return false;
        }).ToList();
        return missing.Count == 0 ? null : string.Join(", ", missing);
    }

    public static string VcRuntimeMessage(string missing) =>
        $"The Microsoft Visual C++ Redistributable (x64) is not installed on this PC (missing: {missing}). The game needs " +
        $"it for its window and sound. Install it from Microsoft ({VcRuntimeUrl}) and start the game again.";

    //opens a web page in the player's browser; the self test swaps it to see which address a button would open
    public static Action<string> OpenUrl { get; set; } =
        url => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

    //the built game is about 250 MB, including the code built from the disc; in a OneDrive folder it would all be uploaded
    public static string? OneDriveFolderOf(string folder)
    {
        var full = Path.GetFullPath(folder).TrimEnd('\\') + "\\";
        return OneDriveRoots().FirstOrDefault(r => full.StartsWith(r.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase));
    }

    public static string OneDriveWarning(string oneDrive) =>
        $"Note: this folder is inside OneDrive ({oneDrive}), which would upload the built game (about 250 MB). " +
        @"A folder outside OneDrive, for example C:\Games\KnowYourRole, is better.";

    //the folders OneDrive keeps in sync: the ones it names in the environment and each signed-in account's folder
    public static IEnumerable<string> OneDriveRoots()
    {
        var roots = new List<string>();
        foreach (var v in new[] { "OneDrive", "OneDriveConsumer", "OneDriveCommercial" })
            if (Environment.GetEnvironmentVariable(v) is { Length: > 0 } dir) roots.Add(dir);
        using (var accounts = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\OneDrive\Accounts"))
        {
            foreach (var name in accounts?.GetSubKeyNames() ?? [])
            {
                using var account = accounts!.OpenSubKey(name);
                if (account?.GetValue("UserFolder") is string { Length: > 0 } dir) roots.Add(dir);
            }
        }

        return roots.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase);
    }

    public static (long totalMB, long freeMB) Ram()
    {
        var s = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (!GlobalMemoryStatusEx(ref s)) return (0, 0);
        return ((long)(s.TotalPhys / 1048576), (long)(s.AvailPhys / 1048576));
    }

    public static long FreeDiskMB(string folder)
    {
        var root = Path.GetPathRoot(Path.GetFullPath(folder))!;
        return new DriveInfo(root).AvailableFreeSpace / 1048576;
    }

    //null when the build can go ahead; warnings go to the log
    public static string? Check(string output, BuildLog log)
    {
        var (total, free) = Ram();
        var disk = FreeDiskMB(output);
        log.Write($"[System] {Environment.OSVersion}, {Environment.ProcessorCount} cpus, ram {total} MB (free {free} MB), " +
                  $"free disk on {Path.GetPathRoot(Path.GetFullPath(output))} {disk} MB");
        if (OneDriveFolderOf(output) is { } oneDrive)
            log.Write("[System] " + OneDriveWarning(oneDrive));
        if (total > 0 && total < MinTotalRamMB)
            return $"this PC has {total} MB of memory, the build needs at least {MinTotalRamMB} MB";
        if (free > 0 && free < WarnFreeRamMB)
            log.Write($"[System] warning: only {free} MB of memory is free, close other programs if the build fails or " +
                      "is very slow");
        if (disk < MinFreeDiskMB)
            return $"only {disk} MB free on the output drive, the build needs {MinFreeDiskMB} MB";
        return null;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);
}

//the game cannot start without the Visual C++ runtime; callers offer Microsoft's installer
public sealed class VcRuntimeMissingException(string missing) : InvalidOperationException(SystemCheck.VcRuntimeMessage(missing));

//the message about the missing Visual C++ runtime, with a button that opens Microsoft's installer in the browser
public sealed class VcRuntimeDialog : Form
{
    public Button InstallButton { get; } = new() { Text = "Install it", AutoSize = true };

    public VcRuntimeDialog(string message)
    {
        Text = "Know Your Role Recomp";
        Icon = LauncherIcon.Load();
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        var close = new Button { Text = "Close", AutoSize = true, DialogResult = DialogResult.Cancel };
        InstallButton.Click += (_, _) =>
        {
            SystemCheck.OpenUrl(SystemCheck.VcRuntimeUrl);
            DialogResult = DialogResult.OK;
        };
        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        buttons.Controls.Add(close);
        buttons.Controls.Add(InstallButton);
        var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Padding = new Padding(12) };
        layout.Controls.Add(new Label { Text = message, AutoSize = true, MaximumSize = new Size(480, 0), Margin = new Padding(3, 3, 3, 12) });
        layout.Controls.Add(buttons);
        Controls.Add(layout);
        AcceptButton = InstallButton;
        CancelButton = close;
    }

    //shows the problem that stopped the game from starting: the runtime dialog when that is it, a message otherwise
    public static void ShowProblem(IWin32Window? owner, InvalidOperationException e)
    {
        if (e is VcRuntimeMissingException)
        {
            using var d = new VcRuntimeDialog(e.Message);
            d.ShowDialog(owner);
            return;
        }

        MessageBox.Show(owner, e.Message, "Know Your Role Recomp", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
