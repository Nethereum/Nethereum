using Nethereum.Hex.HexTypes;

namespace Nethereum.BlockchainProcessing.BlockStorage.Entities.Mapping
{
    public static class Utils
    {
        public static long ToLong(this HexBigInteger val)
        {
            return val == null ? 0 :
                val.Value > long.MaxValue ? long.MaxValue :
                val.Value < long.MinValue ? long.MinValue :
                (long) val.Value;
        }
    }
}
