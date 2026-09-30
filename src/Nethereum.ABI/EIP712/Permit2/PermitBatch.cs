using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Collections.Generic;
using System.Numerics;

namespace Nethereum.ABI.EIP712.Permit2
{
    [Struct("PermitBatch")]
    public partial class PermitBatch
    {
        [Parameter("tuple[]", "details", 1, "PermitDetails[]")]
        public List<PermitDetails> Details { get; set; }
        [Parameter("address", "spender", 2)]
        public virtual string Spender { get; set; }
        [Parameter("uint256", "sigDeadline", 3)]
        public virtual BigInteger SigDeadline { get; set; }
    }
}
