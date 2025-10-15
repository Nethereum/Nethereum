using Nethereum.ABI;
using Nethereum.Uniswap.V4.PositionManager.ContractDefinition;
using Nethereum.Util;

namespace Nethereum.Uniswap.V4
{
    public static class PoolKeyExtensions
    {
        public static byte[] EncodePoolKey(this PoolKey poolKey)
        {
            var hooks = string.IsNullOrEmpty(poolKey.Hooks) ? AddressUtil.ZERO_ADDRESS : poolKey.Hooks;

            var abiEncode = new ABIEncode();
            return abiEncode.GetABIEncoded(
                new ABIValue("address", poolKey.Currency0),
                new ABIValue("address", poolKey.Currency1),
                new ABIValue("uint24", poolKey.Fee),
                new ABIValue("int24", poolKey.TickSpacing),
                new ABIValue("address", hooks));
        }
    }
}
