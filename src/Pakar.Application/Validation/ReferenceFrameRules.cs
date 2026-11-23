namespace Pakar.Application.Validation;

public static class ReferenceFrameRules
{
    public const int MaxBytes = 2 * 1024 * 1024;
    public static string? ContentType(ReadOnlySpan<byte> data)
    {
        if (data.Length == 0 || data.Length > MaxBytes) return null;
        if (data.Length >= 8 && data[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return "image/png";
        if (data.Length >= 3 && data[0] == 255 && data[1] == 216 && data[2] == 255) return "image/jpeg";
        return null;
    }
}
