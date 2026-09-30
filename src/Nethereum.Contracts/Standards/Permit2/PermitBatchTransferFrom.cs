using System.Collections.Generic;
using Nethereum.ABI.EIP712.Permit2;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.Contracts.Standards.Permit2
{
    [Struct("PermitBatchTransferFrom")]
    public partial class PermitBatchTransferFrom : PermitBatchTransferFromBase {

        [Parameter("tuple[]", "permitted", 1, "TokenPermissions[]")]
        public new virtual List<TokenPermissions> Permitted { get; set; }

    }
}
