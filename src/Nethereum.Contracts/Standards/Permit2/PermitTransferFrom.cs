using Nethereum.ABI.EIP712.Permit2;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.Contracts.Standards.Permit2
{
    [Struct("PermitTransferFrom")]
    public partial class PermitTransferFrom : PermitTransferFromBase {

        [Parameter("tuple", "permitted", 1, "TokenPermissions")]
        public override TokenPermissions Permitted { get; set; }
    }
}
