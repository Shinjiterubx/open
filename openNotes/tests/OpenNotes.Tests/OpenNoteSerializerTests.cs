using OpenNotes.Core.Models;
using OpenNotes.Infrastructure.Storage;
using Xunit;

namespace OpenNotes.Tests;

public class OpenNoteSerializerTests
{
    [Fact]
    public void Round_trip_keeps_content_metadata_and_assets()
    {
        var note = new Note { Title = "Bevásárlás", Content = "# Lista\n- [ ] tej\n![](assets/kep.png)", IsPinned = true };
        var path = note.AddAsset("kép.png", new byte[] { 1, 2, 3 });
        var serializer = new OpenNoteSerializer();

        using var stream = new MemoryStream();
        serializer.Write(note, stream);
        stream.Position = 0;
        var copy = serializer.Read(stream);

        Assert.Equal("assets/kep.png", path);
        Assert.Equal(note.Id, copy.Id);
        Assert.Equal(note.Title, copy.Title);
        Assert.Equal(note.Content, copy.Content);
        Assert.True(copy.IsPinned);
        Assert.Equal(new byte[] { 1, 2, 3 }, copy.Assets["kep.png"]);
    }

    [Fact]
    public void Identical_assets_are_stored_once()
    {
        var note = new Note();
        var first = note.AddAsset("a.png", new byte[] { 9 });
        var second = note.AddAsset("b.png", new byte[] { 9 });
        Assert.Equal(first, second);
        Assert.Single(note.Assets);
    }

    [Fact]
    public void Asset_names_cannot_escape_the_assets_folder()
    {
        Assert.Equal("passwd", AssetName.Sanitize("../../etc/passwd"));
    }
}
