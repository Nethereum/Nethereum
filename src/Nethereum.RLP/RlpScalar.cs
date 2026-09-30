namespace Nethereum.RLP
{
    /// <summary>
    /// Yellow Paper, of an RLP item that is not canonically encoded: <i>"dismissing it
    /// completely"</i>. A scalar is canonical when it carries no leading zero byte; zero is
    /// the empty string.
    /// </summary>
    public static class RlpScalar
    {
        private const int BytesInA256BitField = 32;

        public static bool IsCanonical(byte[] itemBytes)
        {
            if (itemBytes == null || itemBytes.Length == 0) return true;
            return itemBytes[0] != 0;
        }

        public static bool FitsA256BitField(byte[] itemBytes)
        {
            if (itemBytes == null) return true;
            return itemBytes.Length <= BytesInA256BitField;
        }
    }
}
