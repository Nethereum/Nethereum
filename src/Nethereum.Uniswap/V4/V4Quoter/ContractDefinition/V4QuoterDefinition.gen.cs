using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;

namespace Nethereum.Uniswap.V4.V4Quoter.ContractDefinition
{


    public partial class V4QuoterDeployment : V4QuoterDeploymentBase
    {
        public V4QuoterDeployment() : base(BYTECODE) { }
        public V4QuoterDeployment(string byteCode) : base(byteCode) { }
    }

    public class V4QuoterDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x";
        public V4QuoterDeploymentBase() : base(BYTECODE) { }
        public V4QuoterDeploymentBase(string byteCode) : base(byteCode) { }
        [Parameter("address", "_poolManager", 1)]
        public virtual string PoolManager { get; set; }
    }

    public partial class QuoteExactInputFunction : QuoteExactInputFunctionBase { }

    [Function("quoteExactInput", typeof(QuoteExactInputOutputDTO))]
    public class QuoteExactInputFunctionBase : FunctionMessage
    {
        [Parameter("tuple", "params", 1)]
        public virtual QuoteExactParams Params { get; set; }
    }

    public partial class QuoteExactInputSingleFunction : QuoteExactInputSingleFunctionBase { }

    [Function("quoteExactInputSingle", typeof(QuoteExactInputSingleOutputDTO))]
    public class QuoteExactInputSingleFunctionBase : FunctionMessage
    {
        [Parameter("tuple", "params", 1)]
        public virtual QuoteExactSingleParams Params { get; set; }
    }

    public partial class QuoteExactOutputFunction : QuoteExactOutputFunctionBase { }

    [Function("quoteExactOutput", typeof(QuoteExactOutputOutputDTO))]
    public class QuoteExactOutputFunctionBase : FunctionMessage
    {
        [Parameter("tuple", "params", 1)]
        public virtual QuoteExactParams Params { get; set; }
    }

    public partial class QuoteExactOutputSingleFunction : QuoteExactOutputSingleFunctionBase { }

    [Function("quoteExactOutputSingle", typeof(QuoteExactOutputSingleOutputDTO))]
    public class QuoteExactOutputSingleFunctionBase : FunctionMessage
    {
        [Parameter("tuple", "params", 1)]
        public virtual QuoteExactSingleParams Params { get; set; }
    }

    public partial class UnlockCallbackFunction : UnlockCallbackFunctionBase { }

    [Function("unlockCallback", "bytes")]
    public class UnlockCallbackFunctionBase : FunctionMessage
    {
        [Parameter("bytes", "data", 1)]
        public virtual byte[] Data { get; set; }
    }

    public partial class NotEnoughLiquidityError : NotEnoughLiquidityErrorBase { }

    [Error("NotEnoughLiquidity")]
    public class NotEnoughLiquidityErrorBase : IErrorDTO
    {
        [Parameter("bytes32", "poolId", 1)]
        public virtual byte[] PoolId { get; set; }
    }

    public partial class NotPoolManagerError : NotPoolManagerErrorBase { }
    [Error("NotPoolManager")]
    public class NotPoolManagerErrorBase : IErrorDTO
    {
    }

    public partial class NotSelfError : NotSelfErrorBase { }
    [Error("NotSelf")]
    public class NotSelfErrorBase : IErrorDTO
    {
    }

    public partial class QuoteSwapError : QuoteSwapErrorBase { }

    [Error("QuoteSwap")]
    public class QuoteSwapErrorBase : IErrorDTO
    {
        [Parameter("uint256", "amount", 1)]
        public virtual BigInteger Amount { get; set; }
    }

    public partial class UnexpectedCallSuccessError : UnexpectedCallSuccessErrorBase { }
    [Error("UnexpectedCallSuccess")]
    public class UnexpectedCallSuccessErrorBase : IErrorDTO
    {
    }

    public partial class UnexpectedRevertBytesError : UnexpectedRevertBytesErrorBase { }

    [Error("UnexpectedRevertBytes")]
    public class UnexpectedRevertBytesErrorBase : IErrorDTO
    {
        [Parameter("bytes", "revertData", 1)]
        public virtual byte[] RevertData { get; set; }
    }

    public partial class QuoteExactInputOutputDTO : QuoteExactInputOutputDTOBase { }

    [FunctionOutput]
    public class QuoteExactInputOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "amountOut", 1)]
        public virtual BigInteger AmountOut { get; set; }
        [Parameter("uint256", "gasEstimate", 2)]
        public virtual BigInteger GasEstimate { get; set; }
    }

    public partial class QuoteExactInputSingleOutputDTO : QuoteExactInputSingleOutputDTOBase { }

    [FunctionOutput]
    public class QuoteExactInputSingleOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "amountOut", 1)]
        public virtual BigInteger AmountOut { get; set; }
        [Parameter("uint256", "gasEstimate", 2)]
        public virtual BigInteger GasEstimate { get; set; }
    }

    public partial class QuoteExactOutputOutputDTO : QuoteExactOutputOutputDTOBase { }

    [FunctionOutput]
    public class QuoteExactOutputOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "amountIn", 1)]
        public virtual BigInteger AmountIn { get; set; }
        [Parameter("uint256", "gasEstimate", 2)]
        public virtual BigInteger GasEstimate { get; set; }
    }

    public partial class QuoteExactOutputSingleOutputDTO : QuoteExactOutputSingleOutputDTOBase { }

    [FunctionOutput]
    public class QuoteExactOutputSingleOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "amountIn", 1)]
        public virtual BigInteger AmountIn { get; set; }
        [Parameter("uint256", "gasEstimate", 2)]
        public virtual BigInteger GasEstimate { get; set; }
    }


}
