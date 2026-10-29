namespace OpenNotes.UI.ViewModels;

/// <summary>A section of the card view ("Összes jegyzet"): one folder, or the notes without folder.</summary>
public sealed class NoteCardGroupViewModel
{
    public NoteCardGroupViewModel(string title, string color, bool isFolder, IReadOnlyList<NoteItemViewModel> notes)
    {
        Title = title;
        Color = color;
        IsFolder = isFolder;
        Notes = notes;
    }

    public string Title { get; }
    public string Color { get; }
    public bool IsFolder { get; }
    public IReadOnlyList<NoteItemViewModel> Notes { get; }
    public string CountText => Notes.Count.ToString();
}

/// <summary>Parameter of <c>MoveToFolderCommand</c>: which note goes into which folder.</summary>
public sealed record MoveRequest(NoteItemViewModel Note, FolderNavItemViewModel Target);
