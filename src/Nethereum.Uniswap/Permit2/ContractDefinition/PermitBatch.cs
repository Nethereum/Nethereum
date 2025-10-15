using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Collections.Generic;

namespace Nethereum.Uniswap.Core.Permit2.ContractDefinition
{
    [Struct("PermitBatch")]
    public partial class PermitBatch : PermitBatchBase {

        [Parameter("tuple[]", "details", 1, "PermitDetails[]")]
        public override List<PermitDetails> Details { get; set; }
    }
}
