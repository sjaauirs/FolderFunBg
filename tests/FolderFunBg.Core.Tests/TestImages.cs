namespace FolderFunBg.Core.Tests;

internal static class TestImages
{
    private static readonly byte[] PngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] JpegHeader = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46];

    public static string WritePng(string path)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, [.. PngHeader, 0x00, 0x00, 0x00, 0x00]);
        return path;
    }

    public static string WriteJpeg(string path)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, [.. JpegHeader, 0x00, 0x00]);
        return path;
    }

    public static string WriteInvalid(string path)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, [0x00, 0x01, 0x02, 0x03]);
        return path;
    }
}
