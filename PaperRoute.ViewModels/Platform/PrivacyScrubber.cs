namespace PaperRoute.ViewModels.Platform;

// Takes out of a message (an exception's, as the system wrote it) what the
// platform check must not keep: the program's folder (its file name stays),
// the working folder, the person's home folder, their user name and the
// computer's name. Process.Start's message, for one, names the program's
// full path and the working folder.
public sealed class PrivacyScrubber
{
    public const string ProgramFolder = "<program folder>";
    public const string WorkingFolder = "<working folder>";
    public const string HomeFolder = "<home>";
    public const string UserName = "<user>";
    public const string ComputerName = "<computer>";

    private readonly (string Text, string Replacement)[] _replacements;

    public PrivacyScrubber(string? programFile, string? workingFolder, string? homeFolder, string? userName, string? computerName)
    {
        var replacements = new List<(string, string)>();
        if (!string.IsNullOrWhiteSpace(programFile))
        {
            string name = RestartTarget.FileNameOf(programFile);
            string folder = programFile[..^name.Length].TrimEnd('/', '\\');
            replacements.Add((programFile, name));
            if (folder.Length > 0)
            {
                replacements.Add((folder, ProgramFolder));
            }
        }

        Add(replacements, workingFolder, WorkingFolder);
        Add(replacements, homeFolder, HomeFolder);

        // Longest first, so a folder inside another is replaced whole.
        var ordered = replacements.OrderByDescending(item => item.Item1.Length).ToList();

        // Names last, and only whole enough to mean something.
        if (!string.IsNullOrWhiteSpace(userName) && userName.Trim().Length >= 2)
        {
            ordered.Add((userName.Trim(), UserName));
        }

        if (!string.IsNullOrWhiteSpace(computerName) && computerName.Trim().Length >= 2)
        {
            ordered.Add((computerName.Trim(), ComputerName));
        }

        _replacements = [.. ordered];
    }

    // The scrubber for this process: its program file, the working folder,
    // the home folder, the user and the computer.
    public static PrivacyScrubber ForThisProcess(string? programFile) => new(
        programFile,
        SafeRead(() => Environment.CurrentDirectory),
        SafeRead(() => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)),
        SafeRead(() => Environment.UserName),
        SafeRead(() => Environment.MachineName));

    public string Scrub(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        string result = text;
        foreach ((string find, string replacement) in _replacements)
        {
            result = result.Replace(find, replacement, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }

    private static void Add(List<(string, string)> list, string? folder, string replacement)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        string trimmed = folder.TrimEnd('/', '\\');
        if (trimmed.Length > 1)
        {
            list.Add((trimmed, replacement));
        }
    }

    private static string? SafeRead(Func<string> read)
    {
        try
        {
            return read();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (PlatformNotSupportedException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }
}
