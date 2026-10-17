using System.Globalization;
using System.Text;

namespace FolderFunBg.Core;

public sealed record LegacyImportResult(
    FolderFunBgConfig Config,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Imports the legacy ExplorerBgTool config.ini format. The legacy file is
/// typically GBK-encoded; only ASCII keys and ASCII/Unicode section names are
/// interpreted.
/// </summary>
public static class LegacyConfigImporter
{
    private static readonly string[] PositionByIndex =
    [
        BackgroundPositions.TopLeft,
        BackgroundPositions.TopRight,
        BackgroundPositions.BottomLeft,
        BackgroundPositions.BottomRight,
        BackgroundPositions.Center,
        BackgroundPositions.Stretch,
        BackgroundPositions.ZoomFill,
    ];

    static LegacyConfigImporter()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static LegacyImportResult Import(
        string legacyConfigPath,
        string defaultImagesRoot,
        string? legacyImagesDirectory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(legacyConfigPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultImagesRoot);

        var warnings = new List<string>();
        var text = ReadLegacyText(legacyConfigPath);
        var sections = ParseSections(text);

        var position = BackgroundPositions.BottomRight;
        var alpha = 255;
        var random = false;
        var imagesRoot = defaultImagesRoot;
        var custom = false;

        if (sections.TryGetValue("image", out var imageSection))
        {
            if (TryGet(imageSection, "random", out var randomText) &&
                bool.TryParse(randomText, out var parsedRandom))
            {
                random = parsedRandom;
            }

            if (TryGet(imageSection, "custom", out var customText) &&
                bool.TryParse(customText, out var parsedCustom))
            {
                custom = parsedCustom;
            }

            if (TryGet(imageSection, "posType", out var positionText) &&
                int.TryParse(positionText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var positionIndex))
            {
                if (positionIndex >= 0 && positionIndex < PositionByIndex.Length)
                {
                    position = PositionByIndex[positionIndex];
                }
                else
                {
                    warnings.Add($"Legacy posType '{positionText}' is out of range; using bottom-right.");
                }
            }

            if (TryGet(imageSection, "imgAlpha", out var alphaText) &&
                int.TryParse(alphaText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedAlpha))
            {
                alpha = Math.Clamp(parsedAlpha, 0, 255);
            }

            if (TryGet(imageSection, "folder", out var folder) && !string.IsNullOrWhiteSpace(folder))
            {
                imagesRoot = folder;
            }
            else if (!string.IsNullOrWhiteSpace(legacyImagesDirectory))
            {
                imagesRoot = legacyImagesDirectory;
            }
        }
        else if (!string.IsNullOrWhiteSpace(legacyImagesDirectory))
        {
            imagesRoot = legacyImagesDirectory;
        }

        var defaults = new BackgroundDefaults
        {
            Position = position,
            Alpha = alpha,
            Random = random,
        };

        if (!random && !string.IsNullOrWhiteSpace(legacyImagesDirectory))
        {
            var firstImage = FindFirstImage(legacyImagesDirectory);
            if (firstImage is not null)
            {
                defaults.Image = firstImage;
            }
            else
            {
                warnings.Add("No .png/.jpg image was found in the legacy image folder; set a default image in FolderFunBg Settings.");
            }
        }

        var config = new FolderFunBgConfig
        {
            Version = 1,
            ImagesRoot = imagesRoot,
            Defaults = defaults,
        };

        foreach (var (sectionName, values) in sections)
        {
            if (string.Equals(sectionName, "image", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(sectionName, "load", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!TryGet(values, "img", out var image) || string.IsNullOrWhiteSpace(image))
            {
                continue;
            }

            if (!ConfigValidator.IsSafeImageFileName(image))
            {
                warnings.Add($"Skipped legacy section '{sectionName}': image '{image}' is not a supported file name.");
                continue;
            }

            var rule = new FolderRule
            {
                Image = image,
                Enabled = true,
            };

            var clsid = PathRuleResolver.TryNormalizeClsid(sectionName);
            if (clsid is not null &&
                (sectionName.TrimStart().StartsWith("::", StringComparison.Ordinal) ||
                 sectionName.TrimStart().StartsWith('{')))
            {
                rule.Clsid = clsid;
            }
            else
            {
                var path = PathRuleResolver.TryNormalizePath(sectionName);
                if (path is null)
                {
                    warnings.Add($"Skipped legacy section '{sectionName}': path is not valid.");
                    continue;
                }

                rule.Path = path;
            }

            config.Folders.Add(rule);
        }

        if (!custom && config.Folders.Count > 0)
        {
            warnings.Add("The legacy config had custom=false; per-folder sections were imported anyway and are now active.");
        }

        ConfigValidator.ValidateOrThrow(config);
        return new LegacyImportResult(config, warnings);
    }

    private static string ReadLegacyText(string path)
    {
        try
        {
            return File.ReadAllText(path, Encoding.GetEncoding(936));
        }
        catch (ArgumentException)
        {
            return File.ReadAllText(path, Encoding.Latin1);
        }
    }

    private static Dictionary<string, Dictionary<string, string>> ParseSections(string text)
    {
        var sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        string? current = null;

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line[0] is '#' or ';')
            {
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                current = line[1..^1].Trim();
                sections.TryAdd(current, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
                continue;
            }

            if (current is null)
            {
                continue;
            }

            var equals = line.IndexOf('=');
            if (equals <= 0)
            {
                continue;
            }

            var key = line[..equals].Trim();
            var value = line[(equals + 1)..].Trim();
            sections[current][key] = value;
        }

        return sections;
    }

    private static bool TryGet(Dictionary<string, string> values, string key, out string value) =>
        values.TryGetValue(key, out value!);

    private static string? FindFirstImage(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return null;
        }

        return Directory
            .EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
            .Where(ImageLibrary.HasSupportedExtension)
            .Where(path => ImageLibrary.HasValidSignature(path, out _))
            .Select(Path.GetFileName)
            .Where(name => name is not null && ConfigValidator.IsSafeImageFileName(name))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }
}
