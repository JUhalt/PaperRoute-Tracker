using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using PaperRoute.ViewModels.Platform;

namespace PaperRoute.Desktop.Platform;

// The real actions of "--platform-check" (step 15): Avalonia's file dialog
// and launcher on the trial's window, and System.Diagnostics.Process for the
// restart. Kept thin on purpose: no test runs this class, because each call
// opens a dialog, a browser or a second program. Everything around it is
// tested with stand-ins (PlatformCheckTests).
public sealed class AvaloniaPlatformActions : IPlatformActions
{
    private readonly TopLevel _topLevel;

    public AvaloniaPlatformActions(TopLevel topLevel)
    {
        ArgumentNullException.ThrowIfNull(topLevel);
        _topLevel = topLevel;
    }

    public bool CanOpenFileDialog => _topLevel.StorageProvider.CanOpen;

    // Only the names are handed back, for counting; no file is opened.
    public async Task<IReadOnlyList<string>> OpenFileDialogAsync()
    {
        IReadOnlyList<IStorageFile> files = await _topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "PaperRoute Board Trial platform check: choose any file, or cancel",
            AllowMultiple = true,
        });
        var names = new List<string>(files.Count);
        foreach (IStorageFile file in files)
        {
            names.Add(file.Name);
            file.Dispose();
        }

        return names;
    }

    public Task<bool> LaunchAsync(Uri address) => _topLevel.Launcher.LaunchUriAsync(address);

    public IStartedProgram StartProgram(string fileName, IReadOnlyList<string> arguments)
    {
        var start = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        Process process = Process.Start(start) ?? throw new InvalidOperationException("The system started no process.");
        try
        {
            process.OutputDataReceived += (_, _) => { };
            process.ErrorDataReceived += (_, _) => { };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            return new StartedProcess(process);
        }
        catch
        {
            // A started program that nothing would hold is stopped.
            var started = new StartedProcess(process);
            try
            {
                started.Stop();
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
            {
                // It is reported by the exception thrown on below.
            }

            started.Dispose();
            throw;
        }
    }

    private sealed class StartedProcess(Process process) : IStartedProgram
    {
        public int ExitCode => process.ExitCode;

        public async Task<bool> WaitForExitAsync(TimeSpan limit)
        {
            using var cancel = new CancellationTokenSource(limit);
            try
            {
                await process.WaitForExitAsync(cancel.Token);
                return true;
            }
            catch (OperationCanceledException)
            {
                return process.HasExited;
            }
        }

        public void Stop()
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
        }

        public void Dispose() => process.Dispose();
    }
}
