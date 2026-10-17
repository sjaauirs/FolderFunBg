namespace FolderFunBg.Core.Tests;

public sealed class LegacyConfigImporterTests
{
    [Fact]
    public void ImportsDefaultsAndPerFolderRules()
    {
        using var temp = new TempDirectory();
        var legacyImages = temp.Combine("legacy", "Image");
        TestImages.WritePng(Path.Combine(legacyImages, "downloads.png"));
        TestImages.WritePng(Path.Combine(legacyImages, "thispc.png"));
        var ini = WriteLegacyIni(temp, """
            [load]
            folderExt=true
            noerror=true
            [image]
            random=false
            custom=true
            posType=3
            imgAlpha=200
            folder=
            [C:\Users\me\Downloads]
            img=downloads.png
            [::{20D04FE0-3AEA-1069-A2D8-08002B30309D}]
            img=thispc.png
            """);

        var result = LegacyConfigImporter.Import(ini, legacyImages, legacyImages);

        Assert.Equal(BackgroundPositions.BottomRight, result.Config.Defaults.Position);
        Assert.Equal(200, result.Config.Defaults.Alpha);
        Assert.False(result.Config.Defaults.Random);
        Assert.Equal("downloads.png", result.Config.Defaults.Image);
        Assert.Equal(2, result.Config.Folders.Count);

        var pathRule = Assert.Single(result.Config.Folders, rule => rule.Path is not null);
        Assert.Equal(@"C:\Users\me\Downloads", pathRule.Path);
        Assert.Equal("downloads.png", pathRule.Image);

        var clsidRule = Assert.Single(result.Config.Folders, rule => rule.Clsid is not null);
        Assert.Equal("::{20D04FE0-3AEA-1069-A2D8-08002B30309D}", clsidRule.Clsid);
        Assert.Equal("thispc.png", clsidRule.Image);
    }

    [Fact]
    public void RandomModeDoesNotSetDefaultImage()
    {
        using var temp = new TempDirectory();
        var legacyImages = temp.Combine("legacy", "Image");
        TestImages.WritePng(Path.Combine(legacyImages, "one.png"));
        var ini = WriteLegacyIni(temp, """
            [image]
            random=true
            custom=false
            posType=4
            imgAlpha=255
            folder=
            """);

        var result = LegacyConfigImporter.Import(ini, legacyImages, legacyImages);

        Assert.True(result.Config.Defaults.Random);
        Assert.Null(result.Config.Defaults.Image);
        Assert.Equal(BackgroundPositions.Center, result.Config.Defaults.Position);
    }

    [Fact]
    public void InvalidImageNameIsSkippedWithWarning()
    {
        using var temp = new TempDirectory();
        var legacyImages = temp.Combine("legacy", "Image");
        TestImages.WritePng(Path.Combine(legacyImages, "good.png"));
        var ini = WriteLegacyIni(temp, """
            [image]
            random=false
            custom=true
            posType=3
            imgAlpha=255
            folder=
            [C:\Users\me\Downloads]
            img=..\evil.png
            """);

        var result = LegacyConfigImporter.Import(ini, legacyImages, legacyImages);

        Assert.Empty(result.Config.Folders);
        Assert.Contains(result.Warnings, warning => warning.Contains("Skipped legacy section", StringComparison.Ordinal));
    }

    [Fact]
    public void CustomFalseStillImportsRulesAndWarns()
    {
        using var temp = new TempDirectory();
        var legacyImages = temp.Combine("legacy", "Image");
        TestImages.WritePng(Path.Combine(legacyImages, "downloads.png"));
        var ini = WriteLegacyIni(temp, """
            [image]
            random=false
            custom=false
            posType=3
            imgAlpha=255
            folder=
            [C:\Users\me\Downloads]
            img=downloads.png
            """);

        var result = LegacyConfigImporter.Import(ini, legacyImages, legacyImages);

        Assert.Single(result.Config.Folders);
        Assert.Contains(result.Warnings, warning => warning.Contains("custom=false", StringComparison.Ordinal));
    }

    [Fact]
    public void OutOfRangePositionFallsBackToBottomRight()
    {
        using var temp = new TempDirectory();
        var legacyImages = temp.Combine("legacy", "Image");
        TestImages.WritePng(Path.Combine(legacyImages, "one.png"));
        var ini = WriteLegacyIni(temp, """
            [image]
            random=false
            custom=false
            posType=99
            imgAlpha=255
            folder=
            """);

        var result = LegacyConfigImporter.Import(ini, legacyImages, legacyImages);

        Assert.Equal(BackgroundPositions.BottomRight, result.Config.Defaults.Position);
        Assert.Contains(result.Warnings, warning => warning.Contains("out of range", StringComparison.Ordinal));
    }

    private static string WriteLegacyIni(TempDirectory temp, string content)
    {
        var path = temp.Combine("legacy-config.ini");
        File.WriteAllText(path, content);
        return path;
    }
}
