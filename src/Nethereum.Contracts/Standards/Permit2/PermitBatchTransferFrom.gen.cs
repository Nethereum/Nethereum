using System.Collections.Generic;
using System.Numerics;
using Nethereum.ABI.EIP712.Permit2;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.Contracts.Standards.Permit2
{
    public partial class PermitBatchTransferFrom : PermitBatchTransferFromBase { }

    public class PermitBatchTransferFromBase
    {
        [Parameter("tuple[]", "permitted", 1)]
        public virtual List<TokenPermissions> Permitted { get; set; }
        [Parameter("uint256", "nonce", 2)]
        public virtual BigInteger Nonce { get; set; }
        [Parameter("uint256", "deadline", 3)]
        public virtual BigInteger Deadline { get; set; }
    }
}
