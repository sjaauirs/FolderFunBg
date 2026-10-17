using FolderFunBg.Core;
using FolderFunBg.Settings.Services;
using Microsoft.UI.Xaml.Media.Imaging;

namespace FolderFunBg.Settings.Models;

public sealed class ImageItem
{
    public string FileName { get; set; } = string.Empty;

    public string FullPath { get; set; } = string.Empty;

    public BitmapImage? Thumbnail { get; set; }

    public static ImageItem FromInfo(ImageInfo info) => new()
    {
        FileName = info.FileName,
        FullPath = info.FullPath,
        Thumbnail = AppServices.CreateThumbnail(info.FileName),
    };
}
