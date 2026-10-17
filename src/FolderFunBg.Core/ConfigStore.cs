using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FolderFunBg.Core;

/// <summary>
/// Reads and writes the shared config document. Writes are atomic: a temporary
/// file in the same directory is fully written and then moved over the target,
/// so the Explorer hook never observes a partially written document.
/// </summary>
public sealed class ConfigStore
{
    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public ConfigStore(string? rootDirectory = null)
    {
        RootDirectory = Path.GetFullPath(rootDirectory ?? FolderFunBgPaths.DefaultRoot);
        ConfigPath = Path.Combine(RootDirectory, "config.json");
        ImagesPath = Path.Combine(RootDirectory, "Images");
    }

    public string RootDirectory { get; }

    public string ConfigPath { get; }

    public string ImagesPath { get; }

    public FolderFunBgConfig Load()
    {
        if (!File.Exists(ConfigPath))
        {
            return new FolderFunBgConfig();
        }

        var json = File.ReadAllText(ConfigPath, Encoding.UTF8);
        FolderFunBgConfig? config;
        try
        {
            config = JsonSerializer.Deserialize<FolderFunBgConfig>(json, JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new ConfigValidationException([$"config.json is not valid JSON: {exception.Message}"]);
        }

        if (config is null)
        {
            throw new ConfigValidationException(["config.json is empty."]);
        }

        ConfigValidator.ValidateOrThrow(config);
        return config;
    }

    public void Save(FolderFunBgConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        ConfigValidator.ValidateOrThrow(config);

        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(ImagesPath);

        var json = JsonSerializer.Serialize(config, JsonOptions);
        var tempPath = Path.Combine(RootDirectory, $"config.json.tmp-{Guid.NewGuid():N}");

        try
        {
            File.WriteAllText(tempPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(tempPath, ConfigPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
}
