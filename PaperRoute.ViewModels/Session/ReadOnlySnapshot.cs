using System.Globalization;

namespace PaperRoute.ViewModels.Session;

// Lock 2 of the trial design (d1 1.3): the library a person names is only ever
// read, never opened by the core. Copy reads the files a library's Board needs
// from a folder that holds data\ and writes them into a folder inside the
// trial's isolated session; the trial then loads the copy.
//
// - Each source file is opened for reading only, with ReadWrite and Delete
//   sharing, so a running PaperRoute can still save over it. On an IOException
//   the read is tried three more times, 100 ms apart; after that the file is
//   skipped and named.
// - Nothing here takes a source path for writing: the only writes are into the
//   destination, which must lie inside the session folder given (full paths
//   compared), and must not overlap the source.
// - The managed library (attachments) is not copied: the Board reads none.
//
// A source that is, or lies inside, one of the real profile's default storage
// folders is refused before anything is read (RefusalFor): opening the user's
// own library is a decision the trial has not taken (d1 section 8, step 14).
// The default folders are worked out from the folder names, as the core does,
// never through a root resolver.
public static class ReadOnlySnapshot
{
    // The files copied, relative to the source, in the order they are read
    // (AppSettingsService.vb:32-33, ManuscriptRepository.vb:90, 96,
    // AuthorLibraryRepository.vb:69, 75, StorageMigrationService.vb:151).
    public static IReadOnlyList<string> FileNames { get; } =
    [
        "settings.json",
        "settings.bak",
        "schema.json",
        Path.Combine("data", "manuscripts.json"),
        Path.Combine("data", "manuscripts.bak"),
        Path.Combine("data", "authors.json"),
        Path.Combine("data", "authors.bak"),
    ];

    public const string DataFolderName = "data";

    private const int ExtraAttempts = 3;
    private static readonly TimeSpan s_retryWait = TimeSpan.FromMilliseconds(100);

    // Folder names the core uses under %LOCALAPPDATA% and Documents, in the
    // standard and the development profile, current and legacy
    // (ProductInfo.vb:14-30; StorageMigrationService.vb:87-132).
    private static readonly string[] s_dataFolderNames =
    [
        ProductInfo.DataFolderName,
        ProductInfo.DevelopmentDataFolderName,
        ProductInfo.LegacyDataFolderName,
        ProductInfo.DevelopmentLegacyDataFolderName,
    ];

    private static readonly string[] s_managedFolderNames =
    [
        ProductInfo.ManagedLibraryFolderName,
        ProductInfo.DevelopmentManagedLibraryFolderName,
        ProductInfo.LegacyManagedLibraryFolderName,
        ProductInfo.DevelopmentLegacyManagedLibraryFolderName,
    ];

    // The real profile's default storage folders: the data roots under
    // %LOCALAPPDATA% and the managed-library roots under Documents, for both
    // profiles, current and legacy. Paths only; nothing is read.
    public static IReadOnlyList<string> DefaultStorageRoots()
    {
        var roots = new List<string>();
        AddUnder(roots, Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), s_dataFolderNames);
        AddUnder(roots, Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), s_managedFolderNames);
        return roots;
    }

    // Why the folder cannot be copied, in plain words, or null when it can.
    // Only a plain local path is accepted (IsPlainLocalPath). The default
    // storage roots are checked first, on the path as given, before anything
    // is read; then the path with its links and short names resolved; then
    // that the folder exists and holds data\. alsoRefuse adds folders to the
    // default roots (tests name a fake profile); it can only refuse more.
    //
    // What this cannot refuse: a library its owner moved out of the default
    // folders (finding it would mean reading the real settings), and a
    // default folder reached through a drive letter made with subst or a
    // mapped drive.
    public static string? RefusalFor(string source, IEnumerable<string>? alsoRefuse = null)
    {
        if (string.IsNullOrWhiteSpace(source) || !Path.IsPathFullyQualified(source))
        {
            return "The Board trial needs the full path of a library folder, such as C:\\Libraries\\Test.";
        }

        string full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(source));
        if (!IsPlainLocalPath(full))
        {
            return LocalFolderMessage;
        }

        string[] refused = [.. DefaultStorageRoots(), .. (alsoRefuse ?? []).Select(item => Path.GetFullPath(item))];
        string? root = RefusedRootFor(full, refused, out string real);
        if (root is null && !IsPlainLocalPath(real))
        {
            return LocalFolderMessage;
        }

        if (root is not null)
        {
            return $"The Board trial does not open your own PaperRoute library. The folder {full} is, or lies inside, " +
                $"PaperRoute's storage folder {root}. Choose a library folder made for testing.";
        }

        if (!Directory.Exists(full))
        {
            return $"The Board trial found no folder at {full}.";
        }

        if (!Directory.Exists(Path.Combine(full, DataFolderName)))
        {
            return $"The folder {full} holds no \"{DataFolderName}\" folder, so it is not a PaperRoute library.";
        }

        return null;
    }

    // Copies the library at source into destination, which must lie inside
    // sessionRoot. Throws SnapshotRefusedException, having read and written
    // nothing, when the source is refused (RefusalFor), when the destination
    // lies outside the session, or when the two overlap.
    public static SnapshotCopy Copy(string source, string destination, string sessionRoot, IEnumerable<string>? alsoRefuse = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionRoot);

        List<string> refused = [.. alsoRefuse ?? []];
        string? refusal = RefusalFor(source, refused);
        if (refusal is not null)
        {
            throw new SnapshotRefusedException(refusal);
        }

        string from = Path.TrimEndingDirectorySeparator(Path.GetFullPath(source));
        if (!Path.IsPathFullyQualified(destination) || !Path.IsPathFullyQualified(sessionRoot))
        {
            throw new SnapshotRefusedException("The snapshot needs full paths for its destination and session.");
        }

        string to = Path.TrimEndingDirectorySeparator(Path.GetFullPath(destination));
        string session = Path.TrimEndingDirectorySeparator(Path.GetFullPath(sessionRoot));
        if (!TrialStartup.IsInside(to, session) || string.Equals(to, session, StringComparison.OrdinalIgnoreCase))
        {
            throw new SnapshotRefusedException($"The snapshot writes only inside the trial's session folder {session}, not into {to}.");
        }

        if (TrialStartup.IsInside(to, from) || TrialStartup.IsInside(from, to) || TrialStartup.IsInside(from, session))
        {
            throw new SnapshotRefusedException($"The snapshot's source {from} and its destination {to} overlap.");
        }

        if (Directory.Exists(to) && Directory.EnumerateFileSystemEntries(to).Any())
        {
            throw new SnapshotRefusedException($"The snapshot's destination {to} is not empty.");
        }

        string[] protectedRoots = [.. DefaultStorageRoots(), .. refused.Select(item => Path.GetFullPath(item))];
        var copied = new List<string>();
        var skipped = new List<string>();
        DateTime takenAt = DateTime.Now;
        foreach (string name in FileNames)
        {
            string sourceFile = Path.Combine(from, name);
            if (!File.Exists(sourceFile))
            {
                continue;
            }

            // A file that is a link into the real profile is refused too, and
            // so is one whose path or target is not a plain local path.
            if (!IsPlainLocalPath(sourceFile))
            {
                throw new SnapshotRefusedException(LocalFolderMessage);
            }

            string real = RealPath(sourceFile, protectedRoots);
            if (!IsPlainLocalPath(real))
            {
                throw new SnapshotRefusedException(LocalFolderMessage);
            }

            if (protectedRoots.Any(item => TrialStartup.IsInside(real, item)))
            {
                throw new SnapshotRefusedException(
                    $"The Board trial does not open your own PaperRoute library. The file {sourceFile} leads into PaperRoute's storage folder.");
            }

            string target = Path.Combine(to, name);
            if (!TrialStartup.IsInside(target, session))
            {
                throw new SnapshotRefusedException($"The snapshot writes only inside the trial's session folder {session}.");
            }

            if (CopyOne(sourceFile, target))
            {
                copied.Add(name);
            }
            else
            {
                skipped.Add(name);
            }
        }

        Directory.CreateDirectory(Path.Combine(to, DataFolderName));
        return new SnapshotCopy(from, to, takenAt, copied, skipped);
    }

    // True when copied; false when the file stayed unreadable after the
    // retries, or vanished, or may not be read.
    private static bool CopyOne(string sourceFile, string target)
    {
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                using var reader = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                using var writer = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None);
                reader.CopyTo(writer);
                return true;
            }
            catch (FileNotFoundException)
            {
                return false;
            }
            catch (DirectoryNotFoundException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (IOException) when (attempt < ExtraAttempts)
            {
                Thread.Sleep(s_retryWait);
            }
            catch (IOException)
            {
                return false;
            }
        }
    }

    private static void AddUnder(List<string> roots, string parent, string[] names)
    {
        if (string.IsNullOrEmpty(parent))
        {
            return;
        }

        foreach (string name in names)
        {
            roots.Add(Path.Combine(parent, name));
        }
    }

    internal const string LocalFolderMessage = "The Board trial needs a local folder path such as C:\\Libraries\\Test.";

    // True for a full path the comparisons with the storage folders can be
    // trusted on. On Windows that is a path on a drive letter ("C:\..."), with
    // no ':' after the drive; everything else is refused: the \\?\ and \\.\
    // device prefixes, UNC paths (a loopback share such as \\localhost\C$
    // reaches the same folders) and the NTFS stream form
    // "folder::$INDEX_ALLOCATION". On Linux and macOS a folder has no second
    // spelling of that kind (links are resolved by RealPath), so any path
    // from the root ("/...") is plain.
    internal static bool IsPlainLocalPath(string full) =>
        OperatingSystem.IsWindows()
            ? full.Length >= 3 && char.IsAsciiLetter(full[0]) && full[1] == ':' && full[2] == Path.DirectorySeparatorChar &&
                full.IndexOf(':', 2) < 0
            : full.Length >= 1 && full[0] == Path.DirectorySeparatorChar;

    // The refused folder that full (a plain local path) is, or lies inside,
    // on the path as given or with its links and short names resolved; null
    // when none. real is the resolved path.
    internal static string? RefusedRootFor(string full, string[] refused, out string real)
    {
        string? root = refused.FirstOrDefault(item => TrialStartup.IsInside(full, item));
        if (root is not null)
        {
            real = full;
            return root;
        }

        string resolved = RealPath(full, refused);
        real = resolved;
        return refused.FirstOrDefault(item => TrialStartup.IsInside(resolved, item) || TrialStartup.IsInside(resolved, RealParent(item, refused)));
    }

    // The folder that holds a default root, with its links and short names
    // resolved, joined to the root's own name. The root itself is never
    // looked into.
    private static string RealParent(string root, string[] refused)
    {
        string? parent = Path.GetDirectoryName(root);
        return parent is null ? root : Path.Combine(RealPath(parent, refused), Path.GetFileName(root));
    }

    // The path with each existing part replaced by its name on disk (a short
    // 8.3 name becomes the long one) and each link (junction or symbolic link)
    // by its final target. It stops early, returning the path reached, once
    // that lies inside a refused folder, so it never lists one.
    private static string RealPath(string path, string[] refused, int depth = 0)
    {
        string full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        string? drive = Path.GetPathRoot(full);
        if (string.IsNullOrEmpty(drive) || depth > 32)
        {
            return full;
        }

        string current = drive;
        string[] parts = full[drive.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        for (int index = 0; index < parts.Length; index++)
        {
            if (refused.Any(item => TrialStartup.IsInside(current, item)))
            {
                return Path.Combine([current, .. parts[index..]]);
            }

            string part = parts[index];
            FileSystemInfo? entry = null;
            if (part.IndexOfAny(['*', '?']) < 0)
            {
                try
                {
                    entry = new DirectoryInfo(current).EnumerateFileSystemInfos(part).FirstOrDefault();
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                {
                    entry = null;
                }
            }

            if (entry is null)
            {
                return Path.Combine([current, .. parts[index..]]);
            }

            string next = Path.Combine(current, entry.Name);
            if (entry.LinkTarget is not null)
            {
                FileSystemInfo? target = null;
                try
                {
                    target = entry.ResolveLinkTarget(returnFinalTarget: true);
                }
                catch (IOException)
                {
                    target = null;
                }

                if (target is not null)
                {
                    next = RealPath(target.FullName, refused, depth + 1);
                }
            }

            current = next;
        }

        return Path.TrimEndingDirectorySeparator(current);
    }

    // The banner's clock (d1 1.3: "taken at h:mm").
    internal static string TimeText(DateTime takenAt) => takenAt.ToString("h:mm tt", CultureInfo.CurrentCulture);
}

// What a snapshot copied: the folder read, the folder written inside the
// session, when, and the files copied and skipped (relative to the library).
public sealed record SnapshotCopy(
    string SourceFolder,
    string DestinationFolder,
    DateTime TakenAt,
    IReadOnlyList<string> Copied,
    IReadOnlyList<string> Skipped);

// A snapshot session's library: the folder a person named, known from the
// start, and the copy, known once the load has made it.
public sealed class LibrarySnapshot
{
    private SnapshotCopy? _copy;

    public LibrarySnapshot(string sourceFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFolder);
        SourceFolder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(sourceFolder));
    }

    public LibrarySnapshot(SnapshotCopy copy)
        : this(copy?.SourceFolder ?? throw new ArgumentNullException(nameof(copy)))
    {
        _copy = copy;
    }

    public string SourceFolder { get; }

    // Null until the copy is made.
    public SnapshotCopy? Copy => Volatile.Read(ref _copy);

    internal void Took(SnapshotCopy copy) => Volatile.Write(ref _copy, copy);
}

// A snapshot the trial will not take; the message is for people.
public sealed class SnapshotRefusedException : InvalidOperationException
{
    public SnapshotRefusedException()
    {
    }

    public SnapshotRefusedException(string message)
        : base(message)
    {
    }

    public SnapshotRefusedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
