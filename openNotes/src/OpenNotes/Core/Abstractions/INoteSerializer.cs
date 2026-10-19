using OpenNotes.Core.Models;

namespace OpenNotes.Core.Abstractions;

/// <summary>Converts a <see cref="Note"/> to and from the portable .openNote container.</summary>
public interface INoteSerializer
{
    void Write(Note note, Stream destination);

    Note Read(Stream source);
}
