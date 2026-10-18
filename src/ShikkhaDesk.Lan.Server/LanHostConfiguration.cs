using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShikkhaDesk.Lan.Server;

/// <summary>
/// Host settings kept in a small JSON file beside the database, so the service and the desktop
/// installer can both read and change them without touching the registry.
/// </summary>
public sealed class LanHostConfiguration
{
    public int Port { get; set; } = 8787;

    public string BindAddress { get; set; } = string.Empty;

    public bool AllowRemoteShutdown { get; set; }

    private static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string PathFor(string dataDirectory) => System.IO.Path.Combine(dataDirectory, "lan.json");

    public static LanHostConfiguration Load(string dataDirectory)
    {
        string path = PathFor(dataDirectory);

        if (!File.Exists(path))
        {
            var created = new LanHostConfiguration();
            created.Save(dataDirectory);
            return created;
        }

        try
        {
            return JsonSerializer.Deserialize<LanHostConfiguration>(File.ReadAllText(path), Options)
                   ?? new LanHostConfiguration();
        }
        catch (JsonException)
        {
            return new LanHostConfiguration();
        }
    }

    public void Save(string dataDirectory)
    {
        Directory.CreateDirectory(dataDirectory);
        File.WriteAllText(PathFor(dataDirectory), JsonSerializer.Serialize(this, Options));
    }

    public LanHostOptions ToOptions() => new()
    {
        Port = Port,
        BindAddress = BindAddress
    };
}
