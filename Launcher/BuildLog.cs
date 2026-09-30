using System.Text;

namespace KnowYourRole.Launcher;

//user\logs\builder_<timestamp>.log, every line also goes to the console or window that runs the build
public sealed class BuildLog : IDisposable
{
    private readonly StreamWriter _file;
    private readonly TextWriter? _echo;
    private readonly object _lock = new();

    public event Action<string>? Line;

    //the reason of the last failed build, for the message box
    public static string? LastError { get; private set; }
    public string Path { get; }

    public static string Folder => Paths.Logs;

    public BuildLog(TextWriter? echo = null)
    {
        Directory.CreateDirectory(Folder);
        Path = System.IO.Path.Combine(Folder, $"builder_{DateTime.Now:yyyy-MM-dd_HHmmss}.log");
        _file = new StreamWriter(Path, false, new UTF8Encoding(false)) { AutoFlush = true };
        _echo = echo;
    }

    public void Write(string text)
    {
        if (text.StartsWith("[Builder] FAILED", StringComparison.Ordinal)) LastError = text["[Builder] FAILED".Length..].TrimStart(':', ' ');
        var line = $"{DateTime.Now:HH:mm:ss.fff} {text}";
        lock (_lock)
        {
            _file.WriteLine(line);
            _echo?.WriteLine(line);
        }

        Line?.Invoke(line);
    }

    //a TextWriter that turns everything written to it into log lines (for the recompiler's console output)
    public TextWriter Writer(Action<string>? onLine = null)
    {
        return new LineWriter(s =>
        {
            Write(s);
            onLine?.Invoke(s);
        });
    }

    public void Dispose()
    {
        lock (_lock) _file.Dispose();
    }

    private sealed class LineWriter(Action<string> line) : TextWriter
    {
        private readonly StringBuilder _pending = new();
        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value)
        {
            lock (_pending)
            {
                if (value == '\n')
                {
                    line(_pending.ToString().TrimEnd('\r'));
                    _pending.Clear();
                }
                else _pending.Append(value);
            }
        }

        public override void Flush()
        {
            lock (_pending)
            {
                if (_pending.Length == 0) return;
                line(_pending.ToString());
                _pending.Clear();
            }
        }
    }
}
