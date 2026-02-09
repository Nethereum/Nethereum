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
using Nethereum.AccountAbstraction.Contracts.Paymaster.TokenPaymaster.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Paymaster.TokenPaymaster.ContractDefinition
{


    public partial class TokenPaymasterDeployment : TokenPaymasterDeploymentBase
    {
        public TokenPaymasterDeployment() : base(BYTECODE) { }
        public TokenPaymasterDeployment(string byteCode) : base(byteCode) { }
    }

    public class TokenPaymasterDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x60a03461014b57601f610e3138819003918201601f19168301916001600160401b0383118484101761014f5780849260a09460405283398101031261014b5761004781610163565b9061005460208201610163565b9061006160408201610163565b608061006f60608401610163565b9201516001600160a01b03909316938415610138575f80546001600160a01b031981168717825560405196916001600160a01b03909116907f8be0079c531659141344cd1fd0a4f28419497f9722a3daafe3b4186f6b6457e09080a3608052600180546001600160a01b039283166001600160a01b03199182161790915560028054939092169216919091179055600355610cb99081610178823960805181818161030e015281816103650152818161044001528181610591015281816106830152610a9c0152f35b631e4fbdf760e01b5f525f60045260245ffd5b5f80fd5b634e487b7160e01b5f52604160045260245ffd5b51906001600160a01b038216820361014b5756fe60806040526004361015610022575b3615610018575f80fd5b610020610a9a565b005b5f5f3560e01c806306b091f914610774578063144fa6d71461070b578063205c28781461065f5780632630c12f146106365780633e04619d1461061857806352b7512c1461055c578063530e784f146104f1578063715018a6146104975780637c627b21146103e25780638da5cb5b146103bb57806397717d9014610394578063b0d691fe1461034f578063c399ec88146102e1578063d0dafe7e146102a7578063d0e30db014610290578063f1a640f814610274578063f2fde38b146101ee578063f751758b14610150578063fae96f33146101345763fc0c546a14610109575061000e565b346101315780600319360112610131576001546040516001600160a01b039091168152602090f35b80fd5b5034610131578060031936011261013157602060405160648152f35b50346101315780600319360112610131576002546001546040516341976e0960e01b81526001600160a01b039182166004820152929160209184916024918391165afa9081156101e257906101ab575b602090604051908152f35b506020813d6020116101da575b816101c5602093836107c4565b810103126101d657602090516101a0565b5f80fd5b3d91506101b8565b604051903d90823e3d90fd5b5034610131576020366003190112610131576102086107ae565b610210610b01565b6001600160a01b031680156102605781546001600160a01b03198116821783556001600160a01b03167f8be0079c531659141344cd1fd0a4f28419497f9722a3daafe3b4186f6b6457e08380a380f35b631e4fbdf760e01b82526004829052602482fd5b5034610131578060031936011261013157602060405160128152f35b5080600319360112610131576102a4610a9a565b80f35b5034610131576020366003190112610131576004356102c4610b01565b606481106102d25760035580f35b6399c3653960e01b8252600482fd5b50346101315780600319360112610131576040516370a0823160e01b8152306004820152906020826024817f00000000000000000000000000000000000000000000000000000000000000006001600160a01b03165afa9081156101e257906101ab57602090604051908152f35b50346101315780600319360112610131576040517f00000000000000000000000000000000000000000000000000000000000000006001600160a01b03168152602090f35b50346101315760203660031901126101315760206103b3600435610ba8565b604051908152f35b5034610131578060031936011261013157546040516001600160a01b039091168152602090f35b5034610131576080366003190112610131576004359060038210156101315760243567ffffffffffffffff8111610493573660238201121561049357806004013567ffffffffffffffff811161048f57366024828401011161048f577f00000000000000000000000000000000000000000000000000000000000000006001600160a01b03163303610480576102a4929360246044359301906109a9565b63bd07c55160e01b8352600483fd5b8280fd5b5080fd5b50346101315780600319360112610131576104b0610b01565b80546001600160a01b03198116825581906001600160a01b03167f8be0079c531659141344cd1fd0a4f28419497f9722a3daafe3b4186f6b6457e08280a380f35b50346101315760203660031901126101315761050b6107ae565b610513610b01565b600280546001600160a01b039283166001600160a01b0319821681179092559091167f05cd89403c6bdeac21c2ff33de395121a31fa1bc2bf3adf4825f1f86e79969dd8380a380f35b50346101315760603660031901126101315760043567ffffffffffffffff8111610493576101206003198236030112610493577f00000000000000000000000000000000000000000000000000000000000000006001600160a01b031633036106095760206105d3606092604435906004016107fa565b604092919251948593604085528051938491826040880152018686015e8383018501526020830152601f01601f19168101030190f35b63bd07c55160e01b8252600482fd5b50346101315780600319360112610131576020600354604051908152f35b50346101315780600319360112610131576002546040516001600160a01b039091168152602090f35b50346101d65760403660031901126101d6576106796107ae565b610681610b01565b7f00000000000000000000000000000000000000000000000000000000000000006001600160a01b031690813b156101d65760405163040b850f60e31b81526001600160a01b0390911660048201526024803590820152905f908290604490829084905af18015610700576106f4575080f35b61002091505f906107c4565b6040513d5f823e3d90fd5b346101d65760203660031901126101d6576107246107ae565b61072c610b01565b600180546001600160a01b039283166001600160a01b0319821681179092559091167fec507b76e4056f09193394a4361b44129ec561809ddee312c7f97121f93bb58b5f80a3005b346101d65760403660031901126101d6576100206107906107ae565b610798610b01565b60015460243591906001600160a01b0316610b27565b600435906001600160a01b03821682036101d657565b90601f8019910116810190811067ffffffffffffffff8211176107e657604052565b634e487b7160e01b5f52604160045260245ffd5b356001600160a01b03811691908290036101d65761081781610ba8565b600154604051636eb1769f60e11b8152600481018590523060248201526001600160a01b039091169390602081604481885afa80156107005783915f91610974575b5010610924576040516370a0823160e01b815260048101829052602081602481885afa80156107005783915f9161093f575b501061092457604051936323b872dd60e01b5f5281600452306024528260445260205f60648180855af160015f5114811615610905575b856040525f606052156108f35750602084015260408301526060820152606081526108ee6080826107c4565b905f90565b635274afe760e01b5f5260045260245ffd5b600181151661091b57813b15153d1516166108c2565b853d5f823e3d90fd5b505050506040516109366020826107c4565b5f815290600190565b9150506020813d60201161096c575b8161095b602093836107c4565b810103126101d6578290515f61088b565b3d915061094e565b9150506020813d6020116109a1575b81610990602093836107c4565b810103126101d6578290515f610859565b3d9150610983565b92909182606091810103126101d65781356001600160a01b03811692908390036101d65760200135926003811015610a8657600114610a6c576109eb90610ba8565b91828111610a30575b506001546040519283526001600160a01b0316917f4cf1c3e3dbc55728390d34fda81c947a86e0965de037bf5c1ef5fd6092e5fc3b90602090a3565b828103908111610a5857600154610a52919083906001600160a01b0316610b27565b5f6109f4565b634e487b7160e01b5f52601160045260245ffd5b50600154610a849291906001600160a01b0316610b27565b565b634e487b7160e01b5f52602160045260245ffd5b7f00000000000000000000000000000000000000000000000000000000000000006001600160a01b0316803b156101d6575f6024916040519283809263b760faf960e01b825230600483015234905af1801561070057610af75750565b5f610a84916107c4565b5f546001600160a01b03163303610b1457565b63118cdaa760e01b5f523360045260245ffd5b916040519163a9059cbb60e01b5f5260018060a01b031660045260245260205f60448180865af19060015f5114821615610b87575b60405215610b675750565b635274afe760e01b5f9081526001600160a01b0391909116600452602490fd5b906001811516610b9f57823b15153d15161690610b5c565b503d5f823e3d90fd5b6002546001546040516341976e0960e01b81526001600160a01b039182166004820152929160209184916024918391165afa918215610700575f92610c4f575b5060035490818102918183041490151715610a5857670de0b6b3a7640000810290808204670de0b6b3a76400001490151715610a5857606482029180830460641490151715610a58578115610c3b570490565b634e487b7160e01b5f52601260045260245ffd5b9091506020813d602011610c7b575b81610c6b602093836107c4565b810103126101d65751905f610be8565b3d9150610c5e56fea2646970667358221220592bc8376ada79ffd80ffc60b585230bffa10466cdccc221564238441c06a20a64736f6c634300081c0033";
        public TokenPaymasterDeploymentBase() : base(BYTECODE) { }
        public TokenPaymasterDeploymentBase(string byteCode) : base(byteCode) { }
        [Parameter("address", "_entryPoint", 1)]
        public virtual string EntryPoint { get; set; }
        [Parameter("address", "_owner", 2)]
        public virtual string Owner { get; set; }
        [Parameter("address", "tokenAddress", 3)]
        public virtual string TokenAddress { get; set; }
        [Parameter("address", "oracleAddress", 4)]
        public virtual string OracleAddress { get; set; }
        [Parameter("uint256", "markup", 5)]
        public virtual BigInteger Markup { get; set; }
    }

    public partial class MarkupDenominatorFunction : MarkupDenominatorFunctionBase { }

    [Function("MARKUP_DENOMINATOR", "uint256")]
    public class MarkupDenominatorFunctionBase : FunctionMessage
    {

    }

    public partial class PriceDecimalsFunction : PriceDecimalsFunctionBase { }

    [Function("PRICE_DECIMALS", "uint256")]
    public class PriceDecimalsFunctionBase : FunctionMessage
    {

    }

    public partial class DepositFunction : DepositFunctionBase { }

    [Function("deposit")]
    public class DepositFunctionBase : FunctionMessage
    {

    }

    public partial class EntryPointFunction : EntryPointFunctionBase { }

    [Function("entryPoint", "address")]
    public class EntryPointFunctionBase : FunctionMessage
    {

    }

    public partial class EstimateTokenCostFunction : EstimateTokenCostFunctionBase { }

    [Function("estimateTokenCost", "uint256")]
    public class EstimateTokenCostFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "ethCost", 1)]
        public virtual BigInteger EthCost { get; set; }
    }

    public partial class GetCurrentTokenPriceFunction : GetCurrentTokenPriceFunctionBase { }

    [Function("getCurrentTokenPrice", "uint256")]
    public class GetCurrentTokenPriceFunctionBase : FunctionMessage
    {

    }

    public partial class GetDepositFunction : GetDepositFunctionBase { }

    [Function("getDeposit", "uint256")]
    public class GetDepositFunctionBase : FunctionMessage
    {

    }

    public partial class OwnerFunction : OwnerFunctionBase { }

    [Function("owner", "address")]
    public class OwnerFunctionBase : FunctionMessage
    {

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
        [Parameter("uint256", "", 4)]
        public virtual BigInteger ReturnValue4 { get; set; }
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

    public partial class RenounceOwnershipFunction : RenounceOwnershipFunctionBase { }

    [Function("renounceOwnership")]
    public class RenounceOwnershipFunctionBase : FunctionMessage
    {

    }

    public partial class SetPriceMarkupFunction : SetPriceMarkupFunctionBase { }

    [Function("setPriceMarkup")]
    public class SetPriceMarkupFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "markup", 1)]
        public virtual BigInteger Markup { get; set; }
    }

    public partial class SetPriceOracleFunction : SetPriceOracleFunctionBase { }

    [Function("setPriceOracle")]
    public class SetPriceOracleFunctionBase : FunctionMessage
    {
        [Parameter("address", "oracleAddress", 1)]
        public virtual string OracleAddress { get; set; }
    }

    public partial class SetTokenFunction : SetTokenFunctionBase { }

    [Function("setToken")]
    public class SetTokenFunctionBase : FunctionMessage
    {
        [Parameter("address", "tokenAddress", 1)]
        public virtual string TokenAddress { get; set; }
    }

    public partial class TokenFunction : TokenFunctionBase { }

    [Function("token", "address")]
    public class TokenFunctionBase : FunctionMessage
    {

    }

    public partial class TransferOwnershipFunction : TransferOwnershipFunctionBase { }

    [Function("transferOwnership")]
    public class TransferOwnershipFunctionBase : FunctionMessage
    {
        [Parameter("address", "newOwner", 1)]
        public virtual string NewOwner { get; set; }
    }

    public partial class ValidatePaymasterUserOpFunction : ValidatePaymasterUserOpFunctionBase { }

    [Function("validatePaymasterUserOp", typeof(ValidatePaymasterUserOpOutputDTO))]
    public class ValidatePaymasterUserOpFunctionBase : FunctionMessage
    {
        [Parameter("tuple", "userOp", 1)]
        public virtual PackedUserOperation UserOp { get; set; }
        [Parameter("bytes32", "", 2)]
        public virtual byte[] ReturnValue2 { get; set; }
        [Parameter("uint256", "maxCost", 3)]
        public virtual BigInteger MaxCost { get; set; }
    }

    public partial class WithdrawToFunction : WithdrawToFunctionBase { }

    [Function("withdrawTo")]
    public class WithdrawToFunctionBase : FunctionMessage
    {
        [Parameter("address", "to", 1)]
        public virtual string To { get; set; }
        [Parameter("uint256", "amount", 2)]
        public virtual BigInteger Amount { get; set; }
    }

    public partial class WithdrawTokensFunction : WithdrawTokensFunctionBase { }

    [Function("withdrawTokens")]
    public class WithdrawTokensFunctionBase : FunctionMessage
    {
        [Parameter("address", "to", 1)]
        public virtual string To { get; set; }
        [Parameter("uint256", "amount", 2)]
        public virtual BigInteger Amount { get; set; }
    }

    public partial class MarkupDenominatorOutputDTO : MarkupDenominatorOutputDTOBase { }

    [FunctionOutput]
    public class MarkupDenominatorOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class PriceDecimalsOutputDTO : PriceDecimalsOutputDTOBase { }

    [FunctionOutput]
    public class PriceDecimalsOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }



    public partial class EntryPointOutputDTO : EntryPointOutputDTOBase { }

    [FunctionOutput]
    public class EntryPointOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class EstimateTokenCostOutputDTO : EstimateTokenCostOutputDTOBase { }

    [FunctionOutput]
    public class EstimateTokenCostOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class GetCurrentTokenPriceOutputDTO : GetCurrentTokenPriceOutputDTOBase { }

    [FunctionOutput]
    public class GetCurrentTokenPriceOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class GetDepositOutputDTO : GetDepositOutputDTOBase { }

    [FunctionOutput]
    public class GetDepositOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class OwnerOutputDTO : OwnerOutputDTOBase { }

    [FunctionOutput]
    public class OwnerOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
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





    public partial class OracleChangedEventDTO : OracleChangedEventDTOBase { }

    [Event("OracleChanged")]
    public class OracleChangedEventDTOBase : IEventDTO
    {
        [Parameter("address", "oldOracle", 1, true )]
        public virtual string OldOracle { get; set; }
        [Parameter("address", "newOracle", 2, true )]
        public virtual string NewOracle { get; set; }
    }

    public partial class OwnershipTransferredEventDTO : OwnershipTransferredEventDTOBase { }

    [Event("OwnershipTransferred")]
    public class OwnershipTransferredEventDTOBase : IEventDTO
    {
        [Parameter("address", "previousOwner", 1, true )]
        public virtual string PreviousOwner { get; set; }
        [Parameter("address", "newOwner", 2, true )]
        public virtual string NewOwner { get; set; }
    }

    public partial class TokenChangedEventDTO : TokenChangedEventDTOBase { }

    [Event("TokenChanged")]
    public class TokenChangedEventDTOBase : IEventDTO
    {
        [Parameter("address", "oldToken", 1, true )]
        public virtual string OldToken { get; set; }
        [Parameter("address", "newToken", 2, true )]
        public virtual string NewToken { get; set; }
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

    public partial class InsufficientDepositError : InsufficientDepositErrorBase { }
    [Error("InsufficientDeposit")]
    public class InsufficientDepositErrorBase : IErrorDTO
    {
    }

    public partial class InsufficientTokenAllowanceError : InsufficientTokenAllowanceErrorBase { }
    [Error("InsufficientTokenAllowance")]
    public class InsufficientTokenAllowanceErrorBase : IErrorDTO
    {
    }

    public partial class InsufficientTokenBalanceError : InsufficientTokenBalanceErrorBase { }
    [Error("InsufficientTokenBalance")]
    public class InsufficientTokenBalanceErrorBase : IErrorDTO
    {
    }

    public partial class InvalidMarkupError : InvalidMarkupErrorBase { }
    [Error("InvalidMarkup")]
    public class InvalidMarkupErrorBase : IErrorDTO
    {
    }

    public partial class OnlyEntryPointError : OnlyEntryPointErrorBase { }
    [Error("OnlyEntryPoint")]
    public class OnlyEntryPointErrorBase : IErrorDTO
    {
    }

    public partial class OwnableInvalidOwnerError : OwnableInvalidOwnerErrorBase { }

    [Error("OwnableInvalidOwner")]
    public class OwnableInvalidOwnerErrorBase : IErrorDTO
    {
        [Parameter("address", "owner", 1)]
        public virtual string Owner { get; set; }
    }

    public partial class OwnableUnauthorizedAccountError : OwnableUnauthorizedAccountErrorBase { }

    [Error("OwnableUnauthorizedAccount")]
    public class OwnableUnauthorizedAccountErrorBase : IErrorDTO
    {
        [Parameter("address", "account", 1)]
        public virtual string Account { get; set; }
    }

    public partial class SafeERC20FailedOperationError : SafeERC20FailedOperationErrorBase { }

    [Error("SafeERC20FailedOperation")]
    public class SafeERC20FailedOperationErrorBase : IErrorDTO
    {
        [Parameter("address", "token", 1)]
        public virtual string Token { get; set; }
    }

    public partial class TokenTransferFailedError : TokenTransferFailedErrorBase { }
    [Error("TokenTransferFailed")]
    public class TokenTransferFailedErrorBase : IErrorDTO
    {
    }
}
