using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts.CQS;
using Nethereum.Contracts;
using System.Threading;
using Nethereum.AccountAbstraction.Contracts.Interfaces.ITokenPaymaster.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Interfaces.ITokenPaymaster.ContractDefinition
{


    public partial class ITokenPaymasterDeployment : ITokenPaymasterDeploymentBase
    {
        public ITokenPaymasterDeployment() : base(BYTECODE) { }
        public ITokenPaymasterDeployment(string byteCode) : base(byteCode) { }
    }

    public class ITokenPaymasterDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x";
        public ITokenPaymasterDeploymentBase() : base(BYTECODE) { }
        public ITokenPaymasterDeploymentBase(string byteCode) : base(byteCode) { }

    }

    public partial class PostOpFunction : PostOpFunctionBase { }

    [Function("postOp")]
    public class PostOpFunctionBase : FunctionMessage
    {
        [Parameter("uint8", "mode", 1)]
        public virtual byte Mode { get; set; }
        [Parameter("bytes", "context", 2)]
        public virtual byte[] Context { get; set; }
        [Parameter("uint256", "actualGasCost", 3)]
        public virtual BigInteger ActualGasCost { get; set; }
        [Parameter("uint256", "actualUserOpFeePerGas", 4)]
        public virtual BigInteger ActualUserOpFeePerGas { get; set; }
    }

    public partial class PriceMarkupFunction : PriceMarkupFunctionBase { }

    [Function("priceMarkup", "uint256")]
    public class PriceMarkupFunctionBase : FunctionMessage
    {

    }

    public partial class PriceOracleFunction : PriceOracleFunctionBase { }

    [Function("priceOracle", "address")]
    public class PriceOracleFunctionBase : FunctionMessage
    {

    }

    public partial class SetPriceMarkupFunction : SetPriceMarkupFunctionBase { }

    [Function("setPriceMarkup")]
    public class SetPriceMarkupFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "markup", 1)]
        public virtual BigInteger Markup { get; set; }
    }

    public partial class TokenFunction : TokenFunctionBase { }

    [Function("token", "address")]
    public class TokenFunctionBase : FunctionMessage
    {

    }

    public partial class ValidatePaymasterUserOpFunction : ValidatePaymasterUserOpFunctionBase { }

    [Function("validatePaymasterUserOp", typeof(ValidatePaymasterUserOpOutputDTO))]
    public class ValidatePaymasterUserOpFunctionBase : FunctionMessage
    {
        [Parameter("tuple", "userOp", 1)]
        public virtual PackedUserOperation UserOp { get; set; }
        [Parameter("bytes32", "userOpHash", 2)]
        public virtual byte[] UserOpHash { get; set; }
        [Parameter("uint256", "maxCost", 3)]
        public virtual BigInteger MaxCost { get; set; }
    }



    public partial class PriceMarkupOutputDTO : PriceMarkupOutputDTOBase { }

    [FunctionOutput]
    public class PriceMarkupOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class PriceOracleOutputDTO : PriceOracleOutputDTOBase { }

    [FunctionOutput]
    public class PriceOracleOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }



    public partial class TokenOutputDTO : TokenOutputDTOBase { }

    [FunctionOutput]
    public class TokenOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class ValidatePaymasterUserOpOutputDTO : ValidatePaymasterUserOpOutputDTOBase { }

    [FunctionOutput]
    public class ValidatePaymasterUserOpOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes", "context", 1)]
        public virtual byte[] Context { get; set; }
        [Parameter("uint256", "validationData", 2)]
        public virtual BigInteger ValidationData { get; set; }
    }

    public partial class TokenPaymentEventDTO : TokenPaymentEventDTOBase { }

    [Event("TokenPayment")]
    public class TokenPaymentEventDTOBase : IEventDTO
    {
        [Parameter("address", "sender", 1, true )]
        public virtual string Sender { get; set; }
        [Parameter("address", "token", 2, true )]
        public virtual string Token { get; set; }
        [Parameter("uint256", "amount", 3, false )]
        public virtual BigInteger Amount { get; set; }
    }
}
