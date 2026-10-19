using System.Text.Json;
using OpenKanban.Core.Abstractions;
using OpenKanban.Core.Boards;
using OpenKanban.Core.Models;
using OpenKanban.Infrastructure.Storage;

namespace OpenKanban.Infrastructure.Integration;

/// <summary>
/// Writes <c>deadlines.json</c>: every card with a due date, flattened. openCalendar watches this file
/// (its location is published in <c>HKCU\Software\openKanban\DeadlinesFile</c>) – no direct dependency between the apps.
/// </summary>
public sealed class DeadlineFeedWriter : IDeadlineFeed
{
    private readonly IAppPaths _paths;
    private readonly ISettingsStore _settings;

    public DeadlineFeedWriter(IAppPaths paths, ISettingsStore settings)
    {
        _paths = paths;
        _settings = settings;
    }

    public void Publish(IReadOnlyCollection<Workspace> workspaces)
    {
        // Integration switched off: publish an empty feed so openCalendar removes the cards.
        var source = _settings.Current.OpenCalendarIntegration ? workspaces : Array.Empty<Workspace>();
        var entries = source
            .SelectMany(w => w.Boards.SelectMany(b => b.Columns.SelectMany(c => c.Cards
                .Where(card => card.DueDate is not null)
                .Select(card => new DeadlineEntry(card.Id, card.DisplayTitle, card.DueDate!.Value, card.DueTime, card.IsDone,
                    w.Name, b.Name, c.Name, card.CoverColor ?? w.Color)))))
            .OrderBy(e => e.DueDate)
            .ToList();

        var document = new DeadlineFeedDocument("openKanban.deadlines", 1, DateTimeOffset.Now, entries);

        try
        {
            var temp = _paths.DeadlinesFile + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(document, OpenKanbanSerializer.JsonOptions));
            File.Move(temp, _paths.DeadlinesFile, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The feed is optional – the board itself is already saved.
        }
    }
}
