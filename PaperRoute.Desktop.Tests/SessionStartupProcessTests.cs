using System.Diagnostics;
using ManuscriptPipeline.Services;

namespace PaperRoute.Desktop.Tests;

// Rules S2 and S3 (d1 6.2). A storage session is set once per process, so
// the trial's start is checked in a child process: "dotnet PaperRoute.Desktop.dll
// --self-check", started from the Desktop project's own output folder. That
// mode opens no window. It starts the example's isolated session first, loads
// it, checks every storage root, ends the session and exits 0 or 1.
[TestClass]
public sealed class SessionStartupProcessTests
{
    private static readonly TimeSpan s_timeout = TimeSpan.FromMinutes(2);

    [TestMethod]
    public void SelfCheckStartsLoadsAndClosesTheExampleInIsolation()
    {
        SelfCheckRun run = RunSelfCheck();
        string report = run.Describe();

        // S2: every check passed, and the process says so.
        Assert.AreEqual(0, run.ExitCode, report);
        Assert.IsFalse(run.Lines.Any(line => line.StartsWith("FAIL ", StringComparison.Ordinal)), report);
        Assert.Contains("self-check passed", run.Lines, report);
        foreach (string check in new[]
        {
            "isolated session", "session root", "data root", "managed library root",
            "legacy data root", "legacy managed library root", "default roots",
        })
        {
            Assert.IsTrue(run.Lines.Any(line => line.StartsWith($"PASS {check}: ", StringComparison.Ordinal)),
                $"No passing \"{check}\" line.\n{report}");
        }

        Assert.Contains("PASS default roots: 0 default root(s) resolved", run.Lines, report);

        // The example arrives whole: the number the core's own example holds,
        // which is 7 today.
        int expected = ExampleLibraryService.Create(DateTime.Today).Manuscripts.Count;
        Assert.AreEqual(7, expected, "The example library no longer holds 7 manuscripts; update this test and the design.");
        Assert.Contains($"PASS example library: loaded {expected} manuscript(s); the example holds {expected}", run.Lines, report);
        Assert.Contains($"PASS startup notice: Loaded {expected} manuscripts from local storage.", run.Lines, report);

        // Step 5: the example's Board rendered and every filter run on it
        // (TrialStartup's BoardSelfCheck), before the storage checks above,
        // which so cover those renders too.
        string[] boardChecks =
        [
            "board render", "board search", "board stage list", "board stage list unchanged",
            "board stage box closed", "board sort list", "board sort box closed", "board chip", "board clear",
        ];
        foreach (string check in boardChecks)
        {
            Assert.IsTrue(run.Lines.Any(line => line.StartsWith($"PASS {check}: ", StringComparison.Ordinal)),
                $"No passing \"{check}\" line.\n{report}");
        }

        int lastBoard = Array.FindLastIndex(run.Lines, line => line.StartsWith("PASS board ", StringComparison.Ordinal));
        int defaults = Array.FindIndex(run.Lines, line => line.StartsWith("PASS default roots: ", StringComparison.Ordinal));
        Assert.IsLessThan(defaults, lastBoard, "The storage checks run after the Board's.\n" + report);
    }

    [TestMethod]
    public void TheSessionFolderIsGoneAfterANormalExit()
    {
        SelfCheckRun run = RunSelfCheck();
        string report = run.Describe();
        Assert.AreEqual(0, run.ExitCode, report);

        // S3: the folder was a new PaperRoute-Example- child of the temporary
        // folder, and the exit removed it.
        string? line = run.Lines.FirstOrDefault(item => item.StartsWith("session folder: ", StringComparison.Ordinal));
        Assert.IsNotNull(line, report);
        string folder = line["session folder: ".Length..];
        string temporary = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
        Assert.AreEqual(temporary, Path.GetDirectoryName(folder), true, report);
        Assert.StartsWith("PaperRoute-Example-", Path.GetFileName(folder), report);
        Assert.IsFalse(Directory.Exists(folder), $"The session folder {folder} is still there.\n{report}");
        Assert.Contains($"PASS session folder removed: {folder}", run.Lines, report);
    }

    private static SelfCheckRun RunSelfCheck() => RunTrial("--self-check");

    // Runs "dotnet PaperRoute.Desktop.dll <arguments>" with no window; every
    // caller passes "--self-check", or arguments the trial refuses before it
    // opens anything.
    internal static SelfCheckRun RunTrial(params string[] arguments)
    {
        string desktop = DesktopAssemblyPath();
        var start = new ProcessStartInfo(DotnetHost())
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(desktop)!,
        };
        start.ArgumentList.Add(desktop);
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start) ?? throw new InvalidOperationException("The trial did not start.");
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(s_timeout))
        {
            process.Kill(entireProcessTree: true);
            Assert.Fail($"The self-check did not finish within {s_timeout.TotalSeconds} s.");
        }

        process.WaitForExit();
        string text = output.GetAwaiter().GetResult();
        string errors = error.GetAwaiter().GetResult();
        string[] lines = text.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
        return new SelfCheckRun(process.ExitCode, lines, errors);
    }

    // The Desktop project's own output folder, the same configuration as this
    // test run: <repo>\PaperRoute.Desktop\bin\<Configuration>\net10.0.
    internal static string DesktopAssemblyPath()
    {
        string testOutput = Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);
        DirectoryInfo? folder = new(testOutput);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "PaperRoute.Desktop.Tests.csproj")))
        {
            folder = folder.Parent;
        }

        Assert.IsNotNull(folder, $"No PaperRoute.Desktop.Tests project folder above {testOutput}.");
        Assert.IsNotNull(folder.Parent, "The test project has no parent folder.");
        string relative = Path.GetRelativePath(folder.FullName, testOutput);
        string path = Path.Combine(folder.Parent.FullName, "PaperRoute.Desktop", relative, "PaperRoute.Desktop.dll");
        Assert.IsTrue(File.Exists(path), $"The trial was not built at {path}.");
        Assert.IsTrue(File.Exists(Path.ChangeExtension(path, ".runtimeconfig.json")), $"No runtimeconfig beside {path}.");
        return path;
    }

    // The dotnet host that runs these tests, when the SDK names it.
    internal static string DotnetHost()
    {
        string? host = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (!string.IsNullOrEmpty(host) && File.Exists(host))
        {
            return host;
        }

        string? root = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        if (!string.IsNullOrEmpty(root))
        {
            string candidate = Path.Combine(root, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return "dotnet";
    }

    internal sealed record SelfCheckRun(int ExitCode, string[] Lines, string Errors)
    {
        public string Describe() =>
            $"Exit code {ExitCode}.\nOutput:\n{string.Join("\n", Lines)}\nErrors:\n{Errors}";
    }
}
