using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenCalendar.Core.Abstractions;
using OpenCalendar.Core.Models;

namespace OpenCalendar.Infrastructure.Storage;

/// <summary>
/// The <c>.opencalendar</c> format is a ZIP archive (see docs/FORMAT.md):
/// <code>
/// manifest.json                 – format, version, generator, list of calendars
/// calendars/{id}.json           – one calendar with its events
/// </code>
/// </summary>
public sealed class OpenCalendarSerializer : ICalendarSerializer
{
    public const string FileExtension = ".opencalendar";
    public const string FormatName = "openCalendar";
    public const int CurrentVersion = 1;

    private const string ManifestEntry = "manifest.json";
    private const string CalendarFolder = "calendars/";
    private const long MaxEntrySize = 100L * 1024 * 1024;

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public void Write(IReadOnlyCollection<CalendarDocument> calendars, Stream destination)
    {
        using var archive = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true, Utf8NoBom);

        var manifest = new Manifest(FormatName, CurrentVersion, $"openCalendar {AppInfo.Version}", DateTimeOffset.Now,
            calendars.Select(c => new ManifestCalendar(c.Id, c.Name, c.Events.Count)).ToList());
        WriteJson(archive, ManifestEntry, manifest);

        foreach (var calendar in calendars)
            WriteJson(archive, $"{CalendarFolder}{calendar.Id:N}.json", calendar);
    }

    public IReadOnlyList<CalendarDocument> Read(Stream source)
    {
        using var archive = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true, Utf8NoBom);

        var manifest = ReadJson<Manifest>(archive.GetEntry(ManifestEntry)
            ?? throw new InvalidDataException("A fájl nem érvényes openCalendar naptár (hiányzik a manifest.json)."));

        if (!string.Equals(manifest.Format, FormatName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Ismeretlen fájlformátum.");
        if (manifest.Version > CurrentVersion)
            throw new InvalidDataException($"A fájl egy újabb openCalendar verzióval készült (formátum v{manifest.Version}).");

        var calendars = archive.Entries
            .Where(e => e.FullName.StartsWith(CalendarFolder, StringComparison.OrdinalIgnoreCase)
                        && e.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .Select(ReadJson<CalendarDocument>)
            .ToList();

        foreach (var calendar in calendars) Normalize(calendar);
        return calendars;
    }

    /// <summary>Guards against incomplete data from other tools / hand edits.</summary>
    private static void Normalize(CalendarDocument calendar)
    {
        if (calendar.Id == Guid.Empty) calendar.Id = Guid.NewGuid();
        calendar.Events ??= new List<CalendarEvent>();
        foreach (var ev in calendar.Events)
        {
            if (ev.Id == Guid.Empty) ev.Id = Guid.NewGuid();
            ev.ExceptionDates ??= new List<DateOnly>();
            ev.ReminderMinutes ??= new List<int>();
            if (ev.End < ev.Start) ev.End = ev.Start;
            if (ev.Recurrence is { } rule)
            {
                rule.DaysOfWeek ??= new List<DayOfWeek>();
                rule.Interval = Math.Max(1, rule.Interval);
            }
        }
    }

    private static void WriteJson<T>(ZipArchive archive, string name, T value)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var stream = entry.Open();
        JsonSerializer.Serialize(stream, value, JsonOptions);
    }

    private static T ReadJson<T>(ZipArchiveEntry entry)
    {
        if (entry.Length > MaxEntrySize) throw new InvalidDataException($"Túl nagy bejegyzés: {entry.Name}");
        using var stream = entry.Open();
        return JsonSerializer.Deserialize<T>(stream, JsonOptions) ?? throw new InvalidDataException($"Üres bejegyzés: {entry.Name}");
    }

    private sealed record Manifest(string Format, int Version, string? Generator, DateTimeOffset ExportedAt, List<ManifestCalendar>? Calendars);

    private sealed record ManifestCalendar(Guid Id, string Name, int EventCount);
}
