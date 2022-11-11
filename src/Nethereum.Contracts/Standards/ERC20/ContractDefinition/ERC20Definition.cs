using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.Contracts.Standards.ERC20.ContractDefinition
{
    public partial class NameFunction : NameFunctionBase
    {
    }

    [Function("name", "string")]
    public class NameFunctionBase : FunctionMessage
    {
    }

    public partial class ApproveFunction : ApproveFunctionBase
    {
    }

    [Function("approve", "bool")]
    public class ApproveFunctionBase : FunctionMessage
    {
        [Parameter("address", "_spender", 1)]
        public virtual string Spender { get; set; }

        [Parameter("uint256", "_value", 2)]
        public virtual BigInteger Value { get; set; }
    }

    public partial class TotalSupplyFunction : TotalSupplyFunctionBase
    {
    }

    [Function("totalSupply", "uint256")]
    public class TotalSupplyFunctionBase : FunctionMessage
    {
    }

    public partial class TransferFromFunction : TransferFromFunctionBase
    {
    }

    [Function("transferFrom", "bool")]
    public class TransferFromFunctionBase : FunctionMessage
    {
        [Parameter("address", "_from", 1)]
        public virtual string From { get; set; }

        [Parameter("address", "_to", 2)]
        public virtual string To { get; set; }

        [Parameter("uint256", "_value", 3)]
        public virtual BigInteger Value { get; set; }
    }

    public partial class BalancesFunction : BalancesFunctionBase
    {
    }

    [Function("balances", "uint256")]
    public class BalancesFunctionBase : FunctionMessage
    {
        [Parameter("address", "", 1)]
        public virtual string Address { get; set; }
    }

    public partial class DecimalsFunction : DecimalsFunctionBase
    {
    }

    [Function("decimals", "uint8")]
    public class DecimalsFunctionBase : FunctionMessage
    {
    }

    public partial class AllowedFunction : AllowedFunctionBase
    {
    }

    [Function("allowed", "uint256")]
    public class AllowedFunctionBase : FunctionMessage
    {
        [Parameter("address", "", 1)]
        public virtual string Owner { get; set; }

        [Parameter("address", "", 2)]
        public virtual string Spender { get; set; }
    }

    public partial class BalanceOfFunction : BalanceOfFunctionBase
    {
    }

    [Function("balanceOf", "uint256")]
    public class BalanceOfFunctionBase : FunctionMessage
    {
        [Parameter("address", "_owner", 1)]
        public virtual string Owner { get; set; }
    }

    public partial class SymbolFunction : SymbolFunctionBase
    {
    }

    [Function("symbol", "string")]
    public class SymbolFunctionBase : FunctionMessage
    {
    }

    public partial class TransferFunction : TransferFunctionBase
    {
    }

    [Function("transfer", "bool")]
    public class TransferFunctionBase : FunctionMessage
    {
        [Parameter("address", "_to", 1)]
        public virtual string To { get; set; }

        [Parameter("uint256", "_value", 2)]
        public virtual BigInteger Value { get; set; }
    }

    public partial class AllowanceFunction : AllowanceFunctionBase
    {
    }

    [Function("allowance", "uint256")]
    public class AllowanceFunctionBase : FunctionMessage
    {
        [Parameter("address", "_owner", 1)]
        public virtual string Owner { get; set; }

        [Parameter("address", "_spender", 2)]
        public virtual string Spender { get; set; }
    }

    public partial class TransferEventDTO : TransferEventDTOBase
    {
    }

    [Event("Transfer")]
    public class TransferEventDTOBase : IEventDTO
    {
        [Parameter("address", "_from", 1, true)]
        public virtual string From { get; set; }

        [Parameter("address", "_to", 2, true)]
        public virtual string To { get; set; }

        [Parameter("uint256", "_value", 3, false)]
        public virtual BigInteger Value { get; set; }
    }

    public partial class ApprovalEventDTO : ApprovalEventDTOBase
    {
    }

    [Event("Approval")]
    public class ApprovalEventDTOBase : IEventDTO
    {
        [Parameter("address", "_owner", 1, true)]
        public virtual string Owner { get; set; }

        [Parameter("address", "_spender", 2, true)]
        public virtual string Spender { get; set; }

        [Parameter("uint256", "_value", 3, false)]
        public virtual BigInteger Value { get; set; }
    }

    public partial class NameOutputDTO : NameOutputDTOBase
    {
    }

    [FunctionOutput]
    public class NameOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("string", "", 1)]
        public virtual string Name { get; set; }
    }


    public partial class TotalSupplyOutputDTO : TotalSupplyOutputDTOBase
    {
    }

    [FunctionOutput]
    public class TotalSupplyOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger TotalSupply { get; set; }
    }


    public partial class BalancesOutputDTO : BalancesOutputDTOBase
    {
    }

    [FunctionOutput]
    public class BalancesOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger Balance { get; set; }
    }

    public partial class DecimalsOutputDTO : DecimalsOutputDTOBase
    {
    }

    [FunctionOutput]
    public class DecimalsOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint8", "", 1)]
        public virtual byte Decimals { get; set; }
    }

    public partial class AllowedOutputDTO : AllowedOutputDTOBase
    {
    }

    [FunctionOutput]
    public class AllowedOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger Amount { get; set; }
    }

    public partial class BalanceOfOutputDTO : BalanceOfOutputDTOBase
    {
    }

    [FunctionOutput]
    public class BalanceOfOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint256", "balance", 1)]
        public virtual BigInteger Balance { get; set; }
    }

    public partial class SymbolOutputDTO : SymbolOutputDTOBase
    {
    }

    [FunctionOutput]
    public class SymbolOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("string", "", 1)]
        public virtual string Symbol { get; set; }
    }

    public partial class AllowanceOutputDTO : AllowanceOutputDTOBase
    {
    }

    [FunctionOutput]
    public class AllowanceOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint256", "remaining", 1)]
        public virtual BigInteger Remaining { get; set; }
    }
}