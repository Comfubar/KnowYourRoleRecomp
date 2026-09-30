namespace KnowYourRole.Launcher;

public static class Shortcut
{
    //a desktop shortcut that starts the game directly (KnowYourRole.exe --play), through the Windows Script Host
    public static string CreateOnDesktop()
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var lnk = Path.Combine(desktop, "Know Your Role Recomp.lnk");
        var type = Type.GetTypeFromProgID("WScript.Shell") ??
                   throw new InvalidOperationException("the Windows Script Host is not available on this PC");
        dynamic shell = Activator.CreateInstance(type)!;
        dynamic link = shell.CreateShortcut(lnk);
        link.TargetPath = Path.Combine(Paths.Root, "KnowYourRole.exe");
        link.Arguments = "--play";
        link.WorkingDirectory = Paths.Root;
        link.IconLocation = Path.Combine(Paths.Root, "KnowYourRole.exe") + ",0";
        link.Description = "Know Your Role Recomp";
        link.Save();
        return lnk;
    }
}

public static class LauncherIcon
{
    //the executable's own icon (an original drawing of a wrestling ring, see Launcher\app.ico)
    public static Icon? Load() => Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? Application.ExecutablePath);
}
