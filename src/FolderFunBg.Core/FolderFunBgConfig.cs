namespace FolderFunBg.Core;

/// <summary>
/// Root configuration document shared by the settings app and the native hook.
/// </summary>
public sealed class FolderFunBgConfig
{
    public int Version { get; set; } = 1;

    /// <summary>
    /// Absolute path to the managed image library. Environment variables are
    /// expanded by the resolver; the default lives under %LOCALAPPDATA%.
    /// </summary>
    public string ImagesRoot { get; set; } = "%LOCALAPPDATA%\\FolderFunBg\\Images";

    public BackgroundDefaults Defaults { get; set; } = new();

    /// <summary>Global readability scrim settings; rules may override them.</summary>
    public ReadabilitySettings Readability { get; set; } = new();

    public List<FolderRule> Folders { get; set; } = [];
}
