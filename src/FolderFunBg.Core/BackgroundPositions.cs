namespace FolderFunBg.Core;

/// <summary>
/// Canonical position names shared with the native hook.
/// </summary>
public static class BackgroundPositions
{
    public const string TopLeft = "top-left";
    public const string TopRight = "top-right";
    public const string BottomLeft = "bottom-left";
    public const string BottomRight = "bottom-right";
    public const string Center = "center";
    public const string Stretch = "stretch";
    public const string ZoomFill = "zoom-fill";

    public static IReadOnlyList<string> All { get; } =
    [
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
        Center,
        Stretch,
        ZoomFill,
    ];

    public static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        All.Any(position => string.Equals(position, value, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Returns a canonical kebab-case value, or the default when the input is
    /// empty. Throws for an unknown non-empty value.
    /// </summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return BottomRight;
        }

        foreach (var position in All)
        {
            if (string.Equals(position, value, StringComparison.OrdinalIgnoreCase))
            {
                return position;
            }
        }

        throw new ArgumentException($"Unknown background position '{value}'.", nameof(value));
    }
}
