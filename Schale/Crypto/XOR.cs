namespace Schale.Crypto
{
    public static class XOR
    {
        public static void Crypt(byte[] data, byte[] key, uint startOffset = 0)
        {
            var offset = startOffset;
            while (offset < data.Length)
            {
                data[offset] ^= key[offset % key.Length];
                offset++;
            }
        }
    }
}


