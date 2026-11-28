namespace PaperRoute.ViewModels.Startup;

// Step 9 of start-up, "storage" (start-up design 2.4, build 6 F1): prepares
// the library's storage (validates the storage marker, moves a legacy data
// folder or managed library into place, migrates an older schema) before
// anything reads the library, as Program.vb:110-115 and 147-177 do today. The
// one caller of StorageMigrationService.EnsureCurrentStorage, a lifted ban
// listed in lifted-bans.txt.
//
// Today's loop: when the step fails, the failure (always the storage kind,
// Program.vb:162-166) is shown; when the person put the previous save or a
// backup in place, the step runs once more in this process, which still holds
// the library; a failure closed without recovering ends start-up. The work
// runs on a worker and is awaited, so the interface thread is never held by
// a long migration.
//
// Nothing outside the tests runs it yet: the app runs it once it opens real
// libraries (build 6 W1a). The tests run it over libraries of their own in a
// temporary folder, through the internal constructor.
public sealed class StorageStep
{
    private readonly Action _prepare;
    private readonly Func<string> _schemaFilePath;

    public StorageStep()
        : this(PrepareTheDefaultRoots, StorageMigrationService.SchemaFilePath)
    {
    }

    internal StorageStep(Action prepare, Func<string> schemaFilePath)
    {
        ArgumentNullException.ThrowIfNull(prepare);
        ArgumentNullException.ThrowIfNull(schemaFilePath);
        _prepare = prepare;
        _schemaFilePath = schemaFilePath;
    }

    // Runs the step until it passes, showing each failure. showFailure
    // answers true when the person put the previous save or a backup in
    // place, and false when they closed the failure without recovering.
    // The proof that storage is ready, or null when start-up ends here.
    public async Task<StorageReady?> RunAsync(Func<StartupFailure, Task<bool>> showFailure)
    {
        ArgumentNullException.ThrowIfNull(showFailure);
        while (true)
        {
            StartupFailure failure;
            try
            {
                await Task.Run(_prepare);
                return new StorageReady();
            }
            catch (Exception ex)
            {
                failure = StartupFailure.ForStorage(ex, _schemaFilePath());
            }

            if (!await showFailure(failure))
            {
                return null;
            }
        }
    }

    private static void PrepareTheDefaultRoots()
    {
        // The lifted ban: the storage step validates, moves into place and
        // migrates the library the default roots name, as today's start-up
        // does before anything reads it (Program.vb:110-115, #421).
#pragma warning disable RS0030
        StorageMigrationService.EnsureCurrentStorage();
#pragma warning restore RS0030
    }
}

// The proof that a storage step passed in this process (start-up design 2.6):
// only StorageStep makes one, and the person's own library will be opened only
// with one in hand (build 6 W1a).
public sealed class StorageReady
{
    internal StorageReady()
    {
    }
}
