namespace FolderFunBg.Core;

/// <summary>
/// Well-known per-user locations. The native hook resolves the same paths via
/// SHGetKnownFolderPath(FOLDERID_LocalAppData).
/// </summary>
public static class FolderFunBgPaths
{
    public const string AppFolderName = "FolderFunBg";

    public static string LocalAppDataRoot =>
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    public static string DefaultRoot =>
        Path.Combine(LocalAppDataRoot, AppFolderName);

    public static string DefaultConfigPath =>
        Path.Combine(DefaultRoot, "config.json");

    public static string DefaultImagesPath =>
        Path.Combine(DefaultRoot, "Images");

    public static string DefaultLogsPath =>
        Path.Combine(DefaultRoot, "Logs");

    /// <summary>
    /// Expands environment variables and normalizes to a full path. Relative
    /// paths are resolved against the current directory.
    /// </summary>
    public static string ExpandPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Path.GetFullPath(Environment.ExpandEnvironmentVariables(path));
    }
}
