namespace KnowYourRole.Launcher.Controllers;

//"map this controller": one control after another, the player presses what they want for it; the result is an SDL
//mapping line (the SDL_GameControllerDB format) saved to user\gamecontrollerdb.user.txt
public sealed class MappingWizard(PadService.RawCapture capture)
{
    public sealed record Target(string Sdl, string Prompt, bool Axis = false);

    public static readonly Target[] Targets =
    [
        new("a", "Cross (the bottom face button)"),
        new("b", "Circle (the right face button)"),
        new("x", "Square (the left face button)"),
        new("y", "Triangle (the top face button)"),
        new("dpup", "D-pad up"),
        new("dpdown", "D-pad down"),
        new("dpleft", "D-pad left"),
        new("dpright", "D-pad right"),
        new("leftshoulder", "L1"),
        new("rightshoulder", "R1"),
        new("lefttrigger", "L2"),
        new("righttrigger", "R2"),
        new("back", "Select"),
        new("start", "Start"),
        new("leftstick", "L3 (press the left stick)"),
        new("rightstick", "R3 (press the right stick)"),
        new("leftx", "left stick: push it RIGHT", true),
        new("lefty", "left stick: push it DOWN", true),
        new("rightx", "right stick: push it RIGHT", true),
        new("righty", "right stick: push it DOWN", true)
    ];

    private readonly List<(string, string)> _binds = [];
    private bool _waitRelease;

    public int Index { get; private set; }
    public bool Done => Index >= Targets.Length;
    public Target Current => Targets[Math.Min(Index, Targets.Length - 1)];
    public IReadOnlyList<(string Target, string Source)> Binds => _binds;

    //call regularly; true when the current control was taken
    public bool Poll()
    {
        if (Done) return false;
        if (_waitRelease)
        {
            if (!capture.Released()) return false;
            _waitRelease = false;
        }

        var src = capture.Poll();
        if (src == null) return false;
        _binds.Add((Current.Sdl, Source(Current, src)));
        Index++;
        _waitRelease = true;
        return true;
    }

    //the player has no such control
    public void Skip()
    {
        if (!Done) Index++;
    }

    //a stick direction becomes the whole axis ("~" when pushing right/down made it go negative)
    private static string Source(Target t, string raw)
    {
        if (!t.Axis) return raw;
        if (raw.StartsWith('-')) return raw[1..] + "~";
        return raw.TrimStart('+');
    }
}
