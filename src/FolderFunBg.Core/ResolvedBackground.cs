namespace FolderFunBg.Core;

/// <summary>
/// The effective background for one folder after defaults and rule overrides
/// have been merged.
/// </summary>
public sealed record ResolvedBackground(
    string? Image,
    string Position,
    int Alpha,
    bool Random,
    ReadabilitySettings Readability,
    FolderRule? Rule);
