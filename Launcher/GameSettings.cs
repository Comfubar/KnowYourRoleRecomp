using System.Text.Json;
using System.Text.Json.Nodes;

namespace KnowYourRole.Launcher;

//user\settings.json, the file the game reads (RecompOne GameConfig). The launcher edits single fields and keeps every
//other field as it is, so there is one configuration and no second copy that could disagree with it.
public sealed class GameSettings
{
    private readonly JsonObject _json;
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    private GameSettings(JsonObject json) => _json = json;

    public static GameSettings Load()
    {
        if (!File.Exists(Paths.Settings)) return new GameSettings([]);
        try
        {
            return new GameSettings(JsonNode.Parse(File.ReadAllText(Paths.Settings)) as JsonObject ?? []);
        }
        catch (JsonException e)
        {
            //a damaged file is kept next to the new one, never silently dropped
            var bad = Paths.Settings + $".damaged-{DateTime.Now:yyyyMMdd-HHmmss}";
            File.Copy(Paths.Settings, bad, true);
            throw new InvalidDataException($"settings.json could not be read ({e.Message}); a copy was kept as {bad}");
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(Paths.User);
        var tmp = Paths.Settings + ".tmp";
        File.WriteAllText(tmp, _json.ToJsonString(Indented));
        File.Move(tmp, Paths.Settings, true);
    }

    public string CdPath
    {
        get => (string?)_json["CdPath"] ?? "";
        set => _json["CdPath"] = value;
    }

    public string PadDevice(int player) => (string?)_json[DeviceKey(player)] ?? "";
    public void SetPadDevice(int player, string guid) => _json[DeviceKey(player)] = guid;
    private static string DeviceKey(int player) => player == 0 ? "PadDevice" : $"PadDevice{player + 1}";

    public float StickDeadzone
    {
        get => (float?)_json["StickDeadzone"] ?? 0.20f;
        set => _json["StickDeadzone"] = MathF.Round(value, 2);
    }

    //0 Auto, 1 On, 2 Off (RecompOne MultitapMode)
    public int Multitap
    {
        get => (int?)_json["Multitap"] ?? 0;
        set => _json["Multitap"] = value;
    }

    public bool Vibration
    {
        get => (bool?)_json["Vibration"] ?? true;
        set => _json["Vibration"] = value;
    }

    public bool PlayStationBluetoothRumble
    {
        get => (bool?)_json["PlayStationBluetoothRumble"] ?? false;
        set => _json["PlayStationBluetoothRumble"] = value;
    }

    public List<string> IgnoredPads
    {
        get => (_json["IgnoredPads"] as JsonArray)?.Select(n => (string?)n ?? "").Where(s => s.Length > 0).ToList() ?? [];
        set => _json["IgnoredPads"] = new JsonArray(value.Select(v => (JsonNode?)JsonValue.Create(v)).ToArray());
    }

    //player 2 on the keyboard: the second layout, or none
    public bool Keyboard2
    {
        get => _json["Keys2"] is JsonObject k && k.Any(kv => ((string?)kv.Value ?? "").Length > 0);
        set => _json["Keys2"] = value ? DefaultKeys2() : EmptyKeys();
    }

    private static readonly string[] KeyNames =
        ["Cross", "Circle", "Square", "Triangle", "L1", "R1", "L2", "R2", "L3", "R3", "Start", "Select", "Up", "Down", "Left", "Right"];

    private static JsonObject EmptyKeys() => new(KeyNames.Select(k => KeyValuePair.Create(k, (JsonNode?)JsonValue.Create(""))));

    //the same layout as RecompOne's KeyBindings.DefaultPlayer2
    private static JsonObject DefaultKeys2()
    {
        string[] v = ["K", "L", "J", "I", "U", "O", "Number7", "Number9", "Number8", "Number0", "Backspace", "Backslash",
            "Keypad8", "Keypad5", "Keypad4", "Keypad6"];
        return new JsonObject(KeyNames.Select((k, i) => KeyValuePair.Create(k, (JsonNode?)JsonValue.Create(v[i]))));
    }
}

//user\interface.ini, the window settings the game reads ([RecompOne] section, key=value)
public static class InterfaceIni
{
    public static bool Get(string key, bool fallback)
    {
        var v = Read().GetValueOrDefault(key);
        return v == null ? fallback : v.Equals("True", StringComparison.OrdinalIgnoreCase);
    }

    public static void Set(string key, bool value)
    {
        var lines = File.Exists(Paths.InterfaceIni) ? File.ReadAllLines(Paths.InterfaceIni).ToList() : ["[RecompOne]"];
        var section = lines.FindIndex(l => l.Trim() == "[RecompOne]");
        if (section < 0)
        {
            lines.Insert(0, "[RecompOne]");
            section = 0;
        }

        var end = lines.FindIndex(section + 1, l => l.TrimStart().StartsWith('['));
        if (end < 0) end = lines.Count;
        var at = lines.FindIndex(section + 1, end - section - 1, l => l.StartsWith(key + "=", StringComparison.Ordinal));
        var text = $"{key}={(value ? "True" : "False")}";
        if (at >= 0) lines[at] = text;
        else lines.Insert(section + 1, text);
        Directory.CreateDirectory(Paths.User);
        File.WriteAllLines(Paths.InterfaceIni, lines);
    }

    private static Dictionary<string, string> Read()
    {
        var d = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!File.Exists(Paths.InterfaceIni)) return d;
        var inSection = false;
        foreach (var raw in File.ReadAllLines(Paths.InterfaceIni))
        {
            var l = raw.Trim();
            if (l.StartsWith('[')) inSection = l == "[RecompOne]";
            else if (inSection && l.Contains('=')) d[l[..l.IndexOf('=')]] = l[(l.IndexOf('=') + 1)..];
        }

        return d;
    }
}

//user\launcher.json: the launcher's own choices
public sealed class LauncherConfig
{
    public bool SkipLauncher { get; set; }
    public int LastBuildSeconds { get; set; }

    public static LauncherConfig Load()
    {
        if (!File.Exists(Paths.LauncherConfig)) return new LauncherConfig();
        try
        {
            return JsonSerializer.Deserialize<LauncherConfig>(File.ReadAllText(Paths.LauncherConfig)) ?? new LauncherConfig();
        }
        catch (JsonException e)
        {
            //two preferences live here; a damaged file starts over with the defaults, and says so in the log
            Console.Error.WriteLine($"[Launcher] launcher.json could not be read ({e.Message}), using the defaults");
            return new LauncherConfig();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(Paths.User);
        File.WriteAllText(Paths.LauncherConfig, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
