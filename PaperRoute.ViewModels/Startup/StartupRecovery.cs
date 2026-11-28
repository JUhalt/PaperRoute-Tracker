namespace PaperRoute.ViewModels.Startup;

// The failure window's recovery over the core's StartupRecoveryService
// (start-up design 2.8, build 6 F1): the one file that may name that service,
// a lifted ban listed in lifted-bans.txt. It adds nothing: each member is the
// service's own, which keeps a library from a newer PaperRoute out of reach
// and leaves the storage marker the way today's window does (#124).
//
// A plain StartupRecovery works on the library the core's default roots name,
// as today's window does (StartupFailureForm.vb:177): the person's own, or the
// session's. The tests give it a service over a library of their own in a
// temporary folder.
public sealed class StartupRecovery : IStartupRecovery
{
    // The lifted ban: StartupRecoveryService puts the previous save in place
    // of the person's library file, or restores a backup over the library,
    // only when the person asks in the failure window (#124, #421).
#pragma warning disable RS0030
    private readonly StartupRecoveryService _service;

    public StartupRecovery()
        : this(new StartupRecoveryService())
    {
    }

    internal StartupRecovery(StartupRecoveryService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
    }

    public bool PreviousSaveIsValid() => _service.PreviousSaveIsValid();

    public bool LibraryIsFromANewerPaperRoute() => _service.LibraryIsFromANewerPaperRoute();

    public string UsePreviousSave() => _service.UsePreviousSave() ?? string.Empty;

    public BackupInspection InspectBackup(string backupZipPath) => _service.InspectBackup(backupZipPath);

    public RestoreResult RestoreFromBackup(string backupZipPath) => _service.RestoreFromBackup(backupZipPath);
#pragma warning restore RS0030
}
