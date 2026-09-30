using System.Runtime.InteropServices;
using Silk.NET.SDL;

namespace KnowYourRole.Launcher.Controllers;

//the controllers as the game will see them: SDL with the same hints and mapping files as RecompOne's InputManager.
//Used by the launcher's controller cards (list, live input, duplicates) and by "map this controller".
public sealed unsafe class PadService : IDisposable
{
    public sealed record Pad(int Index, int Instance, string Guid, string Name, string Family, string Connection,
        bool Mapped, bool Virtual);

    private readonly Sdl _sdl;
    private readonly Dictionary<int, nint> _open = []; //instance id -> SDL_GameController*
    private readonly RecompOne.Runtime.Input.MirrorDetector _mirrors = new(8);
    private readonly Dictionary<int, int> _mirrorOf = []; //instance -> instance it mirrors
    public int MappingsLoaded { get; }

    public PadService()
    {
        //the launcher is one file; the game's SDL2.dll in runtime\ is the same library, loaded first so SDL's binding
        //finds it (Windows reuses a loaded module for the same name)
        var sdlDll = Path.Combine(Paths.Runtime, "SDL2.dll");
        if (File.Exists(sdlDll)) NativeLibrary.Load(sdlDll);
        _sdl = Sdl.GetApi();
        //the same backends as the game (see InputManager.Initialize); events also while another window has the focus
        _sdl.SetHint("SDL_JOYSTICK_RAWINPUT", "0");
        _sdl.SetHint("SDL_JOYSTICK_HIDAPI", "1");
        _sdl.SetHint("SDL_JOYSTICK_HIDAPI_PS4_RUMBLE", "0");
        _sdl.SetHint("SDL_JOYSTICK_HIDAPI_PS5_RUMBLE", "0");
        _sdl.SetHint("SDL_GAMECONTROLLER_USE_BUTTON_LABELS", "0");
        _sdl.SetHint("SDL_JOYSTICK_ALLOW_BACKGROUND_EVENTS", "1");
        if (_sdl.Init(Sdl.InitGamecontroller | Sdl.InitJoystick) != 0)
            throw new InvalidOperationException($"SDL could not start: {_sdl.GetErrorS()}");
        MappingsLoaded = AddMappings(Paths.BundledMappings) + AddMappings(Paths.UserMappings);
    }

    public string Version
    {
        get
        {
            Silk.NET.SDL.Version v;
            _sdl.GetVersion(&v);
            return $"{v.Major}.{v.Minor}.{v.Patch}";
        }
    }

    public int AddMappings(string path)
    {
        if (!File.Exists(path)) return 0;
        var n = 0;
        foreach (var raw in File.ReadLines(path))
        {
            var m = raw.Trim();
            if (m.Length == 0 || m.StartsWith('#')) continue;
            if (m.Contains("platform:", StringComparison.Ordinal) && !m.Contains("platform:Windows,", StringComparison.Ordinal)) continue;
            if (_sdl.GameControllerAddMapping(m) >= 0) n++;
        }

        return n;
    }

    //handles hotplug; call regularly (the launcher's timer)
    public void Update()
    {
        Event ev;
        while (_sdl.PollEvent(&ev) != 0)
        {
        }

        _sdl.GameControllerUpdate();
    }

    public List<Pad> List()
    {
        var list = new List<Pad>();
        var n = _sdl.NumJoysticks();
        for (var i = 0; i < n; i++)
        {
            var guid = Guid(i);
            var mapped = _sdl.IsGameController(i) == SdlBool.True;
            var name = mapped ? _sdl.GameControllerNameForIndexS(i) : _sdl.JoystickNameForIndexS(i);
            var type = mapped ? _sdl.GameControllerTypeForIndex(i) : GameControllerType.Unknown;
            var instance = _sdl.JoystickGetDeviceInstanceID(i);
            list.Add(new Pad(i, instance, guid, string.IsNullOrWhiteSpace(name) ? $"Controller {i + 1}" : name,
                RecompOne.Runtime.Input.PadInfo.FamilyName(type), RecompOne.Runtime.Input.PadInfo.Connection(guid),
                mapped, guid.Length == 32 && guid.Substring(28, 2) == "76"));
        }

        return list;
    }

    private string Guid(int index)
    {
        var g = _sdl.JoystickGetDeviceGUID(index);
        var text = new byte[33];
        fixed (byte* t = text) _sdl.JoystickGetGUIDString(g, t, text.Length);
        var len = Array.IndexOf(text, (byte)0);
        return System.Text.Encoding.ASCII.GetString(text, 0, len < 0 ? 32 : len);
    }

    private GameController* Open(Pad pad)
    {
        if (_open.TryGetValue(pad.Instance, out var c)) return (GameController*)c;
        var ctrl = _sdl.GameControllerOpen(pad.Index);
        if (ctrl != null) _open[pad.Instance] = (nint)ctrl;
        return ctrl;
    }

    //PlayStation names of the buttons held right now, by position (south = Cross on every pad), and the left stick
    public (List<string> Buttons, float LeftX, float LeftY) State(Pad pad)
    {
        var held = new List<string>();
        if (!pad.Mapped) return (held, 0, 0);
        var c = Open(pad);
        if (c == null) return (held, 0, 0);
        for (var b = 0; b < (int)GameControllerButton.Max; b++)
            if (_sdl.GameControllerGetButton(c, (GameControllerButton)b) != 0)
                held.Add(PlayStationName(b));
        if (_sdl.GameControllerGetAxis(c, GameControllerAxis.Triggerleft) > 8000) held.Add("L2");
        if (_sdl.GameControllerGetAxis(c, GameControllerAxis.Triggerright) > 8000) held.Add("R2");
        return (held, _sdl.GameControllerGetAxis(c, GameControllerAxis.Leftx) / 32768f,
            _sdl.GameControllerGetAxis(c, GameControllerAxis.Lefty) / 32768f);
    }

    public static string PlayStationName(int sdlButton) => sdlButton switch
    {
        0 => "Cross", 1 => "Circle", 2 => "Square", 3 => "Triangle", 4 => "Select", 5 => "Guide", 6 => "Start",
        7 => "L3", 8 => "R3", 9 => "L1", 10 => "R1", 11 => "Up", 12 => "Down", 13 => "Left", 14 => "Right",
        _ => $"button {sdlButton}"
    };

    //which pad is a second view of another one (a remapper); checked on every Update with the pads' buttons
    public int? MirrorOf(Pad pad) => _mirrorOf.TryGetValue(pad.Instance, out var m) ? m : null;

    public void CheckMirrors(List<Pad> pads)
    {
        var mapped = pads.Where(p => p.Mapped).Take(8).ToList();
        var masks = new uint[8];
        var open = new bool[8];
        for (var i = 0; i < mapped.Count; i++)
        {
            open[i] = true;
            var c = Open(mapped[i]);
            if (c == null) continue;
            for (var b = 0; b < (int)GameControllerButton.Max; b++)
                if (_sdl.GameControllerGetButton(c, (GameControllerButton)b) != 0)
                    masks[i] |= 1u << b;
        }

        if (Environment.GetEnvironmentVariable("KYR_MIRROR_TRACE") == "1" && masks.Any(m => m != 0))
            Console.WriteLine($"[Mirror] {string.Join(" ", mapped.Select((p, i) => $"{p.Name}:{masks[i]:X}"))}");
        var mirror = _mirrors.Observe(masks, open);
        if (mirror < 0) return;
        var twin = Array.FindIndex(open, o => o);
        _mirrorOf[mapped[mirror].Instance] = mapped[twin].Instance;
        _mirrors.Reset();
    }

    //raw input of an unmapped (or any) pad for "map this controller": the first control that changes from its resting
    //state, as an SDL mapping source ("b3", "h0.1", "a2", "+a4", "-a4")
    public sealed class RawCapture : IDisposable
    {
        private readonly Sdl _sdl;
        private readonly Joystick* _joy;
        private readonly short[] _rest;
        private readonly bool[] _heldAtStart;

        public RawCapture(Sdl sdl, int index)
        {
            _sdl = sdl;
            _joy = sdl.JoystickOpen(index);
            if (_joy == null) throw new InvalidOperationException($"SDL could not open the controller: {sdl.GetErrorS()}");
            sdl.JoystickUpdate();
            _rest = new short[sdl.JoystickNumAxes(_joy)];
            for (var a = 0; a < _rest.Length; a++) _rest[a] = sdl.JoystickGetAxis(_joy, a);
            _heldAtStart = new bool[sdl.JoystickNumButtons(_joy)];
            for (var b = 0; b < _heldAtStart.Length; b++) _heldAtStart[b] = sdl.JoystickGetButton(_joy, b) != 0;
        }

        public string? Poll()
        {
            _sdl.JoystickUpdate();
            for (var b = 0; b < _heldAtStart.Length; b++)
            {
                var down = _sdl.JoystickGetButton(_joy, b) != 0;
                if (down && !_heldAtStart[b]) return $"b{b}";
                if (!down) _heldAtStart[b] = false;
            }

            for (var h = 0; h < _sdl.JoystickNumHats(_joy); h++)
            {
                var v = _sdl.JoystickGetHat(_joy, h);
                if (v is 1 or 2 or 4 or 8) return $"h{h}.{v}";
            }

            for (var a = 0; a < _rest.Length; a++)
            {
                var d = _sdl.JoystickGetAxis(_joy, a) - _rest[a];
                if (Math.Abs(d) < 16000) continue;
                //a trigger rests at one end, a stick in the middle
                if (Math.Abs(_rest[a]) > 20000) return $"a{a}";
                return d > 0 ? $"+a{a}" : $"-a{a}";
            }

            return null;
        }

        //nothing held any more (so the next target does not take the same press)
        public bool Released()
        {
            _sdl.JoystickUpdate();
            for (var b = 0; b < _heldAtStart.Length; b++)
                if (_sdl.JoystickGetButton(_joy, b) != 0) return false;
            for (var h = 0; h < _sdl.JoystickNumHats(_joy); h++)
                if (_sdl.JoystickGetHat(_joy, h) != 0) return false;
            for (var a = 0; a < _rest.Length; a++)
                if (Math.Abs(_sdl.JoystickGetAxis(_joy, a) - _rest[a]) > 8000) return false;
            return true;
        }

        public void Dispose() => _sdl.JoystickClose(_joy);
    }

    public RawCapture Capture(Pad pad) => new(_sdl, pad.Index);

    //saves a mapping to the user mapping file (replacing an older one for the same GUID) and applies it now
    public void SaveMapping(Pad pad, IEnumerable<(string Target, string Source)> binds)
    {
        var line = string.Join(",", new[] { pad.Guid, pad.Name.Replace(",", " ") }
            .Concat(binds.Select(b => $"{b.Target}:{b.Source}")).Append("platform:Windows")) + ",";
        Directory.CreateDirectory(Paths.User);
        var lines = File.Exists(Paths.UserMappings) ? File.ReadAllLines(Paths.UserMappings).ToList() : [];
        lines.RemoveAll(l => l.StartsWith(pad.Guid + ",", StringComparison.OrdinalIgnoreCase));
        if (lines.Count == 0) lines.Add("# controller mappings made with \"Map this controller\" (SDL_GameControllerDB format)");
        lines.Add(line);
        File.WriteAllLines(Paths.UserMappings, lines);
        if (_sdl.GameControllerAddMapping(line) < 0)
            throw new InvalidOperationException($"SDL did not accept the mapping: {_sdl.GetErrorS()}");
        CloseAll();
    }

    //test hook (launcher --selftest): SDL virtual pads, see RecompOne.Runtime.Input.VirtualPads
    public void AttachVirtual(string id, string family) =>
        RecompOne.Runtime.Input.VirtualPads.Attach(_sdl, id, RecompOne.Runtime.Input.VirtualPads.FindFamily(family) ??
                                                            throw new ArgumentException($"no family {family}"), "");

    public void PressVirtual(string id, int button, bool down) =>
        RecompOne.Runtime.Input.VirtualPads.SetButtons(_sdl, id, [button], down);

    public void DetachVirtual(string id) => RecompOne.Runtime.Input.VirtualPads.Detach(_sdl, id);

    private void CloseAll()
    {
        foreach (var c in _open.Values) _sdl.GameControllerClose((GameController*)c);
        _open.Clear();
    }

    public void Dispose()
    {
        CloseAll();
        if (RecompOne.Runtime.Input.VirtualPads.Count > 0) RecompOne.Runtime.Input.VirtualPads.DetachAll(_sdl);
        _sdl.Quit();
    }
}
