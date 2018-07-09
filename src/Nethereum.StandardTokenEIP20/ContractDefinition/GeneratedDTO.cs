using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;

namespace Nethereum.StandardTokenEIP20.DTOs
{
    [FunctionOutput]
    public class AllowanceOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint256", "remaining", 1)]
        public BigInteger Remaining {get; set;}
    }

    public partial class AllowanceOutputDTO : AllowanceOutputDTOBase
    {

    }

    [FunctionOutput]
    public class AllowedOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint256", "", 1)]
        public BigInteger B { get; set; }
    }

    public partial class AllowedOutputDTO : AllowedOutputDTOBase
    {

    }

    [Event("Approval")]
    public class ApprovalEventDTOBase : IEventDTO
    {
        [Parameter("address", "_owner", 1, true)]
        public string Owner { get; set; }
        [Parameter("address", "_spender", 2, true)]
        public string Spender { get; set; }
        [Parameter("uint256", "_value", 3, false)]
        public BigInteger Value { get; set; }
    }

    public partial class ApprovalEventDTO : ApprovalEventDTOBase
    {

    }

    [FunctionOutput]
    public class BalanceOfOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint256", "balance", 1)]
        public BigInteger Balance { get; set; }
    }

    public partial class BalanceOfOutputDTO : BalanceOfOutputDTOBase
    {

    }

    [FunctionOutput]
    public class BalancesOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint256", "", 1)]
        public BigInteger B { get; set; }
    }


    public partial class BalancesOutputDTO : BalancesOutputDTOBase
    {

    }

    [FunctionOutput]
    public class DecimalsOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint8", "", 1)]
        public byte B { get; set; }
    }

    public partial class DecimalsOutputDTO : DecimalsOutputDTOBase
    {

    }

    [FunctionOutput]
    public class NameOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("string", "", 1)]
        public string B { get; set; }
    }

    public partial class NameOutputDTO : NameOutputDTOBase
    {

    }

    [FunctionOutput]
    public class SymbolOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("string", "", 1)]
        public string B { get; set; }
    }

    public partial class SymbolOutputDTO : SymbolOutputDTOBase
    {

    }

    [FunctionOutput]
    public class TotalSupplyOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint256", "", 1)]
        public BigInteger B { get; set; }
    }

    public partial class TotalSupplyOutputDTO : TotalSupplyOutputDTOBase
    {

    }

    [Event("Transfer")]
    public class TransferEventDTOBase : IEventDTO
    {
        [Parameter("address", "_from", 1, true)]
        public string From { get; set; }
        [Parameter("address", "_to", 2, true)]
        public string To { get; set; }
        [Parameter("uint256", "_value", 3, false)]
        public BigInteger Value { get; set; }
    }

    public partial class TransferEventDTO : TransferEventDTOBase
    {

    }
}
