using System.Security.Cryptography;

namespace ConsoleAESKeyGenApp;

public static class GuidV7
{
    public static Guid NewGuid()
    {
        long unixTimeMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        byte[] timestampBytes = BitConverter.GetBytes(unixTimeMillis);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(timestampBytes);
        }

        byte[] guidBytes = new byte[16];

        Array.Copy(timestampBytes, 2, guidBytes, 0, 6);

        byte[] randomBytes = new byte[10];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }

        Array.Copy(randomBytes, 0, guidBytes, 6, 10);

        guidBytes[6] &= 0x0F;
        guidBytes[6] |= 0x70;

        guidBytes[8] &= 0x3F;
        guidBytes[8] |= 0x80;

        return new Guid(guidBytes);
    }
}