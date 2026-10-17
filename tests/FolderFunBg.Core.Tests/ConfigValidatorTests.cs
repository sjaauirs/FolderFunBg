namespace FolderFunBg.Core.Tests;

public sealed class ConfigValidatorTests
{
    [Fact]
    public void ValidConfigHasNoErrors()
    {
        var config = CreateValidConfig();
        config.Folders.Add(new FolderRule { Path = @"C:\Users\me\Downloads", Image = "downloads.png" });

        Assert.Empty(ConfigValidator.Validate(config));
    }

    [Fact]
    public void InvalidPositionIsReported()
    {
        var config = CreateValidConfig();
        config.Defaults.Position = "diagonal";

        var errors = ConfigValidator.Validate(config);

        Assert.Contains(errors, error => error.Contains("defaults.position", StringComparison.Ordinal));
    }

    [Fact]
    public void AlphaOutsideRangeIsReported()
    {
        var config = CreateValidConfig();
        config.Folders.Add(new FolderRule { Path = @"C:\A", Alpha = 300 });

        var errors = ConfigValidator.Validate(config);

        Assert.Contains(errors, error => error.Contains("alpha", StringComparison.Ordinal));
    }

    [Fact]
    public void RuleRequiresExactlyOneIdentifier()
    {
        var config = CreateValidConfig();
        config.Folders.Add(new FolderRule
        {
            Path = @"C:\A",
            Clsid = "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}",
        });

        var errors = ConfigValidator.Validate(config);

        Assert.Contains(errors, error => error.Contains("exactly one", StringComparison.Ordinal));
    }

    [Fact]
    public void RecursiveClsidIsRejected()
    {
        var config = CreateValidConfig();
        config.Folders.Add(new FolderRule
        {
            Clsid = "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}",
            Recursive = true,
        });

        var errors = ConfigValidator.Validate(config);

        Assert.Contains(errors, error => error.Contains("recursive", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("..\\evil.png")]
    [InlineData("sub/evil.png")]
    [InlineData("evil.exe")]
    [InlineData("evil")]
    public void UnsafeImageNamesAreRejected(string image)
    {
        var config = CreateValidConfig();
        config.Folders.Add(new FolderRule { Path = @"C:\A", Image = image });

        var errors = ConfigValidator.Validate(config);

        Assert.Contains(errors, error => error.Contains("image", StringComparison.Ordinal));
    }

    [Fact]
    public void DuplicateEnabledRulesAreRejected()
    {
        var config = CreateValidConfig();
        config.Folders.Add(new FolderRule { Path = @"C:\A", Image = "one.png" });
        config.Folders.Add(new FolderRule { Path = @"c:\a\", Image = "two.png" });

        var errors = ConfigValidator.Validate(config);

        Assert.Contains(errors, error => error.Contains("duplicates", StringComparison.Ordinal));
    }

    [Fact]
    public void RelativePathIsRejected()
    {
        var config = CreateValidConfig();
        config.Folders.Add(new FolderRule { Path = "relative\\folder" });

        var errors = ConfigValidator.Validate(config);

        Assert.Contains(errors, error => error.Contains("absolute", StringComparison.Ordinal));
    }

    [Fact]
    public void InvalidReadabilityModeIsReported()
    {
        var config = CreateValidConfig();
        config.Readability.Mode = "purple";

        var errors = ConfigValidator.Validate(config);

        Assert.Contains(errors, error => error.Contains("readability.mode", StringComparison.Ordinal));
    }

    [Fact]
    public void RuleReadabilityStrengthOutsideRangeIsReported()
    {
        var config = CreateValidConfig();
        config.Folders.Add(new FolderRule
        {
            Path = @"C:\A",
            Readability = new ReadabilityOverride { Strength = 999 },
        });

        var errors = ConfigValidator.Validate(config);

        Assert.Contains(errors, error => error.Contains("readability.strength", StringComparison.Ordinal));
    }

    [Fact]
    public void RandomWithoutImageIsValid()
    {
        var config = CreateValidConfig();
        config.Defaults.Random = true;
        config.Defaults.Image = null;

        Assert.Empty(ConfigValidator.Validate(config));
    }

    private static FolderFunBgConfig CreateValidConfig() => new()
    {
        ImagesRoot = @"C:\Images",
        Defaults = new BackgroundDefaults
        {
            Position = BackgroundPositions.BottomRight,
            Alpha = 255,
        },
    };
}
