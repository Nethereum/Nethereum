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
using Nethereum.AccountAbstraction.Contracts.Paymaster.VerifyingPaymaster.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Paymaster.VerifyingPaymaster.ContractDefinition
{


    public partial class VerifyingPaymasterDeployment : VerifyingPaymasterDeploymentBase
    {
        public VerifyingPaymasterDeployment() : base(BYTECODE) { }
        public VerifyingPaymasterDeployment(string byteCode) : base(byteCode) { }
    }

    public class VerifyingPaymasterDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x60a03461012757601f610d1c38819003918201601f19168301916001600160401b0383118484101761012b57808492606094604052833981010312610127576100478161013f565b9061006060406100596020840161013f565b920161013f565b6001600160a01b03909116918215610114575f80546001600160a01b031981168517825560405194916001600160a01b03909116907f8be0079c531659141344cd1fd0a4f28419497f9722a3daafe3b4186f6b6457e09080a3608052600180546001600160a01b0319166001600160a01b0392909216919091179055610bc8908161015482396080518181816102000152818161029a01528181610389015281816105230152818161060901526108ce0152f35b631e4fbdf760e01b5f525f60045260245ffd5b5f80fd5b634e487b7160e01b5f52604160045260245ffd5b51906001600160a01b03821682036101275756fe60806040526004361015610022575b3615610018575f80fd5b6100206108cc565b005b5f5f3560e01c8063205c2878146105d357806323d9ac9b146105aa57806352b7512c146104ee5780635829c5f51461047d578063715018a6146104235780637c627b211461032d5780638da5cb5b146103065780639c90b443146102c9578063b0d691fe14610284578063c399ec88146101d4578063d0e30db0146101bd578063f2fde38b146101335763f5cba98c146100bc575061000e565b34610130576020366003190112610130576004356001600160a01b0381169081900361012e576100ea610935565b600180546001600160a01b0319811683179091556001600160a01b03167feeb293e1f8f3a9db91ade748726387ed1352ca78f5430c5f06fe3d1e1ad505798380a380f35b505b80fd5b5034610130576020366003190112610130576004356001600160a01b0381169081900361012e57610162610935565b80156101a95781546001600160a01b03198116821783556001600160a01b03167f8be0079c531659141344cd1fd0a4f28419497f9722a3daafe3b4186f6b6457e08380a380f35b631e4fbdf760e01b82526004829052602482fd5b5080600319360112610130576101d16108cc565b80f35b50346101305780600319360112610130576040516370a0823160e01b81523060048201526020816024817f00000000000000000000000000000000000000000000000000000000000000006001600160a01b03165afa908115610279578291610243575b602082604051908152f35b90506020813d602011610271575b8161025e60209383610685565b8101031261012e5760209150515f610238565b3d9150610251565b6040513d84823e3d90fd5b50346101305780600319360112610130576040517f00000000000000000000000000000000000000000000000000000000000000006001600160a01b03168152602090f35b5034610130576020366003190112610130576004356001600160a01b0381169081900361012e578160409160209352600283522054604051908152f35b5034610130578060031936011261013057546040516001600160a01b039091168152602090f35b503461013057608036600319011261013057600360043510156101305760243567ffffffffffffffff811161012e573660238201121561012e57806004013567ffffffffffffffff811161041f57810136602482011161041f577f00000000000000000000000000000000000000000000000000000000000000006001600160a01b03163303610410576040908290031261012e5760248101356001600160a01b0381169182820361040c57505060405160443581527f2f5b7da0b8502c9a04f1c60a92a12cc859cb3cefb8951253e2f1c0df6c65d28d90602090a280f35b8380fd5b63bd07c55160e01b8352600483fd5b8280fd5b503461013057806003193601126101305761043c610935565b80546001600160a01b03198116825581906001600160a01b03167f8be0079c531659141344cd1fd0a4f28419497f9722a3daafe3b4186f6b6457e08280a380f35b50346101305760603660031901126101305760043567ffffffffffffffff811161012e57610120600319823603011261012e576024359065ffffffffffff8216820361041f576044359265ffffffffffff841684036101305760206104e68585856004016109a1565b604051908152f35b50346101305760603660031901126101305760043567ffffffffffffffff811161012e57610120600319823603011261012e577f00000000000000000000000000000000000000000000000000000000000000006001600160a01b0316330361059b57602061056560609260443590600401610702565b604092919251948593604085528051938491826040880152018686015e8383018501526020830152601f01601f19168101030190f35b63bd07c55160e01b8252600482fd5b50346101305780600319360112610130576001546040516001600160a01b039091168152602090f35b5034610681576040366003190112610681576004356001600160a01b03811690818103610681575060243590610607610935565b7f00000000000000000000000000000000000000000000000000000000000000006001600160a01b031691823b156106815760445f9283604051958694859363040b850f60e31b8552600485015260248401525af180156106765761066a575080f35b61002091505f90610685565b6040513d5f823e3d90fd5b5f80fd5b90601f8019910116810190811067ffffffffffffffff8211176106a757604052565b634e487b7160e01b5f52604160045260245ffd5b903590601e1981360301821215610681570180359067ffffffffffffffff82116106815760200191813603831361068157565b356001600160a01b03811681036106815790565b919061071160e08401846106bb565b9190936020831061088e5782601a1161068157601485013560d01c926020811061068157601a86013560d01c95610753913691601f199091019060200161095b565b83421180610885575b610855576107a16107aa916107728887866109a1565b7f19457468657265756d205369676e6564204d6573736167653a0a3332000000005f52601c52603c5f20610a62565b90929192610a9c565b6001546001600160a01b03908116911603610814576107c8906106ee565b906040519160018060a01b031660208301526040820152604081526107ee606082610685565b9260a09190911b65ffffffffffff60a01b1660d09190911b6001600160d01b0319161790565b50919260a09190911b65ffffffffffff60a01b1660d09190911b6001600160d01b0319161760011790505b9060405161084e602082610685565b5f81529190565b5050509161083f919260019165ffffffffffff60d01b9060d01b169065ffffffffffff60a01b9060a01b16171790565b5083151561075c565b60405162461bcd60e51b8152602060048201526016602482015275496e76616c6964207061796d6173746572206461746160501b6044820152606490fd5b7f00000000000000000000000000000000000000000000000000000000000000006001600160a01b0316803b15610681575f6024916040519283809263b760faf960e01b825230600483015234905af18015610676576109295750565b5f61093391610685565b565b5f546001600160a01b0316330361094857565b63118cdaa760e01b5f523360045260245ffd5b92919267ffffffffffffffff82116106a75760405191610985601f8201601f191660200184610685565b829481845281830111610681578281602093845f960137010152565b909165ffffffffffff90816109b5846106ee565b9460c06109cf6109c860408801886106bb565b369161095b565b60208151910120956109e76109c860608301836106bb565b6020815191012060405197602089019960018060a01b03168a52602083013560408a015260608901526080880152608081013560a088015260a081013582880152013560e0860152466101008601523061012086015216610140840152166101608201526101608152610a5c61018082610685565b51902090565b8151919060418303610a9257610a8b9250602082015190606060408401519301515f1a90610b10565b9192909190565b50505f9160029190565b6004811015610afc5780610aae575050565b60018103610ac55763f645eedf60e01b5f5260045ffd5b60028103610ae0575063fce698f760e01b5f5260045260245ffd5b600314610aea5750565b6335e2f38360e21b5f5260045260245ffd5b634e487b7160e01b5f52602160045260245ffd5b91907f7fffffffffffffffffffffffffffffff5d576e7357a4501ddfe92f46681b20a08411610b87579160209360809260ff5f9560405194855216868401526040830152606082015282805260015afa15610676575f516001600160a01b03811615610b7d57905f905f90565b505f906001905f90565b5050505f916003919056fea2646970667358221220970638e845d7a4a744d9ac6dc271d923d982af40af8c977181c667412c7a23d764736f6c634300081c0033";
        public VerifyingPaymasterDeploymentBase() : base(BYTECODE) { }
        public VerifyingPaymasterDeploymentBase(string byteCode) : base(byteCode) { }
        [Parameter("address", "_entryPoint", 1)]
        public virtual string EntryPoint { get; set; }
        [Parameter("address", "_owner", 2)]
        public virtual string Owner { get; set; }
        [Parameter("address", "_signer", 3)]
        public virtual string Signer { get; set; }
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

    public partial class GetDepositFunction : GetDepositFunctionBase { }

    [Function("getDeposit", "uint256")]
    public class GetDepositFunctionBase : FunctionMessage
    {

    }

    public partial class GetHashFunction : GetHashFunctionBase { }

    [Function("getHash", "bytes32")]
    public class GetHashFunctionBase : FunctionMessage
    {
        [Parameter("tuple", "userOp", 1)]
        public virtual PackedUserOperation UserOp { get; set; }
        [Parameter("uint48", "validUntil", 2)]
        public virtual ulong ValidUntil { get; set; }
        [Parameter("uint48", "validAfter", 3)]
        public virtual ulong ValidAfter { get; set; }
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
        [Parameter("uint8", "", 1)]
        public virtual byte ReturnValue1 { get; set; }
        [Parameter("bytes", "context", 2)]
        public virtual byte[] Context { get; set; }
        [Parameter("uint256", "actualGasCost", 3)]
        public virtual BigInteger ActualGasCost { get; set; }
        [Parameter("uint256", "", 4)]
        public virtual BigInteger ReturnValue4 { get; set; }
    }

    public partial class RenounceOwnershipFunction : RenounceOwnershipFunctionBase { }

    [Function("renounceOwnership")]
    public class RenounceOwnershipFunctionBase : FunctionMessage
    {

    }

    public partial class SenderNonceFunction : SenderNonceFunctionBase { }

    [Function("senderNonce", "uint256")]
    public class SenderNonceFunctionBase : FunctionMessage
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class SetVerifyingSignerFunction : SetVerifyingSignerFunctionBase { }

    [Function("setVerifyingSigner")]
    public class SetVerifyingSignerFunctionBase : FunctionMessage
    {
        [Parameter("address", "signer", 1)]
        public virtual string Signer { get; set; }
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
        [Parameter("bytes32", "userOpHash", 2)]
        public virtual byte[] UserOpHash { get; set; }
        [Parameter("uint256", "maxCost", 3)]
        public virtual BigInteger MaxCost { get; set; }
    }

    public partial class VerifyingSignerFunction : VerifyingSignerFunctionBase { }

    [Function("verifyingSigner", "address")]
    public class VerifyingSignerFunctionBase : FunctionMessage
    {

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



    public partial class EntryPointOutputDTO : EntryPointOutputDTOBase { }

    [FunctionOutput]
    public class EntryPointOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class GetDepositOutputDTO : GetDepositOutputDTOBase { }

    [FunctionOutput]
    public class GetDepositOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class GetHashOutputDTO : GetHashOutputDTOBase { }

    [FunctionOutput]
    public class GetHashOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class OwnerOutputDTO : OwnerOutputDTOBase { }

    [FunctionOutput]
    public class OwnerOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }





    public partial class SenderNonceOutputDTO : SenderNonceOutputDTOBase { }

    [FunctionOutput]
    public class SenderNonceOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
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

    public partial class VerifyingSignerOutputDTO : VerifyingSignerOutputDTOBase { }

    [FunctionOutput]
    public class VerifyingSignerOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }



    public partial class GasSponsoredEventDTO : GasSponsoredEventDTOBase { }

    [Event("GasSponsored")]
    public class GasSponsoredEventDTOBase : IEventDTO
    {
        [Parameter("address", "sender", 1, true )]
        public virtual string Sender { get; set; }
        [Parameter("uint256", "gasCost", 2, false )]
        public virtual BigInteger GasCost { get; set; }
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

    public partial class SignerChangedEventDTO : SignerChangedEventDTOBase { }

    [Event("SignerChanged")]
    public class SignerChangedEventDTOBase : IEventDTO
    {
        [Parameter("address", "oldSigner", 1, true )]
        public virtual string OldSigner { get; set; }
        [Parameter("address", "newSigner", 2, true )]
        public virtual string NewSigner { get; set; }
    }

    public partial class ECDSAInvalidSignatureError : ECDSAInvalidSignatureErrorBase { }
    [Error("ECDSAInvalidSignature")]
    public class ECDSAInvalidSignatureErrorBase : IErrorDTO
    {
    }

    public partial class ECDSAInvalidSignatureLengthError : ECDSAInvalidSignatureLengthErrorBase { }

    [Error("ECDSAInvalidSignatureLength")]
    public class ECDSAInvalidSignatureLengthErrorBase : IErrorDTO
    {
        [Parameter("uint256", "length", 1)]
        public virtual BigInteger Length { get; set; }
    }

    public partial class ECDSAInvalidSignatureSError : ECDSAInvalidSignatureSErrorBase { }

    [Error("ECDSAInvalidSignatureS")]
    public class ECDSAInvalidSignatureSErrorBase : IErrorDTO
    {
        [Parameter("bytes32", "s", 1)]
        public virtual byte[] S { get; set; }
    }

    public partial class ExpiredSignatureError : ExpiredSignatureErrorBase { }
    [Error("ExpiredSignature")]
    public class ExpiredSignatureErrorBase : IErrorDTO
    {
    }

    public partial class InsufficientDepositError : InsufficientDepositErrorBase { }
    [Error("InsufficientDeposit")]
    public class InsufficientDepositErrorBase : IErrorDTO
    {
    }

    public partial class InvalidSignatureError : InvalidSignatureErrorBase { }
    [Error("InvalidSignature")]
    public class InvalidSignatureErrorBase : IErrorDTO
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
}
