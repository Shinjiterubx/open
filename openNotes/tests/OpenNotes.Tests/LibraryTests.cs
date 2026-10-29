using OpenNotes.Core.Abstractions;
using OpenNotes.Core.Library;
using OpenNotes.Core.Models;
using OpenNotes.Infrastructure.Storage;
using Xunit;

namespace OpenNotes.Tests;

public class LibraryTests
{
    private static readonly Guid Work = Guid.NewGuid();

    [Fact]
    public void Closed_notes_are_only_listed_in_the_closed_view()
    {
        var open = new Note { Title = "Nyitott" };
        var closed = new Note { Title = "Lezárt", IsDone = true };

        Assert.True(NoteFilter.IsVisible(open, showClosed: false, NoteScope.All, null));
        Assert.False(NoteFilter.IsVisible(closed, showClosed: false, NoteScope.All, null));
        Assert.True(NoteFilter.IsVisible(closed, showClosed: true, NoteScope.All, null));
        Assert.False(NoteFilter.IsVisible(open, showClosed: true, NoteScope.All, null));
    }

    [Fact]
    public void Scopes_select_folder_unfiled_or_all()
    {
        var inWork = new Note { FolderId = Work };
        var unfiled = new Note();

        Assert.True(NoteScope.All.Includes(inWork));
        Assert.True(NoteScope.All.Includes(unfiled));
        Assert.True(NoteScope.Of(Work).Includes(inWork));
        Assert.False(NoteScope.Of(Work).Includes(unfiled));
        Assert.True(NoteScope.Unfiled.Includes(unfiled));
        Assert.False(NoteScope.Unfiled.Includes(inWork));
    }

    [Fact]
    public void Folders_work_for_closed_notes_too()
    {
        var closedInWork = new Note { FolderId = Work, IsDone = true };
        Assert.True(NoteFilter.IsVisible(closedInWork, showClosed: true, NoteScope.Of(Work), null));
        Assert.False(NoteFilter.IsVisible(closedInWork, showClosed: false, NoteScope.Of(Work), null));
    }

    [Fact]
    public void Search_matches_title_and_content_case_insensitively()
    {
        var note = new Note { Title = "Költözés", Content = "Internet átíratása" };
        Assert.True(NoteFilter.IsVisible(note, false, NoteScope.All, "költ"));
        Assert.True(NoteFilter.IsVisible(note, false, NoteScope.All, "INTERNET"));
        Assert.False(NoteFilter.IsVisible(note, false, NoteScope.All, "bicikli"));
    }

    [Fact]
    public void Folder_survives_the_openNote_round_trip()
    {
        var note = new Note { Title = "Jegyzet", FolderId = Work, FolderName = "Munka", IsDone = true };
        var serializer = new OpenNoteSerializer();

        using var stream = new MemoryStream();
        serializer.Write(note, stream);
        stream.Position = 0;
        var copy = serializer.Read(stream);

        Assert.Equal(Work, copy.FolderId);
        Assert.Equal("Munka", copy.FolderName);
        Assert.True(copy.IsDone);
    }

    [Fact]
    public void Folder_list_is_saved_and_loaded()
    {
        var root = Path.Combine(Path.GetTempPath(), "openNotes-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var repository = new JsonFolderRepository(new TestPaths(root));
            Assert.Empty(repository.LoadAll());

            repository.SaveAll(new[] { new NoteFolder { Id = Work, Name = "Munka", Color = "#2F5A8A" } });
            var loaded = Assert.Single(repository.LoadAll());
            Assert.Equal(Work, loaded.Id);
            Assert.Equal("Munka", loaded.Name);
            Assert.Equal("#2F5A8A", loaded.Color);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class TestPaths : IAppPaths
    {
        public TestPaths(string root) => DataRoot = root;
        public string DataRoot { get; }
        public string NotesDirectory => Path.Combine(DataRoot, "Notes");
        public string TrashDirectory => Path.Combine(DataRoot, "Trash");
        public string SettingsFile => Path.Combine(DataRoot, "settings.json");
        public string FoldersFile => Path.Combine(DataRoot, "folders.json");
        public string LogDirectory => DataRoot;
        public string PreviewCacheDirectory => DataRoot;
        public string WebViewDataDirectory => DataRoot;
        public string WebAssetsDirectory => DataRoot;
    }
}
