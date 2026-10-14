using System.Net.Http.Headers;

namespace OpenDownload.Core;

public static class FileFormats
{
    public static string ResponseName(string fallback, ContentDispositionHeaderValue? disposition, string? contentType)
    {
        string? supplied = disposition?.FileNameStar ?? disposition?.FileName;
        string name = string.IsNullOrWhiteSpace(supplied) ? fallback : DownloadNames.Sanitize(supplied.Trim('"'));
        string? extension = contentType?.ToLowerInvariant() switch
        {
            "image/png" => ".png", "image/jpeg" => ".jpg", "image/gif" => ".gif", "image/webp" => ".webp",
            "image/avif" => ".avif", "image/bmp" => ".bmp", "image/tiff" => ".tiff", "image/svg+xml" => ".svg",
            "application/pdf" => ".pdf", "application/zip" => ".zip", "application/x-7z-compressed" => ".7z",
            _ => null
        };
        if (extension != null && (contentType!.StartsWith("image/", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(name) is "" or ".bin"))
            name = Path.ChangeExtension(name, extension);
        return DownloadNames.Sanitize(name);
    }
    public static string? ImageExtension(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith(new byte[] { 137,80,78,71,13,10,26,10 })) return ".png";
        if (bytes.StartsWith(new byte[] { 255,216,255 })) return ".jpg";
        if (bytes.StartsWith("GIF87a"u8) || bytes.StartsWith("GIF89a"u8)) return ".gif";
        if (bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes.Slice(8,4).SequenceEqual("WEBP"u8)) return ".webp";
        if (bytes.Length >= 12 && bytes.Slice(4,4).SequenceEqual("ftyp"u8) && (bytes.Slice(8,4).SequenceEqual("avif"u8) || bytes.Slice(8,4).SequenceEqual("avis"u8))) return ".avif";
        return null;
    }
}
