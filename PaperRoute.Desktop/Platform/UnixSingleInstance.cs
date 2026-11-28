using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using PaperRoute.ViewModels.Platform;

namespace PaperRoute.Desktop.Platform;

// Single instance on Linux and macOS (start-up design 2.3a, pull request I1).
// Plain .NET; its tests run on Linux and both Macs (UnixHoldTests).
//
// The guarantee is a lock file beside the data root, never inside it:
// "<parent of the root>/<leaf>.instance.lock", its parent folder made first.
// The storage step replaces an empty data root with a legacy one and skips
// that migration without a word when the root is not empty
// (StorageMigrationService.vb:773-785), so a lock file inside would stop it.
// The file is opened with FileShare.None, for which .NET takes flock(LOCK_EX |
// LOCK_NB): a holder elsewhere gives an IOException, and the lock ends when
// the process ends, also after a crash. No key is built from the path's text:
// two spellings of one folder open one file and take one lock, two folders
// take two, whatever the disk's case rule.
//
// "Come forward" goes over a local socket, "paperroute-<first 16 hex digits of
// the id>.sock" in XDG_RUNTIME_DIR on Linux when it names a folder, else the
// temporary folder (the per-user TMPDIR on macOS): not in the data root, since
// a socket path may hold only 108 bytes on Linux and 104 on macOS. The id, 32
// random hex digits, is written beside the lock, once, while holding it. A
// missing, stale or squatted socket can only lose "come forward"; it never lets
// two copies open one library. The socket listens from the moment the hold is
// taken, so a second start during the storage step queues and is served when
// Listen starts.
//
// Release closes and deletes the socket, then the lock. The lock and id files
// stay: deleting the lock file would let a third copy lock a new file while a
// second still holds the old one.
public sealed class UnixSingleInstance : ISingleInstance
{
    // The one line a second copy sends; the holder ignores any other.
    public const string ComeForwardLine = "PaperRoute come-forward 1";

    public const string LockSuffix = ".instance.lock";

    public const string IdSuffix = ".instance.id";

    private const int Backlog = 4;
    private const int IdLength = 32;
    private const int LongestLine = 64;

    // How long the holder waits for a connected copy's line.
    private static readonly TimeSpan s_readLimit = TimeSpan.FromSeconds(2);

    private readonly string? _socketFolder;
    private readonly Action<string> _note;

    // The socket folder is for tests; note takes the line that says come
    // forward is off (the error log, once start-up has one; standard error
    // until then).
    public UnixSingleInstance(string? socketFolder = null, Action<string>? note = null)
    {
        _socketFolder = socketFolder;
        _note = note ?? (line => Console.Error.WriteLine(line));
    }

    // Where this form puts its sockets.
    public string SocketFolder => _socketFolder ?? DefaultSocketFolder();

    public static string DefaultSocketFolder()
    {
        if (OperatingSystem.IsLinux())
        {
            string? runtime = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
            if (!string.IsNullOrEmpty(runtime) && Path.IsPathFullyQualified(runtime) && Directory.Exists(runtime))
            {
                return runtime;
            }
        }

        return Path.GetTempPath();
    }

    public static string LockFileFor(string dataRoot)
    {
        (string parent, string leaf) = Split(dataRoot);
        return Path.Combine(parent, leaf + LockSuffix);
    }

    public static string IdFileFor(string dataRoot)
    {
        (string parent, string leaf) = Split(dataRoot);
        return Path.Combine(parent, leaf + IdSuffix);
    }

    public IInstanceHold? TryHold(string dataRoot)
    {
        string lockPath = LockFileFor(dataRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);

        FileStream lockFile;
        try
        {
            lockFile = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException ex) when (ex is not (DirectoryNotFoundException or FileNotFoundException or PathTooLongException))
        {
            return null;
        }

        try
        {
            string socketPath = SocketPathFor(IdUnderTheLock(IdFileFor(dataRoot)));
            Socket? listener = Bind(socketPath);
            return new UnixInstanceHold(dataRoot, lockFile, listener, listener is null ? null : socketPath);
        }
        catch
        {
            lockFile.Dispose();
            throw;
        }
    }

    public bool AskToComeForward(string dataRoot, TimeSpan limit)
    {
        string? id = ReadId(IdFileFor(dataRoot));
        if (id is null)
        {
            return false;
        }

        try
        {
            var endpoint = new UnixDomainSocketEndPoint(SocketPathFor(id));
            using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            using var connecting = new CancellationTokenSource(limit);
            socket.ConnectAsync(endpoint, connecting.Token).AsTask().GetAwaiter().GetResult();
            socket.SendTimeout = (int)Math.Max(1, limit.TotalMilliseconds);
            socket.Send(Encoding.ASCII.GetBytes(ComeForwardLine + "\n"));
            socket.Shutdown(SocketShutdown.Send);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or SocketException or IOException or OperationCanceledException)
        {
            return false;
        }
    }

    private string SocketPathFor(string id) => Path.Combine(SocketFolder, "paperroute-" + id[..16] + ".sock");

    // Bound and listening at once. A leftover file is deleted first: this copy
    // holds the lock, so nobody serves it. When the socket cannot be made (a
    // path too long for the system, a folder in its place), come forward is
    // off and the hold stands.
    private Socket? Bind(string socketPath)
    {
        Socket? socket = null;
        try
        {
            var endpoint = new UnixDomainSocketEndPoint(socketPath);
            if (File.Exists(socketPath))
            {
                File.Delete(socketPath);
            }

            socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            socket.Bind(endpoint);
            socket.Listen(Backlog);
            return socket;
        }
        catch (Exception ex) when (ex is ArgumentException or SocketException or IOException or UnauthorizedAccessException)
        {
            socket?.Dispose();
            _note($"PaperRoute could not open its come-forward socket, so starting it again will not bring this window forward: {ex.Message}");
            return null;
        }
    }

    // Written only while holding the lock, so two first starts cannot race.
    private static string IdUnderTheLock(string idPath)
    {
        string? id = ReadId(idPath);
        if (id is not null)
        {
            return id;
        }

        id = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(IdLength / 2));
        string written = idPath + ".new";
        File.WriteAllText(written, id + "\n", Encoding.ASCII);
        File.Move(written, idPath, overwrite: true);
        return id;
    }

    private static string? ReadId(string idPath)
    {
        string text;
        try
        {
            if (!File.Exists(idPath))
            {
                return null;
            }

            text = File.ReadAllText(idPath, Encoding.ASCII).Trim();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        return text.Length == IdLength && text.All(char.IsAsciiHexDigit) ? text.ToLowerInvariant() : null;
    }

    private static (string Parent, string Leaf) Split(string dataRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);
        string full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(dataRoot));
        string? parent = Path.GetDirectoryName(full);
        string leaf = Path.GetFileName(full);
        if (string.IsNullOrEmpty(parent) || leaf.Length == 0)
        {
            throw new ArgumentException("A data root needs a parent folder for its lock file.", nameof(dataRoot));
        }

        return (parent, leaf);
    }

    internal static async Task<bool> ReadsComeForwardAsync(Socket client, CancellationToken stop)
    {
        byte[] buffer = new byte[LongestLine + 1];
        int count = 0;
        using var reading = CancellationTokenSource.CreateLinkedTokenSource(stop);
        reading.CancelAfter(s_readLimit);
        try
        {
            while (count < buffer.Length)
            {
                int read = await client.ReceiveAsync(buffer.AsMemory(count), SocketFlags.None, reading.Token).ConfigureAwait(false);
                if (read == 0)
                {
                    return false;
                }

                int end = Array.IndexOf(buffer, (byte)'\n', count, read);
                count += read;
                if (end >= 0)
                {
                    return end == ComeForwardLine.Length && Encoding.ASCII.GetString(buffer, 0, end) == ComeForwardLine;
                }
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or SocketException or ObjectDisposedException)
        {
            return false;
        }

        return false;
    }
}

// A hold of the Unix form: the open lock file, and the listening socket when
// come forward is on.
public sealed class UnixInstanceHold : IInstanceHold
{
    // How long Dispose waits for the listener to stop.
    private static readonly TimeSpan s_stopLimit = TimeSpan.FromSeconds(2);

    [ThreadStatic]
    private static bool t_comingForward;

    private readonly FileStream _lock;
    private readonly Socket? _listener;
    private readonly CancellationTokenSource _stop = new();
    private readonly object _gate = new();
    private Task? _serving;
    private bool _disposed;
    private int _listening;

    internal UnixInstanceHold(string dataRoot, FileStream lockFile, Socket? listener, string? socketPath)
    {
        DataRoot = dataRoot;
        _lock = lockFile;
        _listener = listener;
        SocketPath = socketPath;
    }

    public string DataRoot { get; }

    // The bound socket's path, or null when come forward is off.
    public string? SocketPath { get; }

    public void Listen(Action comeForward)
    {
        ArgumentNullException.ThrowIfNull(comeForward);
        if (Interlocked.Exchange(ref _listening, 1) != 0)
        {
            throw new InvalidOperationException("This hold is already listening.");
        }

        if (_listener is null)
        {
            return;
        }

        lock (_gate)
        {
            if (!_disposed)
            {
                _serving = Task.Run(() => ServeAsync(_listener, comeForward, _stop.Token));
            }
        }
    }

    // One request at a time, in the order they came.
    private static async Task ServeAsync(Socket listener, Action comeForward, CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            Socket client;
            try
            {
                client = await listener.AcceptAsync(stop).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is OperationCanceledException or SocketException or ObjectDisposedException)
            {
                return;
            }

            bool asked;
            using (client)
            {
                asked = await UnixSingleInstance.ReadsComeForwardAsync(client, stop).ConfigureAwait(false);
            }

            if (asked && !stop.IsCancellationRequested)
            {
                t_comingForward = true;
                try
                {
                    comeForward();
                }
                catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
                {
                    // The window is closing; there is nothing to bring forward.
                }
                finally
                {
                    t_comingForward = false;
                }
            }
        }
    }

    // A second call does nothing. The token source is not disposed: the
    // listener may still be ending, and it holds no timer.
    public void Dispose()
    {
        Task? serving;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            serving = _serving;
        }

        _stop.Cancel();
        _listener?.Dispose();
        if (serving is not null && !t_comingForward)
        {
            try
            {
                serving.Wait(s_stopLimit);
            }
            catch (AggregateException)
            {
                // ServeAsync catches what ends it; nothing is left to report.
            }
        }

        if (SocketPath is not null)
        {
            try
            {
                File.Delete(SocketPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A leftover socket file is replaced by the next holder.
            }
        }

        _lock.Dispose();
    }
}
