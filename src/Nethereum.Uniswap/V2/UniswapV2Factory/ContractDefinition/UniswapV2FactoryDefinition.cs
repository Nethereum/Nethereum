using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;

namespace Nethereum.Uniswap.V2.UniswapV2Factory.ContractDefinition
{


    public partial class UniswapV2FactoryDeployment : UniswapV2FactoryDeploymentBase
    {
        public UniswapV2FactoryDeployment() : base(BYTECODE) { }
        public UniswapV2FactoryDeployment(string byteCode) : base(byteCode) { }
    }

    public class UniswapV2FactoryDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "";
        public UniswapV2FactoryDeploymentBase() : base(BYTECODE) { }
        public UniswapV2FactoryDeploymentBase(string byteCode) : base(byteCode) { }

    }

    public partial class AllPairsFunction : AllPairsFunctionBase { }

    [Function("allPairs", "address")]
    public class AllPairsFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class AllPairsLengthFunction : AllPairsLengthFunctionBase { }

    [Function("allPairsLength", "uint256")]
    public class AllPairsLengthFunctionBase : FunctionMessage
    {

    }

    public partial class CreatePairFunction : CreatePairFunctionBase { }

    [Function("createPair", "address")]
    public class CreatePairFunctionBase : FunctionMessage
    {
        [Parameter("address", "tokenA", 1)]
        public virtual string TokenA { get; set; }
        [Parameter("address", "tokenB", 2)]
        public virtual string TokenB { get; set; }
    }

    public partial class FeeToFunction : FeeToFunctionBase { }

    [Function("feeTo", "address")]
    public class FeeToFunctionBase : FunctionMessage
    {

    }

    public partial class FeeToSetterFunction : FeeToSetterFunctionBase { }

    [Function("feeToSetter", "address")]
    public class FeeToSetterFunctionBase : FunctionMessage
    {

    }

    public partial class GetPairFunction : GetPairFunctionBase { }

    [Function("getPair", "address")]
    public class GetPairFunctionBase : FunctionMessage
    {
        [Parameter("address", "tokenA", 1)]
        public virtual string TokenA { get; set; }
        [Parameter("address", "tokenB", 2)]
        public virtual string TokenB { get; set; }
    }

    public partial class PairCreatedEventDTO : PairCreatedEventDTOBase { }

    [Event("PairCreated")]
    public class PairCreatedEventDTOBase : IEventDTO
    {
        [Parameter("address", "token0", 1, true )]
        public virtual string Token0 { get; set; }
        [Parameter("address", "token1", 2, true )]
        public virtual string Token1 { get; set; }
        [Parameter("address", "pair", 3, false )]
        public virtual string Pair { get; set; }
        [Parameter("uint256", "", 4, false )]
        public virtual BigInteger ReturnValue4 { get; set; }
    }

    public partial class AllPairsOutputDTO : AllPairsOutputDTOBase { }

    [FunctionOutput]
    public class AllPairsOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "pair", 1)]
        public virtual string Pair { get; set; }
    }

    public partial class AllPairsLengthOutputDTO : AllPairsLengthOutputDTOBase { }

    [FunctionOutput]
    public class AllPairsLengthOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }



    public partial class FeeToOutputDTO : FeeToOutputDTOBase { }

    [FunctionOutput]
    public class FeeToOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class FeeToSetterOutputDTO : FeeToSetterOutputDTOBase { }

    [FunctionOutput]
    public class FeeToSetterOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class GetPairOutputDTO : GetPairOutputDTOBase { }

    [FunctionOutput]
    public class GetPairOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "pair", 1)]
        public virtual string Pair { get; set; }
    }
}
