using System.Diagnostics;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using RecompOne.Runtime.Cdrom;

namespace KnowYourRole.Launcher;

//Output: the runtime folder (the game runtime ships there; the build adds the recompiled game to it)
public sealed record BuildOptions(string Cue, string Output, bool Diagnostic)
{
    public static string DefaultOutput => Paths.Runtime;
}

public sealed record BuildProgress(int Step, int Steps, string Text, TimeSpan Elapsed = default, TimeSpan Estimate = default);

//disc check -> recompile (sources only) -> compile every program -> settings, build stamp
public sealed class BuildJob(BuildOptions options, BuildLog log, IProgress<BuildProgress> progress)
{
    public const string GameExe = Paths.GameExeName;
    private const string GameHost = Paths.GameDll;
    private const string Prefix = "Recompiled.";

    //the build takes about this long; the last build's time replaces it
    public TimeSpan Estimate { get; init; } = TimeSpan.FromSeconds(160);
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    //the images are compiled in dependency order: Common <- main <- each program <- Entry
    private const string Common = "Common", Main = "main", Entry = "Entry";

    private string PortConfig => Path.Combine(options.Output, "port", "config", "KnowYourRole.json");

    private int _step, _steps = 4;

    public bool Run()
    {
        var total = Stopwatch.StartNew();
        log.Write($"[Builder] {Assembly.GetExecutingAssembly().GetName().Version} disc={options.Cue} " +
                  $"output={options.Output} diagnostic={options.Diagnostic}");
        try
        {
            Build();
            log.Write($"[Builder] done in {total.Elapsed:mm\\:ss}, start {Path.Combine(options.Output, GameExe)}");
            Report("Done");
            return true;
        }
        catch (BuildException e)
        {
            log.Write($"[Builder] FAILED: {e.Message}");
            Report($"Failed: {e.Message}");
            return false;
        }
        catch (Exception e)
        {
            log.Write($"[Builder] FAILED with an unexpected error: {e}");
            Report($"Failed: {e.Message} (see {log.Path})");
            return false;
        }
    }

    private void Build()
    {
        if (!File.Exists(Path.Combine(options.Output, GameExe)) || !File.Exists(PortConfig))
            throw new BuildException($"the game runtime is missing ({options.Output}); unpack the whole release zip and " +
                                     "start KnowYourRole.exe from there");

        Report("Checking this PC");
        Directory.CreateDirectory(options.Output);
        var problem = SystemCheck.Check(options.Output, log);
        if (problem != null) throw new BuildException(problem);

        Report("Checking the disc");
        CheckDisc();

        var work = Path.Combine(options.Output, "build-work");
        var sources = Path.Combine(work, "generated");
        if (Directory.Exists(work)) Directory.Delete(work, true);
        Report("Recompiling the game code");
        Recompile(sources);

        var images = Directory.GetDirectories(sources).Select(Path.GetFileName).OfType<string>()
            .OrderBy(n => n == Common ? 0 : n == Main ? 1 : n == Entry ? 3 : 2).ThenBy(n => n, StringComparer.Ordinal)
            .ToList();
        _steps += images.Count;
        foreach (var old in Directory.GetFiles(options.Output, $"{Prefix}*"))
            File.Delete(old);
        foreach (var image in images)
        {
            Compile(image, Path.Combine(sources, image));
            //one program's syntax trees are well over a GB, they go before the next one is parsed
            GC.Collect(2, GCCollectionMode.Forced, true, true);
        }

        WriteSettings();
        WriteStamp();
        if (options.Diagnostic) log.Write($"[Builder] diagnostic mode: the generated sources stay in {work}");
        else Directory.Delete(work, true);
    }

    private void CheckDisc()
    {
        var result = DiscVerify.Check(options.Cue);
        foreach (var (file, hash) in result.Hashes)
        {
            log.Write($"[Disc] {file} sha256 {hash}");
            _discHashes[file] = hash;
        }

        foreach (var w in result.Warnings) log.Write($"[Disc] warning: {w}");
        if (!result.Ok) throw new BuildException(result.Message);
    }

    private void Recompile(string sources)
    {
        var recompiler = typeof(RecompOne.Recompiler.CodeGen.ProjectLayoutWriter).Assembly.EntryPoint!;
        string[] args = [PortConfig, "-cue", Path.GetFullPath(options.Cue), "-out", sources, "-sources-only"];
        log.Write($"[Recompiler] {string.Join(' ', args)}");

        var stdout = Console.Out;
        var stderr = Console.Error;
        using var writer = log.Writer(line =>
        {
            if (line.Contains("emiting ")) Report($"Recompiling: {line[(line.IndexOf("emiting ") + 8)..]}", false);
        });
        Console.SetOut(writer);
        Console.SetError(writer);
        object? result;
        try
        {
            result = recompiler.Invoke(null, [args]);
        }
        catch (TargetInvocationException e) when (e.InnerException != null)
        {
            throw new BuildException($"the recompiler failed: {e.InnerException.Message}");
        }
        finally
        {
            writer.Flush();
            Console.SetOut(stdout);
            Console.SetError(stderr);
        }

        if (result is not 0) throw new BuildException($"the recompiler failed (exit code {result}), see {log.Path}");
    }

    private void Compile(string image, string folder)
    {
        var name = Prefix + image;
        Report($"Compiling {image}");
        var watch = Stopwatch.StartNew();

        var parse = new CSharpParseOptions(LanguageVersion.Latest);
        var files = Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories);
        var trees = new SyntaxTree[files.Length + 1];
        Parallel.For(0, files.Length, i =>
        {
            using var stream = File.OpenRead(files[i]);
            trees[i] = CSharpSyntaxTree.ParseText(SourceText.From(stream, Encoding.UTF8,
                canBeEmbedded: options.Diagnostic), parse, files[i]);
        });
        //the SDK's implicit usings, which the generated project files turn on
        trees[^1] = CSharpSyntaxTree.ParseText(SourceText.From(
            "global using global::System; global using global::System.Collections.Generic; global using global::System.IO; " +
            "global using global::System.Linq; global using global::System.Net.Http; global using global::System.Threading; " +
            "global using global::System.Threading.Tasks;", Encoding.UTF8), parse, "ImplicitUsings.cs");

        var compilation = CSharpCompilation.Create(name, trees, References(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: options.Diagnostic ? OptimizationLevel.Debug : OptimizationLevel.Release,
                nullableContextOptions: NullableContextOptions.Disable,
                concurrentBuild: true));

        var dll = Path.Combine(options.Output, name + ".dll");
        var pdb = options.Diagnostic ? Path.Combine(options.Output, name + ".pdb") : null;
        Microsoft.CodeAnalysis.Emit.EmitResult result;
        using (var peStream = File.Create(dll))
        using (var pdbStream = pdb == null ? null : File.Create(pdb))
        {
            result = compilation.Emit(peStream, pdbStream,
                options: pdb == null ? null : new Microsoft.CodeAnalysis.Emit.EmitOptions(
                    debugInformationFormat: Microsoft.CodeAnalysis.Emit.DebugInformationFormat.PortablePdb));
        }

        var errors = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        foreach (var e in errors.Take(50)) log.Write($"[Compile] {image}: {e}");
        if (!result.Success)
        {
            File.Delete(dll);
            throw new BuildException($"{image} did not compile ({errors.Count} errors), see {log.Path}");
        }

        var proc = Process.GetCurrentProcess();
        log.Write($"[Compile] {image}: {files.Length} files, {new FileInfo(dll).Length / 1024} KB, " +
                  $"{watch.Elapsed.TotalSeconds:F1} s, {result.Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning)} " +
                  $"warnings, peak working set {proc.PeakWorkingSet64 / 1048576} MB");
    }

    //every managed assembly in the runtime folder: the .NET runtime it ships with, RecompOne.Runtime and its libraries,
    //and the programs compiled so far. The game host executable is left out.
    private List<MetadataReference> References()
    {
        var refs = new List<MetadataReference>();
        foreach (var dll in Directory.GetFiles(options.Output, "*.dll"))
        {
            if (string.Equals(Path.GetFileName(dll), GameHost, StringComparison.OrdinalIgnoreCase)) continue;
            if (!IsManaged(dll)) continue;
            refs.Add(MetadataReference.CreateFromFile(dll));
        }

        return refs;
    }

    private static bool IsManaged(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var pe = new PEReader(stream);
            return pe.HasMetadata;
        }
        catch (BadImageFormatException)
        {
            return false;
        }
    }

    //the game starts from the disc it was built from; everything else in settings.json is left alone
    private void WriteSettings()
    {
        var settings = GameSettings.Load();
        settings.CdPath = Path.GetFullPath(options.Cue);
        settings.Save();
        log.Write($"[Builder] {Paths.Settings}: CdPath set");
    }

    //runtimeuild.json: which version built the game from which disc; the launcher rebuilds after an update
    private void WriteStamp()
    {
        var stamp = new JsonObject
        {
            ["Version"] = Program.Version,
            ["Built"] = DateTime.Now.ToString("s"),
            ["Seconds"] = (int)_clock.Elapsed.TotalSeconds,
            ["Disc"] = new JsonObject(_discHashes.Select(h => KeyValuePair.Create(h.Key, (JsonNode?)JsonValue.Create(h.Value))))
        };
        File.WriteAllText(Path.Combine(options.Output, "build.json"), stamp.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }

    private readonly Dictionary<string, string> _discHashes = [];

    private void Report(string text, bool step = true)
    {
        if (step) _step++;
        progress.Report(new BuildProgress(Math.Min(_step, _steps), _steps, text, _clock.Elapsed, Estimate));
    }
}

public sealed class BuildException(string message) : Exception(message);
