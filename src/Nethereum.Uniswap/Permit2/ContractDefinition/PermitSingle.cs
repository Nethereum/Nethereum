using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.Uniswap.Core.Permit2.ContractDefinition
{
    [Struct("PermitSingle")]
    public partial class PermitSingle : PermitSingleBase {

        [Parameter("tuple", "details", 1, "PermitDetails")]
        public override PermitDetails Details { get; set; }
       
    }
}
