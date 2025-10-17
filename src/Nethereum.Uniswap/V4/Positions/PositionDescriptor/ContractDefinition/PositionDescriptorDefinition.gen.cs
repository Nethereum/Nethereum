using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;

namespace Nethereum.Uniswap.V4.Positions.PositionDescriptor.ContractDefinition
{


    public partial class PositionDescriptorDeployment : PositionDescriptorDeploymentBase
    {
        public PositionDescriptorDeployment() : base(BYTECODE) { }
        public PositionDescriptorDeployment(string byteCode) : base(byteCode) { }
    }

    public class PositionDescriptorDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x";
        public PositionDescriptorDeploymentBase() : base(BYTECODE) { }
        public PositionDescriptorDeploymentBase(string byteCode) : base(byteCode) { }
        [Parameter("address", "_poolManager", 1)]
        public virtual string PoolManager { get; set; }
        [Parameter("address", "_wrappedNative", 2)]
        public virtual string WrappedNative { get; set; }
        [Parameter("bytes32", "_nativeCurrencyLabelBytes", 3)]
        public virtual byte[] NativeCurrencyLabelBytes { get; set; }
    }

    public partial class CurrencyRatioPriorityFunction : CurrencyRatioPriorityFunctionBase { }

    [Function("currencyRatioPriority", "int256")]
    public class CurrencyRatioPriorityFunctionBase : FunctionMessage
    {
        [Parameter("address", "currency", 1)]
        public virtual string Currency { get; set; }
    }

    public partial class FlipRatioFunction : FlipRatioFunctionBase { }

    [Function("flipRatio", "bool")]
    public class FlipRatioFunctionBase : FunctionMessage
    {
        [Parameter("address", "currency0", 1)]
        public virtual string Currency0 { get; set; }
        [Parameter("address", "currency1", 2)]
        public virtual string Currency1 { get; set; }
    }

    public partial class NativeCurrencyLabelFunction : NativeCurrencyLabelFunctionBase { }

    [Function("nativeCurrencyLabel", "string")]
    public class NativeCurrencyLabelFunctionBase : FunctionMessage
    {

    }

    public partial class PoolManagerFunction : PoolManagerFunctionBase { }

    [Function("poolManager", "address")]
    public class PoolManagerFunctionBase : FunctionMessage
    {

    }

    public partial class TokenURIFunction : TokenURIFunctionBase { }

    [Function("tokenURI", "string")]
    public class TokenURIFunctionBase : FunctionMessage
    {
        [Parameter("address", "positionManager", 1)]
        public virtual string PositionManager { get; set; }
        [Parameter("uint256", "tokenId", 2)]
        public virtual BigInteger TokenId { get; set; }
    }

    public partial class WrappedNativeFunction : WrappedNativeFunctionBase { }

    [Function("wrappedNative", "address")]
    public class WrappedNativeFunctionBase : FunctionMessage
    {

    }

    public partial class InvalidAddressLengthError : InvalidAddressLengthErrorBase { }

    [Error("InvalidAddressLength")]
    public class InvalidAddressLengthErrorBase : IErrorDTO
    {
        [Parameter("uint256", "len", 1)]
        public virtual BigInteger Len { get; set; }
    }

    public partial class InvalidTokenIdError : InvalidTokenIdErrorBase { }

    [Error("InvalidTokenId")]
    public class InvalidTokenIdErrorBase : IErrorDTO
    {
        [Parameter("uint256", "tokenId", 1)]
        public virtual BigInteger TokenId { get; set; }
    }

    public partial class StringsInsufficientHexLengthError : StringsInsufficientHexLengthErrorBase { }

    [Error("StringsInsufficientHexLength")]
    public class StringsInsufficientHexLengthErrorBase : IErrorDTO
    {
        [Parameter("uint256", "value", 1)]
        public virtual BigInteger Value { get; set; }
        [Parameter("uint256", "length", 2)]
        public virtual BigInteger Length { get; set; }
    }

    public partial class CurrencyRatioPriorityOutputDTO : CurrencyRatioPriorityOutputDTOBase { }

    [FunctionOutput]
    public class CurrencyRatioPriorityOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("int256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class FlipRatioOutputDTO : FlipRatioOutputDTOBase { }

    [FunctionOutput]
    public class FlipRatioOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class NativeCurrencyLabelOutputDTO : NativeCurrencyLabelOutputDTOBase { }

    [FunctionOutput]
    public class NativeCurrencyLabelOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("string", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class PoolManagerOutputDTO : PoolManagerOutputDTOBase { }

    [FunctionOutput]
    public class PoolManagerOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class TokenURIOutputDTO : TokenURIOutputDTOBase { }

    [FunctionOutput]
    public class TokenURIOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("string", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class WrappedNativeOutputDTO : WrappedNativeOutputDTOBase { }

    [FunctionOutput]
    public class WrappedNativeOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }
}
