namespace FolderFunBg.Core;

/// <summary>
/// Partial per-folder readability override. Null fields inherit the global
/// <see cref="FolderFunBgConfig.Readability"/> settings.
/// </summary>
public sealed class ReadabilityOverride
{
    public string? Mode { get; set; }

    public int? Strength { get; set; }

    public bool? Gradient { get; set; }
}
