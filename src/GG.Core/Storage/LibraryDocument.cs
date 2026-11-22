using GG.Core.Models;

namespace GG.Core.Storage;

/// <summary>Root object of library.json. The version number allows future migrations.</summary>
public sealed class LibraryDocument
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    public List<Game> Games { get; set; } = new();
}
