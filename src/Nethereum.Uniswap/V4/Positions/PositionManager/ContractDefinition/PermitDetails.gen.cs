using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.Uniswap.V4.Positions.PositionManager.ContractDefinition
{
    public partial class PermitDetails : PermitDetailsBase { }

    public class PermitDetailsBase 
    {
        [Parameter("address", "token", 1)]
        public virtual string Token { get; set; }
        [Parameter("uint160", "amount", 2)]
        public virtual BigInteger Amount { get; set; }
        [Parameter("uint48", "expiration", 3)]
        public virtual ulong Expiration { get; set; }
        [Parameter("uint48", "nonce", 4)]
        public virtual ulong Nonce { get; set; }
    }
}
