using ManuscriptPipeline.Services;
using PaperRoute.Desktop.Tests.Fixtures.Recovery;
using PaperRoute.ViewModels.Startup;

namespace PaperRoute.Desktop.Tests.Startup;

// The storage half of acceptance line 2 of #421, "recovery fixtures pass"
// (start-up design 5.3 part a, build 6 F1): each storage-step fixture runs
// through StorageStep, in-process, and its failure through the failure
// window's model with the real StartupRecovery over the fixture's own
// library, with the fixture's file hashes listed before and after.
[TestClass]
public sealed class StorageStepFixtureTests
{
    private static readonly TimeSpan s_limit = TimeSpan.FromSeconds(60);

    // fails: the step shows a failure (always the storage kind before any
    // window, Program.vb:162-166); the offers are the model's after its checks.
    [TestMethod]
    [DataRow(StorageFixture.NewerSchema, true, false, false, DisplayName = StorageFixture.NewerSchema)]
    [DataRow(StorageFixture.LegacyFolder, false, false, false, DisplayName = StorageFixture.LegacyFolder)]
    [DataRow(StorageFixture.OldSchemaDamagedWithGoodPreviousSave, true, true, true, DisplayName = StorageFixture.OldSchemaDamagedWithGoodPreviousSave)]
    public void EachStorageStepFixtureShowsItsKindAndOffers(string name, bool fails, bool offersPreviousSave, bool offersRestore)
    {
        using StorageFixture fixture = StorageFixture.Make(name);
        SortedDictionary<string, string> before = fails ? fixture.Hashes() : new(StringComparer.Ordinal);
        var shown = new List<StartupFailureViewModel>();

        StorageReady? ready = Run(fixture.Step().RunAsync(failure =>
        {
            shown.Add(new StartupFailureViewModel(failure, fixture.Recovery, new RecordingStartupQuestions()));
            return Task.FromResult(false);
        }));

        if (!fails)
        {
            Assert.IsNotNull(ready, $"{name}: the storage step passes.");
            Assert.IsEmpty(shown, $"{name}: no failure is shown.");
            return;
        }

        Assert.IsNull(ready, $"{name}: a failure closed without recovering ends start-up.");
        StartupFailureViewModel model = shown.Single();
        Assert.AreEqual(StartupFailureKind.Storage, model.Failure.Kind, $"{name}: before any window, every failure is the storage kind.");
        Assert.AreEqual(fixture.SchemaFilePath, model.Failure.FilePath, $"{name}: the storage failure names the storage marker.");
        Assert.AreEqual(offersPreviousSave, model.OffersPreviousSave, $"{name}: Use the Previous Save is offered exactly where it reads and would mend the library.");
        Assert.AreEqual(offersRestore, model.OffersRestore, $"{name}: Restore From Backup... is offered unless a newer PaperRoute wrote the library.");
        StorageFixture.AssertSame(before, fixture.Hashes(), $"{name}: the step and a Close change nothing on disk.");
    }

    // The storage marker says a newer PaperRoute wrote the library: not
    // damage, so nothing is offered, and asked anyway the model asks nothing
    // and changes nothing.
    [TestMethod]
    public void NoRecoveryForALibraryFromANewerPaperRoute()
    {
        using StorageFixture fixture = StorageFixture.Make(StorageFixture.NewerSchema);
        SortedDictionary<string, string> before = fixture.Hashes();
        var questions = new RecordingStartupQuestions();
        StartupFailureViewModel? model = null;

        Run(fixture.Step().RunAsync(async failure =>
        {
            model = new StartupFailureViewModel(failure, fixture.Recovery, questions);
            model.UsePreviousSave();
            await model.RestoreFromBackupAsync();
            return model.Recovered;
        }));

        Assert.IsNotNull(model, "The newer schema stops the storage step.");
        StringAssert.Contains(model.Details, "newer storage schema (10)", "The details say why.");
        Assert.IsFalse(model.OffersPreviousSave, "No previous save is put over a library from a newer PaperRoute.");
        Assert.IsFalse(model.OffersRestore, "No backup is put over a library from a newer PaperRoute.");
        Assert.IsFalse(model.Recovered, "Nothing was recovered.");
        Assert.IsEmpty(questions.Log.Names, "Nothing was asked.");
        StorageFixture.AssertSame(before, fixture.Hashes(), "Nothing changed on disk.");
    }

    // The old-schema fixture: the storage step fails on the damaged library
    // file, Use the Previous Save puts manuscripts.bak in its place, the step
    // runs once more and migrates it to the current schema, and the library
    // opens as the primary.
    [TestMethod]
    public void UsePreviousSaveContinuesStartupWithALibraryThatOpens()
    {
        using StorageFixture fixture = StorageFixture.Make(StorageFixture.OldSchemaDamagedWithGoodPreviousSave);
        int attempts = 0;
        var shown = new List<StartupFailureViewModel>();

        StorageReady? ready = Run(fixture.Step(() => attempts++).RunAsync(failure =>
        {
            var model = new StartupFailureViewModel(failure, fixture.Recovery, new RecordingStartupQuestions());
            shown.Add(model);
            model.UsePreviousSave();
            return Task.FromResult(model.Recovered);
        }));

        Assert.IsNotNull(ready, "Start-up goes on with storage ready.");
        Assert.AreEqual(2, attempts, "The storage step ran once more after the recovery.");
        Assert.IsTrue(shown.Single().Recovered, "The previous save was put in place.");
        Assert.AreEqual(StorageMigrationService.CurrentSchemaVersion, StorageMigrationService.ReadSchemaVersion(fixture.SchemaFilePath),
            "The second run migrated the recovered library to the current schema.");

        ManuscriptRepository repository = fixture.Repository();
        CollectionAssert.AreEqual(fixture.Titles.ToArray(), repository.Load().Select(manuscript => manuscript.Title).ToArray(), "The library opens with the previous save's manuscripts.");
        Assert.IsFalse(repository.LastLoadRecoveredFromBackup, "It opens as the primary, with no fallback needed.");
    }

    [TestMethod]
    public void UsePreviousSaveSaysWhereTheReplacedFileWent()
    {
        using StorageFixture fixture = StorageFixture.Make(StorageFixture.OldSchemaDamagedWithGoodPreviousSave);
        StartupFailureViewModel? model = null;

        Run(fixture.Step().RunAsync(failure =>
        {
            model = new StartupFailureViewModel(failure, fixture.Recovery, new RecordingStartupQuestions());
            model.UsePreviousSave();
            return Task.FromResult(false);
        }));

        Assert.IsNotNull(model, "The damaged library stops the storage step.");
        Assert.IsTrue(model.Recovered, "The previous save was put in place.");
        string kept = Directory.GetFiles(Path.Combine(fixture.DataFolder, "recovery")).Single();
        CollectionAssert.AreEqual(fixture.DamagedLibrary, File.ReadAllBytes(kept), "The replaced library file is kept, byte for byte.");
        Assert.AreEqual(StartupFailureViewModel.PreviousSaveInPlace + StartupFailureViewModel.KeptTheReplacedFileAt + kept + ".", model.Status,
            "The status line says, in today's words, where the replaced file is kept.");
    }

    // The previous save was offered, but it no longer reads when taken: the
    // model says why in the status line, is not recovered, and nothing on
    // disk changes.
    [TestMethod]
    public void AFailedUsePreviousSaveChangesNothingAndSaysWhy()
    {
        using StorageFixture fixture = StorageFixture.Make(StorageFixture.OldSchemaDamagedWithGoodPreviousSave);
        StartupFailureViewModel? model = null;
        SortedDictionary<string, string> before = new(StringComparer.Ordinal);

        Run(fixture.Step().RunAsync(failure =>
        {
            model = new StartupFailureViewModel(failure, fixture.Recovery, new RecordingStartupQuestions());
            Assert.IsTrue(model.OffersPreviousSave, "The previous save reads when the window opens.");
            File.WriteAllText(fixture.PreviousSaveFile, "{ damaged too");
            before = fixture.Hashes();
            model.UsePreviousSave();
            return Task.FromResult(model.Recovered);
        }));

        Assert.IsNotNull(model);
        Assert.IsFalse(model.Recovered, "Nothing was recovered.");
        StringAssert.Contains(model.Status, "Nothing has been changed", "The status line says why, in the core's words.");
        StorageFixture.AssertSame(before, fixture.Hashes(), "Nothing changed on disk.");
    }

    // The legacy fixture with the hold taken: its lock file lies beside the
    // current root, so the migration still finds the current root empty and
    // moves the legacy library in, leaving the legacy folder and the lock file
    // as they were.
    [TestMethod]
    public void TheLegacyMigrationRunsWithTheHoldTaken()
    {
        using StorageFixture fixture = StorageFixture.Make(StorageFixture.LegacyFolder);
        SortedDictionary<string, string> legacyBefore = fixture.Hashes(fixture.LegacyDataRoot);
        Assert.IsNotNull(fixture.Hold, "The hold's stand-in is open.");

        StorageReady? ready = Run(fixture.Step().RunAsync(_ => Task.FromResult(false)));

        Assert.IsNotNull(ready, "The storage step passes with the hold taken.");
        Assert.IsTrue(File.Exists(fixture.LibraryFile), "The legacy library was moved into the current root, not skipped because of the lock file.");
        CollectionAssert.AreEqual(fixture.Titles.ToArray(), fixture.Repository().Load().Select(manuscript => manuscript.Title).ToArray(),
            "The current root holds the legacy library.");
        StorageFixture.AssertSame(legacyBefore, fixture.Hashes(fixture.LegacyDataRoot), "The legacy folder is left as it was.");
        Assert.IsTrue(File.Exists(fixture.HoldFile) && new FileInfo(fixture.HoldFile).Length == 0, "The lock file beside the root is untouched.");
        Assert.IsTrue(fixture.Hold.CanWrite, "The hold is still held.");
        Assert.IsFalse(Directory.GetFiles(fixture.CurrentDataRoot, "*.instance.lock", SearchOption.AllDirectories).Any(), "No lock file lies inside the data root.");
    }

    // The test assembly's storage guard (AssemblySetup): running every
    // fixture, recovery included, resolves no default root, and leaves the
    // guard's own folder, where a default root would now land, as it was.
    [TestMethod]
    public void EveryFixtureLeavesNoDefaultRootUse()
    {
        using StorageFixture newer = StorageFixture.Make(StorageFixture.NewerSchema);
        using StorageFixture legacy = StorageFixture.Make(StorageFixture.LegacyFolder);
        using StorageFixture old = StorageFixture.Make(StorageFixture.OldSchemaDamagedWithGoodPreviousSave);
        SortedDictionary<string, string> guardBefore = StorageFixture.HashesOf(AssemblySetup.StorageRoot);
        int usesBefore = StorageEnvironment.DefaultRootUses;

        foreach (StorageFixture fixture in new[] { newer, legacy, old })
        {
            Run(fixture.Step().RunAsync(failure =>
            {
                var model = new StartupFailureViewModel(failure, fixture.Recovery, new RecordingStartupQuestions());
                model.UsePreviousSave();
                _ = model.CopyText();
                return Task.FromResult(model.Recovered);
            }));
        }

        Assert.AreEqual(0, usesBefore, "No default root answered before the fixtures ran.");
        Assert.AreEqual(0, StorageEnvironment.DefaultRootUses, "No default root answered while the fixtures ran.");
        StorageFixture.AssertSame(guardBefore, StorageFixture.HashesOf(AssemblySetup.StorageRoot),
            "The fixtures wrote nowhere the core's default roots lead.");
    }

    private static T Run<T>(Task<T> work) => work.WaitAsync(s_limit).GetAwaiter().GetResult();
}
