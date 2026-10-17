namespace FolderFunBg.Core.Tests;

public sealed class ConfigStoreTests
{
    [Fact]
    public void SaveThenLoadRoundTrips()
    {
        using var temp = new TempDirectory();
        var store = new ConfigStore(temp.Root);
        var config = CreateConfig(store.ImagesPath);
        config.Readability.Mode = ReadabilitySettings.Light;
        config.Readability.Strength = 77;
        config.Folders.Add(new FolderRule
        {
            Path = @"C:\Users\me\Downloads",
            Image = "downloads.png",
            Position = BackgroundPositions.Center,
            Alpha = 123,
        });

        store.Save(config);
        var loaded = store.Load();

        Assert.Equal(config.ImagesRoot, loaded.ImagesRoot);
        Assert.Equal(ReadabilitySettings.Light, loaded.Readability.Mode);
        Assert.Equal(77, loaded.Readability.Strength);
        Assert.Single(loaded.Folders);
        Assert.Equal("downloads.png", loaded.Folders[0].Image);
        Assert.Equal(BackgroundPositions.Center, loaded.Folders[0].Position);
        Assert.Equal(123, loaded.Folders[0].Alpha);
    }

    [Fact]
    public void MissingConfigReturnsDefaultDocument()
    {
        using var temp = new TempDirectory();
        var store = new ConfigStore(temp.Root);

        var config = store.Load();

        Assert.Equal(1, config.Version);
        Assert.Empty(config.Folders);
    }

    [Fact]
    public void SaveWritesCamelCaseIndentedJson()
    {
        using var temp = new TempDirectory();
        var store = new ConfigStore(temp.Root);
        store.Save(CreateConfig(store.ImagesPath));

        var json = File.ReadAllText(store.ConfigPath);

        Assert.Contains("\"imagesRoot\"", json, StringComparison.Ordinal);
        Assert.Contains("\n", json, StringComparison.Ordinal);
    }

    [Fact]
    public void SaveLeavesNoTemporaryFiles()
    {
        using var temp = new TempDirectory();
        var store = new ConfigStore(temp.Root);
        store.Save(CreateConfig(store.ImagesPath));

        Assert.Empty(Directory.GetFiles(temp.Root, "config.json.tmp-*"));
    }

    [Fact]
    public void InvalidConfigIsRejectedOnSave()
    {
        using var temp = new TempDirectory();
        var store = new ConfigStore(temp.Root);
        var config = CreateConfig(store.ImagesPath);
        config.Defaults.Position = "diagonal";

        Assert.Throws<ConfigValidationException>(() => store.Save(config));
    }

    [Fact]
    public void InvalidJsonIsRejectedOnLoad()
    {
        using var temp = new TempDirectory();
        var store = new ConfigStore(temp.Root);
        File.WriteAllText(store.ConfigPath, "{ not json }");

        Assert.Throws<ConfigValidationException>(() => store.Load());
    }

    private static FolderFunBgConfig CreateConfig(string imagesPath) => new()
    {
        ImagesRoot = imagesPath,
        Defaults = new BackgroundDefaults
        {
            Position = BackgroundPositions.BottomRight,
            Alpha = 255,
        },
    };
}
