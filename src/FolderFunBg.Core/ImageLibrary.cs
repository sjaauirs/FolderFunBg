namespace FolderFunBg.Core;

public sealed record ImageInfo(string FileName, string FullPath, long Length);

/// <summary>
/// Manages the per-user image folder. Files are validated by extension, size,
/// and magic bytes; the native hook performs the actual GDI+ decode.
/// </summary>
public sealed class ImageLibrary
{
    public const long MaxFileBytes = 64L * 1024 * 1024;

    private static readonly string[] SupportedExtensions = [".png", ".jpg", ".jpeg"];
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public ImageLibrary(string imagesRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagesRoot);
        ImagesRoot = Path.GetFullPath(imagesRoot);
    }

    public string ImagesRoot { get; }

    public ImageInfo Import(string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        var source = Path.GetFullPath(sourcePath);
        if (!File.Exists(source))
        {
            throw new FileNotFoundException("Image file not found.", source);
        }

        if (!HasSupportedExtension(source))
        {
            throw new InvalidDataException("Only .png, .jpg, and .jpeg images are supported.");
        }

        var length = new FileInfo(source).Length;
        if (length <= 0 || length > MaxFileBytes)
        {
            throw new InvalidDataException($"Image must be between 1 byte and {MaxFileBytes} bytes.");
        }

        if (!HasValidSignature(source, out var signatureError))
        {
            throw new InvalidDataException(signatureError ?? "Image signature is not valid.");
        }

        Directory.CreateDirectory(ImagesRoot);

        if (IsInsideLibrary(source))
        {
            return new ImageInfo(Path.GetFileName(source), source, length);
        }

        var fileName = Path.GetFileName(source);
        if (!ConfigValidator.IsSafeImageFileName(fileName))
        {
            fileName = $"image-{Guid.NewGuid():N}{Path.GetExtension(source).ToLowerInvariant()}";
        }

        var destination = MakeUniquePath(fileName);
        File.Copy(source, destination, overwrite: false);
        return new ImageInfo(Path.GetFileName(destination), destination, length);
    }

    public IReadOnlyList<ImageInfo> List()
    {
        if (!Directory.Exists(ImagesRoot))
        {
            return [];
        }

        var images = new List<ImageInfo>();
        foreach (var path in Directory.EnumerateFiles(ImagesRoot, "*", SearchOption.TopDirectoryOnly))
        {
            if (!HasSupportedExtension(path) || !HasValidSignature(path, out _))
            {
                continue;
            }

            var info = new FileInfo(path);
            images.Add(new ImageInfo(info.Name, info.FullName, info.Length));
        }

        return images
            .OrderBy(image => image.FileName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public void Delete(string fileName)
    {
        var path = ResolvePath(fileName);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public string ResolvePath(string fileName)
    {
        if (!ConfigValidator.IsSafeImageFileName(fileName))
        {
            throw new ArgumentException("Invalid image file name.", nameof(fileName));
        }

        var full = Path.GetFullPath(Path.Combine(ImagesRoot, fileName));
        var rootWithSeparator = ImagesRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        if (!full.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Image path escapes the image library.", nameof(fileName));
        }

        return full;
    }

    public static bool HasSupportedExtension(string path)
    {
        var extension = Path.GetExtension(path);
        return SupportedExtensions.Any(supported =>
            string.Equals(extension, supported, StringComparison.OrdinalIgnoreCase));
    }

    public static bool HasValidSignature(string path, out string? error)
    {
        Span<byte> header = stackalloc byte[8];
        int read;
        using (var stream = File.OpenRead(path))
        {
            read = stream.Read(header);
        }

        if (read >= PngSignature.Length && header[..PngSignature.Length].SequenceEqual(PngSignature))
        {
            error = null;
            return true;
        }

        if (read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            error = null;
            return true;
        }

        error = "File is not a valid PNG or JPEG image.";
        return false;
    }

    private bool IsInsideLibrary(string fullPath)
    {
        var rootWithSeparator = ImagesRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
    }

    private string MakeUniquePath(string fileName)
    {
        var destination = Path.Combine(ImagesRoot, fileName);
        if (!File.Exists(destination))
        {
            return destination;
        }

        var stem = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        for (var index = 1; ; index++)
        {
            destination = Path.Combine(ImagesRoot, $"{stem}-{index}{extension}");
            if (!File.Exists(destination))
            {
                return destination;
            }
        }
    }
}
