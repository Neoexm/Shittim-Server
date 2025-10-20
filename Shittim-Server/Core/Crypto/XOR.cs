namespace BlueArchiveAPI.Core.Crypto
{
    public static class XOR
    {
        public static void Crypt(byte[] data, byte[] key)
        {
            for (int i = 0; i < data.Length; i++)
            {
                data[i] ^= key[0];
            }
        }
    }
}
