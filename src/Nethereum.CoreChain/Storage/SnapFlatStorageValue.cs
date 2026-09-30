namespace Nethereum.CoreChain.Storage
{
    public static class SnapFlatStorageValue
    {
        public static bool ClearsSlot(byte[] value)
        {
            if (value == null || value.Length == 0) return true;
            for (var i = 0; i < value.Length; i++)
                if (value[i] != 0) return false;
            return true;
        }
    }
}
