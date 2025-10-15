using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.Uniswap.Core.Permit2.ContractDefinition
{
    [Struct("PermitTransferFrom")]
    public partial class PermitTransferFrom : PermitTransferFromBase {

        [Parameter("tuple", "permitted", 1, "TokenPermissions")]
        public override TokenPermissions Permitted { get; set; }
    }
}
