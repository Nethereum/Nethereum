using System.Collections.Generic;
using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;

namespace Nethereum.Uniswap.V2.UniswapV2Router01.ContractDefinition
{


    public partial class UniswapV2Router01Deployment : UniswapV2Router01DeploymentBase
    {
        public UniswapV2Router01Deployment() : base(BYTECODE) { }
        public UniswapV2Router01Deployment(string byteCode) : base(byteCode) { }
    }

    public class UniswapV2Router01DeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "";
        public UniswapV2Router01DeploymentBase() : base(BYTECODE) { }
        public UniswapV2Router01DeploymentBase(string byteCode) : base(byteCode) { }

    }

    public partial class WETHFunction : WETHFunctionBase { }

    [Function("WETH", "address")]
    public class WETHFunctionBase : FunctionMessage
    {

    }

    public partial class AddLiquidityFunction : AddLiquidityFunctionBase { }

    [Function("addLiquidity", typeof(AddLiquidityOutputDTO))]
    public class AddLiquidityFunctionBase : FunctionMessage
    {
        [Parameter("address", "tokenA", 1)]
        public virtual string TokenA { get; set; }
        [Parameter("address", "tokenB", 2)]
        public virtual string TokenB { get; set; }
        [Parameter("uint256", "amountADesired", 3)]
        public virtual BigInteger AmountADesired { get; set; }
        [Parameter("uint256", "amountBDesired", 4)]
        public virtual BigInteger AmountBDesired { get; set; }
        [Parameter("uint256", "amountAMin", 5)]
        public virtual BigInteger AmountAMin { get; set; }
        [Parameter("uint256", "amountBMin", 6)]
        public virtual BigInteger AmountBMin { get; set; }
        [Parameter("address", "to", 7)]
        public virtual string To { get; set; }
        [Parameter("uint256", "deadline", 8)]
        public virtual BigInteger Deadline { get; set; }
    }

    public partial class AddLiquidityETHFunction : AddLiquidityETHFunctionBase { }

    [Function("addLiquidityETH", typeof(AddLiquidityETHOutputDTO))]
    public class AddLiquidityETHFunctionBase : FunctionMessage
    {
        [Parameter("address", "token", 1)]
        public virtual string Token { get; set; }
        [Parameter("uint256", "amountTokenDesired", 2)]
        public virtual BigInteger AmountTokenDesired { get; set; }
        [Parameter("uint256", "amountTokenMin", 3)]
        public virtual BigInteger AmountTokenMin { get; set; }
        [Parameter("uint256", "amountETHMin", 4)]
        public virtual BigInteger AmountETHMin { get; set; }
        [Parameter("address", "to", 5)]
        public virtual string To { get; set; }
        [Parameter("uint256", "deadline", 6)]
        public virtual BigInteger Deadline { get; set; }
    }

    public partial class FactoryFunction : FactoryFunctionBase { }

    [Function("factory", "address")]
    public class FactoryFunctionBase : FunctionMessage
    {

    }

    public partial class GetAmountInFunction : GetAmountInFunctionBase { }

    [Function("getAmountIn", "uint256")]
    public class GetAmountInFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "amountOut", 1)]
        public virtual BigInteger AmountOut { get; set; }
        [Parameter("uint256", "reserveIn", 2)]
        public virtual BigInteger ReserveIn { get; set; }
        [Parameter("uint256", "reserveOut", 3)]
        public virtual BigInteger ReserveOut { get; set; }
    }

    public partial class GetAmountOutFunction : GetAmountOutFunctionBase { }

    [Function("getAmountOut", "uint256")]
    public class GetAmountOutFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "amountIn", 1)]
        public virtual BigInteger AmountIn { get; set; }
        [Parameter("uint256", "reserveIn", 2)]
        public virtual BigInteger ReserveIn { get; set; }
        [Parameter("uint256", "reserveOut", 3)]
        public virtual BigInteger ReserveOut { get; set; }
    }

    public partial class GetAmountsInFunction : GetAmountsInFunctionBase { }

    [Function("getAmountsIn", "uint256[]")]
    public class GetAmountsInFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "amountOut", 1)]
        public virtual BigInteger AmountOut { get; set; }
        [Parameter("address[]", "path", 2)]
        public virtual List<string> Path { get; set; }
    }

    public partial class GetAmountsOutFunction : GetAmountsOutFunctionBase { }

    [Function("getAmountsOut", "uint256[]")]
    public class GetAmountsOutFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "amountIn", 1)]
        public virtual BigInteger AmountIn { get; set; }
        [Parameter("address[]", "path", 2)]
        public virtual List<string> Path { get; set; }
    }

    public partial class QuoteFunction : QuoteFunctionBase { }

    [Function("quote", "uint256")]
    public class QuoteFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "amountA", 1)]
        public virtual BigInteger AmountA { get; set; }
        [Parameter("uint256", "reserveA", 2)]
        public virtual BigInteger ReserveA { get; set; }
        [Parameter("uint256", "reserveB", 3)]
        public virtual BigInteger ReserveB { get; set; }
    }

    public partial class RemoveLiquidityFunction : RemoveLiquidityFunctionBase { }

    [Function("removeLiquidity", typeof(RemoveLiquidityOutputDTO))]
    public class RemoveLiquidityFunctionBase : FunctionMessage
    {
        [Parameter("address", "tokenA", 1)]
        public virtual string TokenA { get; set; }
        [Parameter("address", "tokenB", 2)]
        public virtual string TokenB { get; set; }
        [Parameter("uint256", "liquidity", 3)]
        public virtual BigInteger Liquidity { get; set; }
        [Parameter("uint256", "amountAMin", 4)]
        public virtual BigInteger AmountAMin { get; set; }
        [Parameter("uint256", "amountBMin", 5)]
        public virtual BigInteger AmountBMin { get; set; }
        [Parameter("address", "to", 6)]
        public virtual string To { get; set; }
        [Parameter("uint256", "deadline", 7)]
        public virtual BigInteger Deadline { get; set; }
    }

    public partial class RemoveLiquidityETHFunction : RemoveLiquidityETHFunctionBase { }

    [Function("removeLiquidityETH", typeof(RemoveLiquidityETHOutputDTO))]
    public class RemoveLiquidityETHFunctionBase : FunctionMessage
    {
        [Parameter("address", "token", 1)]
        public virtual string Token { get; set; }
        [Parameter("uint256", "liquidity", 2)]
        public virtual BigInteger Liquidity { get; set; }
        [Parameter("uint256", "amountTokenMin", 3)]
        public virtual BigInteger AmountTokenMin { get; set; }
        [Parameter("uint256", "amountETHMin", 4)]
        public virtual BigInteger AmountETHMin { get; set; }
        [Parameter("address", "to", 5)]
        public virtual string To { get; set; }
        [Parameter("uint256", "deadline", 6)]
        public virtual BigInteger Deadline { get; set; }
    }

    public partial class RemoveLiquidityETHWithPermitFunction : RemoveLiquidityETHWithPermitFunctionBase { }

    [Function("removeLiquidityETHWithPermit", typeof(RemoveLiquidityETHWithPermitOutputDTO))]
    public class RemoveLiquidityETHWithPermitFunctionBase : FunctionMessage
    {
        [Parameter("address", "token", 1)]
        public virtual string Token { get; set; }
        [Parameter("uint256", "liquidity", 2)]
        public virtual BigInteger Liquidity { get; set; }
        [Parameter("uint256", "amountTokenMin", 3)]
        public virtual BigInteger AmountTokenMin { get; set; }
        [Parameter("uint256", "amountETHMin", 4)]
        public virtual BigInteger AmountETHMin { get; set; }
        [Parameter("address", "to", 5)]
        public virtual string To { get; set; }
        [Parameter("uint256", "deadline", 6)]
        public virtual BigInteger Deadline { get; set; }
        [Parameter("bool", "approveMax", 7)]
        public virtual bool ApproveMax { get; set; }
        [Parameter("uint8", "v", 8)]
        public virtual byte V { get; set; }
        [Parameter("bytes32", "r", 9)]
        public virtual byte[] R { get; set; }
        [Parameter("bytes32", "s", 10)]
        public virtual byte[] S { get; set; }
    }

    public partial class RemoveLiquidityWithPermitFunction : RemoveLiquidityWithPermitFunctionBase { }

    [Function("removeLiquidityWithPermit", typeof(RemoveLiquidityWithPermitOutputDTO))]
    public class RemoveLiquidityWithPermitFunctionBase : FunctionMessage
    {
        [Parameter("address", "tokenA", 1)]
        public virtual string TokenA { get; set; }
        [Parameter("address", "tokenB", 2)]
        public virtual string TokenB { get; set; }
        [Parameter("uint256", "liquidity", 3)]
        public virtual BigInteger Liquidity { get; set; }
        [Parameter("uint256", "amountAMin", 4)]
        public virtual BigInteger AmountAMin { get; set; }
        [Parameter("uint256", "amountBMin", 5)]
        public virtual BigInteger AmountBMin { get; set; }
        [Parameter("address", "to", 6)]
        public virtual string To { get; set; }
        [Parameter("uint256", "deadline", 7)]
        public virtual BigInteger Deadline { get; set; }
        [Parameter("bool", "approveMax", 8)]
        public virtual bool ApproveMax { get; set; }
        [Parameter("uint8", "v", 9)]
        public virtual byte V { get; set; }
        [Parameter("bytes32", "r", 10)]
        public virtual byte[] R { get; set; }
        [Parameter("bytes32", "s", 11)]
        public virtual byte[] S { get; set; }
    }

    public partial class SwapETHForExactTokensFunction : SwapETHForExactTokensFunctionBase { }

    [Function("swapETHForExactTokens", "uint256[]")]
    public class SwapETHForExactTokensFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "amountOut", 1)]
        public virtual BigInteger AmountOut { get; set; }
        [Parameter("address[]", "path", 2)]
        public virtual List<string> Path { get; set; }
        [Parameter("address", "to", 3)]
        public virtual string To { get; set; }
        [Parameter("uint256", "deadline", 4)]
        public virtual BigInteger Deadline { get; set; }
    }

    public partial class SwapExactETHForTokensFunction : SwapExactETHForTokensFunctionBase { }

    [Function("swapExactETHForTokens", "uint256[]")]
    public class SwapExactETHForTokensFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "amountOutMin", 1)]
        public virtual BigInteger AmountOutMin { get; set; }
        [Parameter("address[]", "path", 2)]
        public virtual List<string> Path { get; set; }
        [Parameter("address", "to", 3)]
        public virtual string To { get; set; }
        [Parameter("uint256", "deadline", 4)]
        public virtual BigInteger Deadline { get; set; }
    }

    public partial class SwapExactTokensForETHFunction : SwapExactTokensForETHFunctionBase { }

    [Function("swapExactTokensForETH", "uint256[]")]
    public class SwapExactTokensForETHFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "amountIn", 1)]
        public virtual BigInteger AmountIn { get; set; }
        [Parameter("uint256", "amountOutMin", 2)]
        public virtual BigInteger AmountOutMin { get; set; }
        [Parameter("address[]", "path", 3)]
        public virtual List<string> Path { get; set; }
        [Parameter("address", "to", 4)]
        public virtual string To { get; set; }
        [Parameter("uint256", "deadline", 5)]
        public virtual BigInteger Deadline { get; set; }
    }

    public partial class SwapExactTokensForTokensFunction : SwapExactTokensForTokensFunctionBase { }

    [Function("swapExactTokensForTokens", "uint256[]")]
    public class SwapExactTokensForTokensFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "amountIn", 1)]
        public virtual BigInteger AmountIn { get; set; }
        [Parameter("uint256", "amountOutMin", 2)]
        public virtual BigInteger AmountOutMin { get; set; }
        [Parameter("address[]", "path", 3)]
        public virtual List<string> Path { get; set; }
        [Parameter("address", "to", 4)]
        public virtual string To { get; set; }
        [Parameter("uint256", "deadline", 5)]
        public virtual BigInteger Deadline { get; set; }
    }

    public partial class SwapTokensForExactETHFunction : SwapTokensForExactETHFunctionBase { }

    [Function("swapTokensForExactETH", "uint256[]")]
    public class SwapTokensForExactETHFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "amountOut", 1)]
        public virtual BigInteger AmountOut { get; set; }
        [Parameter("uint256", "amountInMax", 2)]
        public virtual BigInteger AmountInMax { get; set; }
        [Parameter("address[]", "path", 3)]
        public virtual List<string> Path { get; set; }
        [Parameter("address", "to", 4)]
        public virtual string To { get; set; }
        [Parameter("uint256", "deadline", 5)]
        public virtual BigInteger Deadline { get; set; }
    }

    public partial class SwapTokensForExactTokensFunction : SwapTokensForExactTokensFunctionBase { }

    [Function("swapTokensForExactTokens", "uint256[]")]
    public class SwapTokensForExactTokensFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "amountOut", 1)]
        public virtual BigInteger AmountOut { get; set; }
        [Parameter("uint256", "amountInMax", 2)]
        public virtual BigInteger AmountInMax { get; set; }
        [Parameter("address[]", "path", 3)]
        public virtual List<string> Path { get; set; }
        [Parameter("address", "to", 4)]
        public virtual string To { get; set; }
        [Parameter("uint256", "deadline", 5)]
        public virtual BigInteger Deadline { get; set; }
    }

    public partial class WETHOutputDTO : WETHOutputDTOBase { }

    [FunctionOutput]
    public class WETHOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class AddLiquidityOutputDTO : AddLiquidityOutputDTOBase { }

    [FunctionOutput]
    public class AddLiquidityOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "amountA", 1)]
        public virtual BigInteger AmountA { get; set; }
        [Parameter("uint256", "amountB", 2)]
        public virtual BigInteger AmountB { get; set; }
        [Parameter("uint256", "liquidity", 3)]
        public virtual BigInteger Liquidity { get; set; }
    }

    public partial class AddLiquidityETHOutputDTO : AddLiquidityETHOutputDTOBase { }

    [FunctionOutput]
    public class AddLiquidityETHOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "amountToken", 1)]
        public virtual BigInteger AmountToken { get; set; }
        [Parameter("uint256", "amountETH", 2)]
        public virtual BigInteger AmountETH { get; set; }
        [Parameter("uint256", "liquidity", 3)]
        public virtual BigInteger Liquidity { get; set; }
    }

    public partial class FactoryOutputDTO : FactoryOutputDTOBase { }

    [FunctionOutput]
    public class FactoryOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class GetAmountInOutputDTO : GetAmountInOutputDTOBase { }

    [FunctionOutput]
    public class GetAmountInOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "amountIn", 1)]
        public virtual BigInteger AmountIn { get; set; }
    }

    public partial class GetAmountOutOutputDTO : GetAmountOutOutputDTOBase { }

    [FunctionOutput]
    public class GetAmountOutOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "amountOut", 1)]
        public virtual BigInteger AmountOut { get; set; }
    }

    public partial class GetAmountsInOutputDTO : GetAmountsInOutputDTOBase { }

    [FunctionOutput]
    public class GetAmountsInOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256[]", "amounts", 1)]
        public virtual List<BigInteger> Amounts { get; set; }
    }

    public partial class GetAmountsOutOutputDTO : GetAmountsOutOutputDTOBase { }

    [FunctionOutput]
    public class GetAmountsOutOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256[]", "amounts", 1)]
        public virtual List<BigInteger> Amounts { get; set; }
    }

    public partial class QuoteOutputDTO : QuoteOutputDTOBase { }

    [FunctionOutput]
    public class QuoteOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "amountB", 1)]
        public virtual BigInteger AmountB { get; set; }
    }

    public partial class RemoveLiquidityOutputDTO : RemoveLiquidityOutputDTOBase { }

    [FunctionOutput]
    public class RemoveLiquidityOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "amountA", 1)]
        public virtual BigInteger AmountA { get; set; }
        [Parameter("uint256", "amountB", 2)]
        public virtual BigInteger AmountB { get; set; }
    }

    public partial class RemoveLiquidityETHOutputDTO : RemoveLiquidityETHOutputDTOBase { }

    [FunctionOutput]
    public class RemoveLiquidityETHOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "amountToken", 1)]
        public virtual BigInteger AmountToken { get; set; }
        [Parameter("uint256", "amountETH", 2)]
        public virtual BigInteger AmountETH { get; set; }
    }

    public partial class RemoveLiquidityETHWithPermitOutputDTO : RemoveLiquidityETHWithPermitOutputDTOBase { }

    [FunctionOutput]
    public class RemoveLiquidityETHWithPermitOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "amountToken", 1)]
        public virtual BigInteger AmountToken { get; set; }
        [Parameter("uint256", "amountETH", 2)]
        public virtual BigInteger AmountETH { get; set; }
    }

    public partial class RemoveLiquidityWithPermitOutputDTO : RemoveLiquidityWithPermitOutputDTOBase { }

    [FunctionOutput]
    public class RemoveLiquidityWithPermitOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "amountA", 1)]
        public virtual BigInteger AmountA { get; set; }
        [Parameter("uint256", "amountB", 2)]
        public virtual BigInteger AmountB { get; set; }
    }












}
