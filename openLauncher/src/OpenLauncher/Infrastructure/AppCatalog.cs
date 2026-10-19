using OpenLauncher.Core.Abstractions;
using OpenLauncher.Core.Models;

namespace OpenLauncher.Infrastructure;

/// <summary>The apps of the "open" suite. AppIds must match the AppId in each app's Inno Setup script.</summary>
public sealed class AppCatalog : IAppCatalog
{
    public IReadOnlyList<AppDefinition> Apps { get; } = new[]
    {
        new AppDefinition(
            "openNotes",
            "openNotes",
            "Jegyzetek Markdownban",
            "Letisztult jegyzetelő mini ablakkal, globális gyorsbillentyűkkel, képekkel, teendőlistákkal, prioritással és határidő-emlékeztetőkkel.",
            "#5B3E96",
            "openNotes.exe",
            "{8E3C5B2A-6F1D-4C7E-9A42-0B7D5E1F3C21}",
            "openNotes-Setup.exe"),
        new AppDefinition(
            "openCalendar",
            "openCalendar",
            "Naptár és emlékeztetők",
            "Nap, hét, hónap és napló nézet, ismétlődő események, emlékeztetők – az openNotes és openKanban határidőivel együtt.",
            "#D45D79",
            "openCalendar.exe",
            "{4B1D7E92-3C5A-4F8B-B6E1-9D2C7A5F0E34}",
            "openCalendar-Setup.exe"),
        new AppDefinition(
            "openKanban",
            "openKanban",
            "Kanban táblák",
            "Workspace-ek, táblák és kártyák húzd-és-ejtsd kezeléssel, WIP limittel, címkékkel, határidőkkel és ellenőrzőlistákkal.",
            "#2F5A8A",
            "openKanban.exe",
            "{6A2C9D14-7E3B-4F51-A8C6-2B9E4D7F1A58}",
            "openKanban-Setup.exe"),
    };
}
