using System.Text;

namespace PaperRoute.Desktop.Tests.Legibility;

// The measurement files (Measurements) are the same bytes on every system,
// so the files the Windows and Ubuntu runners write can be laid side by side
// (the harness design, part 2.4, "measure before judging").
// Limits of headless: HeadlessLimits.md
[TestClass]
public sealed class MeasurementsTests
{
    // Two rows written into a folder under the storage guard's root: tabs
    // between cells, numbers to two decimals in the invariant culture, a line
    // feed alone after every line (Environment.NewLine is a carriage return
    // and a line feed on Windows), and no byte order mark.
    [TestMethod]
    public void AMeasurementFileIsTheSameBytesOnEverySystem()
    {
        string folder = Path.Combine(AssemblySetup.StorageRoot, "measurements-" + Guid.NewGuid().ToString("N"));
        string path = Measurements.Write(folder, "R0", "Light", ["name", "ratio"], [["first", Measurements.Number(4.5)], ["second", Measurements.Number(21)]]);

        Assert.AreEqual(Path.Combine(folder, "legibility-r0-light.txt"), path, "The file is named by its rule and theme case.");
        Assert.AreEqual(
            "name\tratio\nfirst\t4.50\nsecond\t21.00\n",
            Encoding.UTF8.GetString(File.ReadAllBytes(path)),
            "The file's bytes are the same on every system.");
    }
}
