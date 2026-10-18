using System.IO;

namespace ShikkhaDesk.Infrastructure;

/// <summary>Where this installation keeps its data, backups and logs.</summary>
public sealed class ShikkhaDeskEnvironment
{
    public string DataDirectory { get; init; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ShikkhaDesk", "Data");

    public string BackupDirectory { get; init; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ShikkhaDesk", "Backups");

    public string LogDirectory { get; init; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ShikkhaDesk", "Logs");

    public string DatabasePath => Path.Combine(DataDirectory, "shikkhadesk.db");

    public static ShikkhaDeskEnvironment ForTests(string root) => new()
    {
        DataDirectory = Path.Combine(root, "data"),
        BackupDirectory = Path.Combine(root, "backups"),
        LogDirectory = Path.Combine(root, "logs")
    };
}
