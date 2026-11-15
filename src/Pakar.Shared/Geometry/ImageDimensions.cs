using System.Buffers.Binary;

namespace Pakar.Shared.Geometry;

public static class ImageDimensions
{
    public static (int Width, int Height)? Read(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 24 && bytes[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
            return Valid(BinaryPrimitives.ReadInt32BigEndian(bytes[16..20]), BinaryPrimitives.ReadInt32BigEndian(bytes[20..24]));
        if (bytes.Length < 4 || bytes[0] != 255 || bytes[1] != 216) return null;
        int offset = 2;
        while (offset + 4 <= bytes.Length)
        {
            if (bytes[offset++] != 255) return null;
            while (offset < bytes.Length && bytes[offset] == 255) offset++;
            if (offset >= bytes.Length) return null;
            byte marker = bytes[offset++];
            if (marker is 0xD9 or 0xDA) break;
            if (marker is 0x01 or >= 0xD0 and <= 0xD7) continue;
            if (offset + 2 > bytes.Length) return null;
            int length = BinaryPrimitives.ReadUInt16BigEndian(bytes[offset..]);
            if (length < 2 || offset + length > bytes.Length) return null;
            if (marker is 0xC0 or 0xC1 or 0xC2 or 0xC3 or 0xC5 or 0xC6 or 0xC7 or 0xC9 or 0xCA or 0xCB or 0xCD or 0xCE or 0xCF)
            {
                if (length < 7) return null;
                return Valid(BinaryPrimitives.ReadUInt16BigEndian(bytes[(offset + 5)..]), BinaryPrimitives.ReadUInt16BigEndian(bytes[(offset + 3)..]));
            }
            offset += length;
        }
        return null;
    }
    private static (int, int)? Valid(int w, int h) => w > 0 && h > 0 && w <= 16384 && h <= 16384 ? (w, h) : null;
}
