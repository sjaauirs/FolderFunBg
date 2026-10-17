namespace FolderFunBg.Core;

/// <summary>
/// Controls the scrim drawn over the background image to keep Explorer's
/// icons and filenames readable. The native hook applies this after the image
/// and before Explorer paints the items.
/// </summary>
public sealed class ReadabilitySettings
{
    public const string Auto = "auto";
    public const string None = "none";
    public const string Light = "light";
    public const string Dark = "dark";

    public static IReadOnlyList<string> AllModes { get; } = [Auto, None, Light, Dark];

    /// <summary>
    /// <c>auto</c> chooses a light or dark scrim from the image luminance and
    /// the Windows app theme; <c>light</c>/<c>dark</c> force a scrim color;
    /// <c>none</c> disables it.
    /// </summary>
    public string Mode { get; set; } = Auto;

    /// <summary>Scrim opacity, 0-255.</summary>
    public int Strength { get; set; } = 96;

    /// <summary>Left-to-right gradient (strongest over the file-name column).</summary>
    public bool Gradient { get; set; } = true;

    public static bool IsValidMode(string? mode) =>
        !string.IsNullOrWhiteSpace(mode) &&
        AllModes.Any(candidate => string.Equals(candidate, mode, StringComparison.OrdinalIgnoreCase));

    public static string NormalizeMode(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode))
        {
            return Auto;
        }

        foreach (var candidate in AllModes)
        {
            if (string.Equals(candidate, mode, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        throw new ArgumentException($"Unknown readability mode '{mode}'.", nameof(mode));
    }
}
