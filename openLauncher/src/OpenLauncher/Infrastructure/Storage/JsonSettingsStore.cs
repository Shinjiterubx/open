using System.Text.Json;
using OpenLauncher.Core.Abstractions;
using OpenLauncher.Core.Models;

namespace OpenLauncher.Infrastructure.Storage;

public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _file;

    public JsonSettingsStore(IAppPaths paths)
    {
        _file = paths.SettingsFile;
        Current = Load();
    }

    public AppSettings Current { get; }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        var temp = _file + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(Current, Options));
        File.Move(temp, _file, overwrite: true);
    }

    private AppSettings Load()
    {
        try
        {
            return File.Exists(_file)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_file), Options) ?? new AppSettings()
                : new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new AppSettings();   // a broken settings file must never block the launcher
        }
    }
}
