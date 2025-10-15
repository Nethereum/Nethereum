using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.Uniswap.Permit2.ContractDefinition
{
    public partial class AllowanceTransferDetails : AllowanceTransferDetailsBase { }

    public class AllowanceTransferDetailsBase 
    {
        [Parameter("address", "from", 1)]
        public virtual string From { get; set; }
        [Parameter("address", "to", 2)]
        public virtual string To { get; set; }
        [Parameter("uint160", "amount", 3)]
        public virtual BigInteger Amount { get; set; }
        [Parameter("address", "token", 4)]
        public virtual string Token { get; set; }
    }
}
