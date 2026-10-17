namespace FolderFunBg.Core;

/// <summary>
/// A per-folder override. Exactly one of <see cref="Path"/> or
/// <see cref="Clsid"/> identifies the folder.
/// </summary>
public sealed class FolderRule
{
    /// <summary>Normal filesystem path, e.g. <c>C:\Users\me\Downloads</c>.</summary>
    public string? Path { get; set; }

    /// <summary>Shell namespace CLSID, e.g. <c>::{20D04FE0-3AEA-1069-A2D8-08002B30309D}</c>.</summary>
    public string? Clsid { get; set; }

    /// <summary>Image file name inside the image library.</summary>
    public string? Image { get; set; }

    /// <summary>One of the values in <see cref="BackgroundPositions"/>.</summary>
    public string? Position { get; set; }

    /// <summary>0-255; null inherits the default.</summary>
    public int? Alpha { get; set; }

    /// <summary>Random image selection; null inherits the default.</summary>
    public bool? Random { get; set; }

    /// <summary>Optional per-folder readability override.</summary>
    public ReadabilityOverride? Readability { get; set; }

    /// <summary>Apply this rule to subfolders as well (path rules only).</summary>
    public bool Recursive { get; set; }

    /// <summary>Disabled rules are ignored.</summary>
    public bool Enabled { get; set; } = true;
}
