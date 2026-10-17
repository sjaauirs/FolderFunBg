namespace FolderFunBg.Core;

/// <summary>
/// Resolves a folder path (or shell CLSID) to the effective background.
/// Matching is case-insensitive. Exact rules win; otherwise the longest
/// matching recursive path rule wins.
/// </summary>
public static class PathRuleResolver
{
    public static ResolvedBackground Resolve(FolderFunBgConfig config, string? location)
    {
        ArgumentNullException.ThrowIfNull(config);

        var defaults = ResolveDefaults(config.Defaults, config.Readability);
        if (string.IsNullOrWhiteSpace(location))
        {
            return defaults;
        }

        var rule = FindRule(config, location);
        return rule is null ? defaults : Merge(defaults, rule);
    }

    public static ResolvedBackground ResolveDefaults(
        BackgroundDefaults? defaults,
        ReadabilitySettings? readability)
    {
        if (defaults is null)
        {
            return new ResolvedBackground(
                null,
                BackgroundPositions.BottomRight,
                255,
                false,
                NormalizeReadability(readability),
                null);
        }

        var position = BackgroundPositions.IsValid(defaults.Position)
            ? BackgroundPositions.Normalize(defaults.Position)
            : BackgroundPositions.BottomRight;

        return new ResolvedBackground(
            defaults.Image,
            position,
            Math.Clamp(defaults.Alpha, 0, 255),
            defaults.Random,
            NormalizeReadability(readability),
            null);
    }

    public static FolderRule? FindRule(FolderFunBgConfig config, string location)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentException.ThrowIfNullOrWhiteSpace(location);

        if (TryNormalizeClsid(location) is { } clsid)
        {
            return config.Folders.FirstOrDefault(rule =>
                rule.Enabled &&
                !string.IsNullOrWhiteSpace(rule.Clsid) &&
                string.Equals(
                    TryNormalizeClsid(rule.Clsid),
                    clsid,
                    StringComparison.OrdinalIgnoreCase));
        }

        var path = TryNormalizePath(location);
        if (path is null)
        {
            return null;
        }

        FolderRule? exact = null;
        FolderRule? recursive = null;
        var recursiveLength = -1;

        foreach (var rule in config.Folders)
        {
            if (!rule.Enabled || string.IsNullOrWhiteSpace(rule.Path))
            {
                continue;
            }

            var rulePath = TryNormalizePath(rule.Path);
            if (rulePath is null)
            {
                continue;
            }

            if (string.Equals(rulePath, path, StringComparison.OrdinalIgnoreCase))
            {
                exact ??= rule;
                continue;
            }

            if (rule.Recursive &&
                IsUnder(path, rulePath) &&
                rulePath.Length > recursiveLength)
            {
                recursive = rule;
                recursiveLength = rulePath.Length;
            }
        }

        return exact ?? recursive;
    }

    public static string? TryNormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            var expanded = Environment.ExpandEnvironmentVariables(path.Trim());
            var full = Path.GetFullPath(expanded);
            if (full.Length > 3)
            {
                full = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }

            return full;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
        catch (PathTooLongException)
        {
            return null;
        }
    }

    /// <summary>
    /// Accepts <c>::{GUID}</c> or <c>{GUID}</c> and returns the canonical
    /// <c>::{GUID}</c> form, or null when the value is not a GUID.
    /// </summary>
    public static string? TryNormalizeClsid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = value.Trim();
        if (text.StartsWith("::", StringComparison.Ordinal))
        {
            text = text[2..];
        }

        return Guid.TryParse(text, out var guid)
            ? "::{" + guid.ToString("D").ToUpperInvariant() + "}"
            : null;
    }

    private static ResolvedBackground Merge(ResolvedBackground defaults, FolderRule rule)
    {
        var position = BackgroundPositions.IsValid(rule.Position)
            ? BackgroundPositions.Normalize(rule.Position)
            : defaults.Position;

        return new ResolvedBackground(
            rule.Image ?? defaults.Image,
            position,
            Math.Clamp(rule.Alpha ?? defaults.Alpha, 0, 255),
            rule.Random ?? defaults.Random,
            MergeReadability(defaults.Readability, rule.Readability),
            rule);
    }

    private static ReadabilitySettings MergeReadability(
        ReadabilitySettings baseSettings,
        ReadabilityOverride? overrides)
    {
        var normalizedBase = NormalizeReadability(baseSettings);
        if (overrides is null)
        {
            return normalizedBase;
        }

        return new ReadabilitySettings
        {
            Mode = overrides.Mode is not null && ReadabilitySettings.IsValidMode(overrides.Mode)
                ? ReadabilitySettings.NormalizeMode(overrides.Mode)
                : normalizedBase.Mode,
            Strength = Math.Clamp(overrides.Strength ?? normalizedBase.Strength, 0, 255),
            Gradient = overrides.Gradient ?? normalizedBase.Gradient,
        };
    }

    private static ReadabilitySettings NormalizeReadability(ReadabilitySettings? readability)
    {
        if (readability is null)
        {
            return new ReadabilitySettings();
        }

        return new ReadabilitySettings
        {
            Mode = ReadabilitySettings.IsValidMode(readability.Mode)
                ? ReadabilitySettings.NormalizeMode(readability.Mode)
                : ReadabilitySettings.Auto,
            Strength = Math.Clamp(readability.Strength, 0, 255),
            Gradient = readability.Gradient,
        };
    }

    private static bool IsUnder(string path, string parent)
    {
        if (string.Equals(path, parent, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return parent.EndsWith(Path.DirectorySeparatorChar)
            ? path.StartsWith(parent, StringComparison.OrdinalIgnoreCase)
            : path.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}
