namespace FolderFunBg.Core;

/// <summary>
/// Style applied when a folder has no matching rule (or a rule leaves a field
/// unspecified).
/// </summary>
public sealed class BackgroundDefaults
{
    /// <summary>
    /// Image file name inside <see cref="FolderFunBgConfig.ImagesRoot"/>.
    /// Null means "no default background".
    /// </summary>
    public string? Image { get; set; }

    /// <summary>One of the values in <see cref="BackgroundPositions"/>.</summary>
    public string Position { get; set; } = BackgroundPositions.BottomRight;

    /// <summary>0-255.</summary>
    public int Alpha { get; set; } = 255;

    /// <summary>Pick a random image from the image library instead of <see cref="Image"/>.</summary>
    public bool Random { get; set; }
}
