namespace Plana.Crypto
{
    public static class XOR
    {
        public static void Crypt(byte[] bytes, byte[] key, uint offset = 0)
        {
            while (offset < bytes.Length)
            {
                bytes[offset] ^= key[offset % key.Length];
                offset++;
            }
        }
    }
}
