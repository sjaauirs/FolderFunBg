namespace FolderFunBg.Core.Tests;

public sealed class ImageLibraryTests
{
    [Fact]
    public void ImportCopiesAndDeduplicatesFileNames()
    {
        using var temp = new TempDirectory();
        var source = TestImages.WritePng(temp.Combine("source", "wall.png"));
        var library = new ImageLibrary(temp.Combine("Images"));

        var first = library.Import(source);
        var second = library.Import(source);

        Assert.Equal("wall.png", first.FileName);
        Assert.Equal("wall-1.png", second.FileName);
        Assert.Equal(2, library.List().Count);
    }

    [Fact]
    public void ImportOfFileAlreadyInLibraryDoesNotDuplicate()
    {
        using var temp = new TempDirectory();
        var library = new ImageLibrary(temp.Combine("Images"));
        Directory.CreateDirectory(library.ImagesRoot);
        var source = TestImages.WritePng(Path.Combine(library.ImagesRoot, "wall.png"));

        var imported = library.Import(source);

        Assert.Equal("wall.png", imported.FileName);
        Assert.Single(library.List());
    }

    [Fact]
    public void InvalidSignatureIsRejected()
    {
        using var temp = new TempDirectory();
        var source = TestImages.WriteInvalid(temp.Combine("source", "fake.png"));
        var library = new ImageLibrary(temp.Combine("Images"));

        Assert.Throws<InvalidDataException>(() => library.Import(source));
    }

    [Fact]
    public void UnsupportedExtensionIsRejected()
    {
        using var temp = new TempDirectory();
        var source = temp.Combine("source", "wall.gif");
        Directory.CreateDirectory(Path.GetDirectoryName(source)!);
        File.WriteAllBytes(source, [0x47, 0x49, 0x46, 0x38]);
        var library = new ImageLibrary(temp.Combine("Images"));

        Assert.Throws<InvalidDataException>(() => library.Import(source));
    }

    [Fact]
    public void JpegSignatureIsAccepted()
    {
        using var temp = new TempDirectory();
        var source = TestImages.WriteJpeg(temp.Combine("source", "wall.jpg"));
        var library = new ImageLibrary(temp.Combine("Images"));

        var imported = library.Import(source);

        Assert.Equal("wall.jpg", imported.FileName);
    }

    [Fact]
    public void ResolvePathRejectsTraversal()
    {
        using var temp = new TempDirectory();
        var library = new ImageLibrary(temp.Combine("Images"));

        Assert.Throws<ArgumentException>(() => library.ResolvePath(@"..\evil.png"));
    }

    [Fact]
    public void DeleteRemovesImage()
    {
        using var temp = new TempDirectory();
        var source = TestImages.WritePng(temp.Combine("source", "wall.png"));
        var library = new ImageLibrary(temp.Combine("Images"));
        library.Import(source);

        library.Delete("wall.png");

        Assert.Empty(library.List());
    }

    [Fact]
    public void ListIgnoresFilesWithInvalidSignatures()
    {
        using var temp = new TempDirectory();
        var library = new ImageLibrary(temp.Combine("Images"));
        Directory.CreateDirectory(library.ImagesRoot);
        TestImages.WriteInvalid(Path.Combine(library.ImagesRoot, "bad.png"));
        TestImages.WritePng(Path.Combine(library.ImagesRoot, "good.png"));

        var images = library.List();

        Assert.Single(images);
        Assert.Equal("good.png", images[0].FileName);
    }
}
