using System.Collections.Generic;
using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.Uniswap.V4.Positions.PositionManager.ContractDefinition
{
    public partial class PermitBatch : PermitBatchBase { }

    public class PermitBatchBase 
    {
        [Parameter("tuple[]", "details", 1)]
        public virtual List<PermitDetails> Details { get; set; }
        [Parameter("address", "spender", 2)]
        public virtual string Spender { get; set; }
        [Parameter("uint256", "sigDeadline", 3)]
        public virtual BigInteger SigDeadline { get; set; }
    }
}
