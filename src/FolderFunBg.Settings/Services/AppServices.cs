using FolderFunBg.Core;
using Microsoft.UI.Xaml.Media.Imaging;

namespace FolderFunBg.Settings.Services;

public static class AppServices
{
    public static ConfigStore Store { get; private set; } = null!;

    public static ImageLibrary Images { get; private set; } = null!;

    public static FolderFunBgConfig Config { get; private set; } = null!;

    public static string ImagesRoot { get; private set; } = string.Empty;

    public static string? StartupError { get; private set; }

    public static void Initialize()
    {
        try
        {
            Store = new ConfigStore();
            Config = Store.Load();
            ImagesRoot = FolderFunBgPaths.ExpandPath(Config.ImagesRoot);
            Images = new ImageLibrary(ImagesRoot);
            Directory.CreateDirectory(ImagesRoot);
        }
        catch (Exception exception)
        {
            StartupError = exception.Message;
            Store = new ConfigStore();
            Config = new FolderFunBgConfig { ImagesRoot = Store.ImagesPath };
            ImagesRoot = Store.ImagesPath;
            Images = new ImageLibrary(ImagesRoot);
            Directory.CreateDirectory(ImagesRoot);
        }
    }

    public static void Save()
    {
        Config.ImagesRoot = ImagesRoot;
        Store.Save(Config);
    }

    public static IReadOnlyList<string> ImageFileNames() =>
        Images.List().Select(image => image.FileName).ToArray();

    public static string? ResolveImagePath(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        try
        {
            return Images.ResolvePath(fileName);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    public static BitmapImage? CreateThumbnail(string? fileName, int decodeWidth = 160)
    {
        var path = ResolveImagePath(fileName);
        if (path is null || !File.Exists(path))
        {
            return null;
        }

        try
        {
            return new BitmapImage(new Uri(path)) { DecodePixelWidth = decodeWidth };
        }
        catch (UriFormatException)
        {
            return null;
        }
    }
}
