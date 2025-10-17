using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Collections.Generic;

namespace Nethereum.Uniswap.Core.Permit2.ContractDefinition
{
    [Struct("PermitBatchTransferFrom")]
    public partial class PermitBatchTransferFrom : PermitBatchTransferFromBase {

        [Parameter("tuple[]", "permitted", 1, "TokenPermissions[]")]
        public new virtual List<TokenPermissions> Permitted { get; set; }

    }
}
