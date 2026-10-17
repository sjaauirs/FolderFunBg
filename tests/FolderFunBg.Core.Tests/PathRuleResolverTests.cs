namespace FolderFunBg.Core.Tests;

public sealed class PathRuleResolverTests
{
    private static readonly string ThisPcClsid = "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}";

    [Fact]
    public void ExactPathRuleWinsOverRecursiveParent()
    {
        var config = CreateConfig();
        config.Folders.Add(new FolderRule
        {
            Path = @"C:\Users\me",
            Image = "home.png",
            Recursive = true,
            Alpha = 200,
        });
        config.Folders.Add(new FolderRule
        {
            Path = @"C:\Users\me\Downloads",
            Image = "downloads.png",
        });

        var resolved = PathRuleResolver.Resolve(config, @"C:\Users\me\Downloads");

        Assert.Equal("downloads.png", resolved.Image);
        Assert.Equal(BackgroundPositions.BottomRight, resolved.Position);
        Assert.Equal(255, resolved.Alpha);
    }

    [Fact]
    public void RecursiveRuleAppliesToChildren()
    {
        var config = CreateConfig();
        config.Folders.Add(new FolderRule
        {
            Path = @"C:\Users\me",
            Image = "home.png",
            Recursive = true,
            Position = BackgroundPositions.Center,
            Alpha = 128,
        });

        var resolved = PathRuleResolver.Resolve(config, @"C:\Users\me\Documents\Project");

        Assert.Equal("home.png", resolved.Image);
        Assert.Equal(BackgroundPositions.Center, resolved.Position);
        Assert.Equal(128, resolved.Alpha);
    }

    [Fact]
    public void PathMatchingIsCaseInsensitiveAndIgnoresTrailingSeparator()
    {
        var config = CreateConfig();
        config.Folders.Add(new FolderRule
        {
            Path = @"C:\Users\Me\Downloads",
            Image = "downloads.png",
        });

        var resolved = PathRuleResolver.Resolve(config, @"c:\users\me\downloads\");

        Assert.Equal("downloads.png", resolved.Image);
    }

    [Fact]
    public void ClsidRuleMatchesBothCanonicalForms()
    {
        var config = CreateConfig();
        config.Folders.Add(new FolderRule
        {
            Clsid = ThisPcClsid,
            Image = "thispc.png",
        });

        var withPrefix = PathRuleResolver.Resolve(config, ThisPcClsid);
        var withoutPrefix = PathRuleResolver.Resolve(config, "{20D04FE0-3AEA-1069-A2D8-08002B30309D}");

        Assert.Equal("thispc.png", withPrefix.Image);
        Assert.Equal("thispc.png", withoutPrefix.Image);
    }

    [Fact]
    public void NoMatchFallsBackToDefaults()
    {
        var config = CreateConfig();
        config.Defaults.Image = "default.png";
        config.Defaults.Position = BackgroundPositions.TopLeft;
        config.Defaults.Alpha = 42;

        var resolved = PathRuleResolver.Resolve(config, @"C:\Somewhere\Else");

        Assert.Equal("default.png", resolved.Image);
        Assert.Equal(BackgroundPositions.TopLeft, resolved.Position);
        Assert.Equal(42, resolved.Alpha);
        Assert.Null(resolved.Rule);
    }

    [Fact]
    public void DisabledRuleIsIgnored()
    {
        var config = CreateConfig();
        config.Folders.Add(new FolderRule
        {
            Path = @"C:\Users\me\Downloads",
            Image = "downloads.png",
            Enabled = false,
        });

        var resolved = PathRuleResolver.Resolve(config, @"C:\Users\me\Downloads");

        Assert.Null(resolved.Image);
        Assert.Null(resolved.Rule);
    }

    [Fact]
    public void RuleInheritsUnspecifiedFieldsFromDefaults()
    {
        var config = CreateConfig();
        config.Defaults.Position = BackgroundPositions.Center;
        config.Defaults.Alpha = 90;
        config.Defaults.Random = true;
        config.Folders.Add(new FolderRule
        {
            Path = @"C:\Users\me\Downloads",
            Image = "downloads.png",
        });

        var resolved = PathRuleResolver.Resolve(config, @"C:\Users\me\Downloads");

        Assert.Equal(BackgroundPositions.Center, resolved.Position);
        Assert.Equal(90, resolved.Alpha);
        Assert.True(resolved.Random);
    }

    [Fact]
    public void RuleReadabilityOverridesOnlySpecifiedFields()
    {
        var config = CreateConfig();
        config.Readability = new ReadabilitySettings
        {
            Mode = ReadabilitySettings.Light,
            Strength = 20,
            Gradient = true,
        };
        config.Folders.Add(new FolderRule
        {
            Path = @"C:\Users\me\Downloads",
            Readability = new ReadabilityOverride
            {
                Mode = ReadabilitySettings.Dark,
                Strength = 200,
            },
        });

        var resolved = PathRuleResolver.Resolve(config, @"C:\Users\me\Downloads");

        Assert.Equal(ReadabilitySettings.Dark, resolved.Readability.Mode);
        Assert.Equal(200, resolved.Readability.Strength);
        Assert.True(resolved.Readability.Gradient);
    }

    [Fact]
    public void NoRuleUsesGlobalReadability()
    {
        var config = CreateConfig();
        config.Readability = new ReadabilitySettings
        {
            Mode = ReadabilitySettings.Light,
            Strength = 33,
            Gradient = false,
        };

        var resolved = PathRuleResolver.Resolve(config, @"C:\Somewhere\Else");

        Assert.Equal(ReadabilitySettings.Light, resolved.Readability.Mode);
        Assert.Equal(33, resolved.Readability.Strength);
        Assert.False(resolved.Readability.Gradient);
    }

    [Fact]
    public void LongestRecursiveRuleWins()
    {
        var config = CreateConfig();
        config.Folders.Add(new FolderRule
        {
            Path = @"C:\Users",
            Image = "users.png",
            Recursive = true,
        });
        config.Folders.Add(new FolderRule
        {
            Path = @"C:\Users\me\Work",
            Image = "work.png",
            Recursive = true,
        });

        var resolved = PathRuleResolver.Resolve(config, @"C:\Users\me\Work\Repo");

        Assert.Equal("work.png", resolved.Image);
    }

    private static FolderFunBgConfig CreateConfig() => new()
    {
        ImagesRoot = @"C:\Images",
        Defaults = new BackgroundDefaults(),
    };
}
