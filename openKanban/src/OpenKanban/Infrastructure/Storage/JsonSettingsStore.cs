using System.Text.Json;
using System.Text.Json.Serialization;
using OpenKanban.Core.Abstractions;
using OpenKanban.Core.Models;

namespace OpenKanban.Infrastructure.Storage;

/// <summary>Human-editable <c>settings.json</c>. A corrupt file is backed up and replaced by defaults.</summary>
public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _filePath;

    public JsonSettingsStore(IAppPaths paths)
    {
        _filePath = paths.SettingsFile;
        Current = Load();
    }

    public AppSettings Current { get; }

    public event EventHandler? Saved;

    public void Save()
    {
        var temp = _filePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(Current, JsonOptions));
        File.Move(temp, _filePath, overwrite: true);
        Saved?.Invoke(this, EventArgs.Empty);
    }

    private AppSettings Load()
    {
        if (!File.Exists(_filePath)) return new AppSettings();

        try
        {
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_filePath), JsonOptions) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is JsonException or IOException or NotSupportedException)
        {
            File.Copy(_filePath, _filePath + ".corrupt", overwrite: true);
            return new AppSettings();
        }
    }
}
