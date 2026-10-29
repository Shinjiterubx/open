using CommunityToolkit.Mvvm.ComponentModel;
using OpenNotes.Core.Library;
using OpenNotes.Core.Models;

namespace OpenNotes.UI.ViewModels;

/// <summary>
/// An entry of the folder navigation: "Összes jegyzet", "Mappa nélkül" or a real folder.
/// The same objects are offered as targets of "Áthelyezés mappába" (except "Összes").
/// </summary>
public sealed partial class FolderNavItemViewModel : ObservableObject
{
    private FolderNavItemViewModel(NoteScopeKind kind, NoteFolder? folder, string name, string color, string glyph)
    {
        Kind = kind;
        Folder = folder;
        _name = name;
        _color = color;
        Glyph = glyph;
    }

    public static FolderNavItemViewModel CreateAll() => new(NoteScopeKind.All, null, "Összes jegyzet", "#6B6B76", "");

    public static FolderNavItemViewModel CreateUnfiled() => new(NoteScopeKind.Unfiled, null, "Mappa nélkül", "#9A97A6", "");

    public static FolderNavItemViewModel ForFolder(NoteFolder folder) => new(NoteScopeKind.Folder, folder, folder.Name, folder.Color, "");

    public NoteScopeKind Kind { get; }

    /// <summary>The folder model; <c>null</c> for "Összes" and "Mappa nélkül".</summary>
    public NoteFolder? Folder { get; }

    /// <summary>Folder id written into notes moved here (<c>null</c> = no folder).</summary>
    public Guid? FolderId => Folder?.Id;

    public NoteScope Scope => Kind switch
    {
        NoteScopeKind.All => NoteScope.All,
        NoteScopeKind.Unfiled => NoteScope.Unfiled,
        _ => NoteScope.Of(Folder!.Id),
    };

    public bool IsFolder => Kind == NoteScopeKind.Folder;

    public string Glyph { get; }

    [ObservableProperty] private string _name;

    [ObservableProperty] private string _color;

    /// <summary>Number of notes in this entry for the current mode (open or closed notes).</summary>
    [ObservableProperty] private int _count;

    /// <summary>The note open in the editor is in this folder – highlighted in the sidebar.</summary>
    [ObservableProperty] private bool _containsOpenNote;

    /// <summary>Applies a rename / recolour to the model and the view.</summary>
    public void Update(string name, string color)
    {
        if (Folder is null) return;
        Folder.Name = name;
        Folder.Color = color;
        Name = name;
        Color = color;
    }
}
