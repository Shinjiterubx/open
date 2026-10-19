using OpenCalendar.Core.Abstractions;
using OpenCalendar.Core.Models;

namespace OpenCalendar.Infrastructure.Storage;

/// <summary>
/// Stores each calendar as <c>{id}.opencalendar</c> (the same format used for export).
/// Writes are atomic; deleted calendars are moved to the Trash folder.
/// </summary>
public sealed class FileCalendarRepository : ICalendarRepository
{
    private readonly IAppPaths _paths;
    private readonly ICalendarSerializer _serializer;
    private readonly List<string> _loadErrors = new();

    public FileCalendarRepository(IAppPaths paths, ICalendarSerializer serializer)
    {
        _paths = paths;
        _serializer = serializer;
    }

    public IReadOnlyList<string> LoadErrors => _loadErrors;

    public IReadOnlyList<CalendarDocument> LoadAll()
    {
        _loadErrors.Clear();
        var calendars = new List<CalendarDocument>();

        foreach (var file in Directory.EnumerateFiles(_paths.CalendarsDirectory, "*" + OpenCalendarSerializer.FileExtension))
        {
            try
            {
                using var stream = File.OpenRead(file);
                var loaded = _serializer.Read(stream);
                if (loaded.Count == 0) continue;

                var calendar = loaded[0];
                if (Guid.TryParseExact(Path.GetFileNameWithoutExtension(file), "N", out var idFromName)) calendar.Id = idFromName;
                calendars.Add(calendar);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException)
            {
                _loadErrors.Add($"{Path.GetFileName(file)}: {ex.Message}");
            }
        }

        return calendars.OrderBy(c => c.CreatedAt).ToList();
    }

    public void Save(CalendarDocument calendar)
    {
        var target = PathFor(calendar.Id);
        var temp = target + ".tmp";
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            _serializer.Write(new[] { calendar }, stream);
        }
        File.Move(temp, target, overwrite: true);
    }

    public void Delete(Guid id)
    {
        var source = PathFor(id);
        if (!File.Exists(source)) return;
        var trashName = $"{id:N}_{DateTime.Now:yyyyMMdd-HHmmss}{OpenCalendarSerializer.FileExtension}";
        File.Move(source, Path.Combine(_paths.TrashDirectory, trashName), overwrite: true);
    }

    private string PathFor(Guid id) =>
        Path.Combine(_paths.CalendarsDirectory, id.ToString("N") + OpenCalendarSerializer.FileExtension);
}
