using OpenLauncher.Core.Versioning;
using OpenLauncher.Infrastructure;
using OpenLauncher.Infrastructure.Installation;
using Xunit;

namespace OpenLauncher.Tests;

public class VersionTextTests
{
    [Theory]
    [InlineData("1.1.0", "1.0.0", true)]
    [InlineData("1.0.0", "1.0", false)]
    [InlineData("1.0.0.0", "1.0.0", false)]
    [InlineData("2.0.0+abc", "1.9.9", true)]
    [InlineData("1.0.0", "1.1.0", false)]
    [InlineData(null, "1.0.0", false)]
    [InlineData("1.0.0", null, false)]
    [InlineData("garbage", "1.0.0", false)]
    public void IsNewer(string? candidate, string? current, bool expected) =>
        Assert.Equal(expected, VersionText.IsNewer(candidate, current));

    [Theory]
    [InlineData("1.0.0.0", "1.0.0")]
    [InlineData("1.2", "1.2.0")]
    [InlineData("1.2.3.4", "1.2.3.4")]
    public void Display(string input, string expected) => Assert.Equal(expected, VersionText.Display(input));
}

public class UninstallStringTests
{
    [Theory]
    [InlineData("\"C:\\Users\\a\\AppData\\Local\\Programs\\openNotes\\unins000.exe\"", "C:\\Users\\a\\AppData\\Local\\Programs\\openNotes\\unins000.exe")]
    [InlineData("C:\\Apps\\openKanban\\unins000.exe /SILENT", "C:\\Apps\\openKanban\\unins000.exe")]
    [InlineData("  ", null)]
    [InlineData(null, null)]
    public void ExtractPath(string? command, string? expected) =>
        Assert.Equal(expected, InnoSetupInstallationService.ExtractPath(command));
}

public class AppCatalogTests
{
    private readonly AppCatalog _catalog = new();

    [Fact]
    public void Contains_the_three_apps_with_unique_ids()
    {
        Assert.Equal(new[] { "openNotes", "openCalendar", "openKanban" }, _catalog.Apps.Select(a => a.Id));
        Assert.Equal(_catalog.Apps.Count, _catalog.Apps.Select(a => a.InnoAppId).Distinct().Count());
    }

    [Fact]
    public void AppIds_are_braced_guids_matching_the_installers()
    {
        foreach (var app in _catalog.Apps)
        {
            Assert.StartsWith("{", app.InnoAppId);
            Assert.True(Guid.TryParse(app.InnoAppId, out _), app.InnoAppId);
            Assert.Equal($"{app.Id}-Setup.exe", app.InstallerFileName);
            Assert.Equal($"{app.Id}.exe", app.ExeName);
        }
    }
}
