namespace FolderFunBg.Core;

/// <summary>
/// Thrown when a configuration document is structurally or semantically
/// invalid. The settings app treats this as a hard error rather than applying a
/// partially valid document.
/// </summary>
public sealed class ConfigValidationException : Exception
{
    public ConfigValidationException(IReadOnlyList<string> errors)
        : base("The FolderFunBg configuration is invalid: " + string.Join(" ", errors))
    {
        Errors = errors;
    }

    public IReadOnlyList<string> Errors { get; }
}
