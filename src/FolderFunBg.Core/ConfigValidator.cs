namespace FolderFunBg.Core;

/// <summary>
/// Validates the shared configuration contract. The settings app refuses to
/// write a document that does not pass, and the native parser applies the same
/// limits defensively.
/// </summary>
public static class ConfigValidator
{
    private static readonly string[] SupportedExtensions = [".png", ".jpg", ".jpeg"];

    public static IReadOnlyList<string> Validate(FolderFunBgConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var errors = new List<string>();

        if (config.Version < 1)
        {
            errors.Add("version must be at least 1.");
        }

        if (string.IsNullOrWhiteSpace(config.ImagesRoot))
        {
            errors.Add("imagesRoot is required.");
        }
        else
        {
            try
            {
                var expanded = Environment.ExpandEnvironmentVariables(config.ImagesRoot);
                if (!Path.IsPathRooted(expanded))
                {
                    errors.Add("imagesRoot must be an absolute path.");
                }
                else
                {
                    _ = Path.GetFullPath(expanded);
                }
            }
            catch (Exception exception) when (
                exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                errors.Add($"imagesRoot is invalid: {exception.Message}");
            }
        }

        ValidateDefaults(config.Defaults, errors);

        if (config.Readability is null)
        {
            errors.Add("readability is required.");
        }
        else
        {
            ValidateReadability(config.Readability, "readability", errors);
        }

        if (config.Folders is null)
        {
            errors.Add("folders must be an array.");
            return errors;
        }

        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < config.Folders.Count; index++)
        {
            var rule = config.Folders[index];
            if (rule is null)
            {
                errors.Add($"folders[{index}] is null.");
                continue;
            }

            ValidateRule(rule, index, keys, errors);
        }

        return errors;
    }

    public static void ValidateOrThrow(FolderFunBgConfig config)
    {
        var errors = Validate(config);
        if (errors.Count > 0)
        {
            throw new ConfigValidationException(errors);
        }
    }

    public static bool IsSafeImageFileName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) ||
            name == "." ||
            name == ".." ||
            name != Path.GetFileName(name) ||
            name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return false;
        }

        var extension = Path.GetExtension(name);
        return SupportedExtensions.Any(supported =>
            string.Equals(extension, supported, StringComparison.OrdinalIgnoreCase));
    }

    private static void ValidateDefaults(BackgroundDefaults? defaults, List<string> errors)
    {
        if (defaults is null)
        {
            errors.Add("defaults is required.");
            return;
        }

        if (!BackgroundPositions.IsValid(defaults.Position))
        {
            errors.Add($"defaults.position '{defaults.Position}' is not a supported position.");
        }

        if (defaults.Alpha is < 0 or > 255)
        {
            errors.Add("defaults.alpha must be between 0 and 255.");
        }

        if (defaults.Image is not null && !IsSafeImageFileName(defaults.Image))
        {
            errors.Add($"defaults.image '{defaults.Image}' is not a supported image file name.");
        }
    }

    private static void ValidateReadability(
        ReadabilitySettings readability,
        string path,
        List<string> errors)
    {
        if (!ReadabilitySettings.IsValidMode(readability.Mode))
        {
            errors.Add($"{path}.mode '{readability.Mode}' is not a supported readability mode.");
        }

        if (readability.Strength is < 0 or > 255)
        {
            errors.Add($"{path}.strength must be between 0 and 255.");
        }
    }

    private static void ValidateReadabilityOverride(
        ReadabilityOverride readability,
        string path,
        List<string> errors)
    {
        if (readability.Mode is not null && !ReadabilitySettings.IsValidMode(readability.Mode))
        {
            errors.Add($"{path}.mode '{readability.Mode}' is not a supported readability mode.");
        }

        if (readability.Strength is < 0 or > 255)
        {
            errors.Add($"{path}.strength must be between 0 and 255.");
        }
    }

    private static void ValidateRule(
        FolderRule rule,
        int index,
        HashSet<string> keys,
        List<string> errors)
    {
        var hasPath = !string.IsNullOrWhiteSpace(rule.Path);
        var hasClsid = !string.IsNullOrWhiteSpace(rule.Clsid);

        if (hasPath == hasClsid)
        {
            errors.Add($"folders[{index}] must set exactly one of path or clsid.");
        }

        string? key = null;

        if (hasPath)
        {
            var expanded = Environment.ExpandEnvironmentVariables(rule.Path!.Trim());
            var path = Path.IsPathRooted(expanded)
                ? PathRuleResolver.TryNormalizePath(rule.Path)
                : null;

            if (path is null)
            {
                errors.Add($"folders[{index}].path is not a valid absolute path.");
            }
            else
            {
                key = "P:" + path;
            }
        }

        if (hasClsid)
        {
            var clsid = PathRuleResolver.TryNormalizeClsid(rule.Clsid);
            if (clsid is null)
            {
                errors.Add($"folders[{index}].clsid is not a valid GUID.");
            }
            else
            {
                key = "C:" + clsid;
                if (rule.Recursive)
                {
                    errors.Add($"folders[{index}] cannot be recursive when it uses a clsid.");
                }
            }
        }

        if (rule.Image is not null && !IsSafeImageFileName(rule.Image))
        {
            errors.Add($"folders[{index}].image '{rule.Image}' is not a supported image file name.");
        }

        if (rule.Position is not null && !BackgroundPositions.IsValid(rule.Position))
        {
            errors.Add($"folders[{index}].position '{rule.Position}' is not a supported position.");
        }

        if (rule.Alpha is < 0 or > 255)
        {
            errors.Add($"folders[{index}].alpha must be between 0 and 255.");
        }

        if (rule.Readability is not null)
        {
            ValidateReadabilityOverride(rule.Readability, $"folders[{index}].readability", errors);
        }

        if (rule.Enabled && key is not null && !keys.Add(key))
        {
            errors.Add($"folders[{index}] duplicates another enabled rule for '{key[2..]}'.");
        }
    }
}
