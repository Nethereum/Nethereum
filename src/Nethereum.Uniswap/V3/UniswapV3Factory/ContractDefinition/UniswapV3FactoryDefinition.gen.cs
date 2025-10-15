using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;

namespace Nethereum.Uniswap.V3.UniswapV3Factory.ContractDefinition
{


    public partial class UniswapV3FactoryDeployment : UniswapV3FactoryDeploymentBase
    {
        public UniswapV3FactoryDeployment() : base(BYTECODE) { }
        public UniswapV3FactoryDeployment(string byteCode) : base(byteCode) { }
    }

    public class UniswapV3FactoryDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x";
        public UniswapV3FactoryDeploymentBase() : base(BYTECODE) { }
        public UniswapV3FactoryDeploymentBase(string byteCode) : base(byteCode) { }

    }

    public partial class CreatePoolFunction : CreatePoolFunctionBase { }

    [Function("createPool", "address")]
    public class CreatePoolFunctionBase : FunctionMessage
    {
        [Parameter("address", "tokenA", 1)]
        public virtual string TokenA { get; set; }
        [Parameter("address", "tokenB", 2)]
        public virtual string TokenB { get; set; }
        [Parameter("uint24", "fee", 3)]
        public virtual uint Fee { get; set; }
    }

    public partial class EnableFeeAmountFunction : EnableFeeAmountFunctionBase { }

    [Function("enableFeeAmount")]
    public class EnableFeeAmountFunctionBase : FunctionMessage
    {
        [Parameter("uint24", "fee", 1)]
        public virtual uint Fee { get; set; }
        [Parameter("int24", "tickSpacing", 2)]
        public virtual int TickSpacing { get; set; }
    }

    public partial class FeeAmountTickSpacingFunction : FeeAmountTickSpacingFunctionBase { }

    [Function("feeAmountTickSpacing", "int24")]
    public class FeeAmountTickSpacingFunctionBase : FunctionMessage
    {
        [Parameter("uint24", "", 1)]
        public virtual uint ReturnValue1 { get; set; }
    }

    public partial class GetPoolFunction : GetPoolFunctionBase { }

    [Function("getPool", "address")]
    public class GetPoolFunctionBase : FunctionMessage
    {
        [Parameter("address", "token0", 1)]
        public virtual string Token0 { get; set; }
        [Parameter("address", "token1", 2)]
        public virtual string Token1 { get; set; }
        [Parameter("uint24", "fee", 3)]
        public virtual uint Fee { get; set; }
    }

    public partial class OwnerFunction : OwnerFunctionBase { }

    [Function("owner", "address")]
    public class OwnerFunctionBase : FunctionMessage
    {

    }

    public partial class ParametersFunction : ParametersFunctionBase { }

    [Function("parameters", typeof(ParametersOutputDTO))]
    public class ParametersFunctionBase : FunctionMessage
    {

    }

    public partial class SetOwnerFunction : SetOwnerFunctionBase { }

    [Function("setOwner")]
    public class SetOwnerFunctionBase : FunctionMessage
    {
        [Parameter("address", "_owner", 1)]
        public virtual string Owner { get; set; }
    }

    public partial class FeeAmountEnabledEventDTO : FeeAmountEnabledEventDTOBase { }

    [Event("FeeAmountEnabled")]
    public class FeeAmountEnabledEventDTOBase : IEventDTO
    {
        [Parameter("uint24", "fee", 1, true )]
        public virtual uint Fee { get; set; }
        [Parameter("int24", "tickSpacing", 2, true )]
        public virtual int TickSpacing { get; set; }
    }

    public partial class OwnerChangedEventDTO : OwnerChangedEventDTOBase { }

    [Event("OwnerChanged")]
    public class OwnerChangedEventDTOBase : IEventDTO
    {
        [Parameter("address", "oldOwner", 1, true )]
        public virtual string OldOwner { get; set; }
        [Parameter("address", "newOwner", 2, true )]
        public virtual string NewOwner { get; set; }
    }

    public partial class PoolCreatedEventDTO : PoolCreatedEventDTOBase { }

    [Event("PoolCreated")]
    public class PoolCreatedEventDTOBase : IEventDTO
    {
        [Parameter("address", "token0", 1, true )]
        public virtual string Token0 { get; set; }
        [Parameter("address", "token1", 2, true )]
        public virtual string Token1 { get; set; }
        [Parameter("uint24", "fee", 3, true )]
        public virtual uint Fee { get; set; }
        [Parameter("int24", "tickSpacing", 4, false )]
        public virtual int TickSpacing { get; set; }
        [Parameter("address", "pool", 5, false )]
        public virtual string Pool { get; set; }
    }





    public partial class FeeAmountTickSpacingOutputDTO : FeeAmountTickSpacingOutputDTOBase { }

    [FunctionOutput]
    public class FeeAmountTickSpacingOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("int24", "", 1)]
        public virtual int ReturnValue1 { get; set; }
    }

    public partial class GetPoolOutputDTO : GetPoolOutputDTOBase { }

    [FunctionOutput]
    public class GetPoolOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class OwnerOutputDTO : OwnerOutputDTOBase { }

    [FunctionOutput]
    public class OwnerOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class ParametersOutputDTO : ParametersOutputDTOBase { }

    [FunctionOutput]
    public class ParametersOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "factory", 1)]
        public virtual string Factory { get; set; }
        [Parameter("address", "token0", 2)]
        public virtual string Token0 { get; set; }
        [Parameter("address", "token1", 3)]
        public virtual string Token1 { get; set; }
        [Parameter("uint24", "fee", 4)]
        public virtual uint Fee { get; set; }
        [Parameter("int24", "tickSpacing", 5)]
        public virtual int TickSpacing { get; set; }
    }


}
