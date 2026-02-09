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
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.IntegrationTests.TestPaymasterAcceptAll.ContractDefinition
{


    public partial class TestPaymasterAcceptAllDeployment : TestPaymasterAcceptAllDeploymentBase
    {
        public TestPaymasterAcceptAllDeployment() : base(BYTECODE) { }
        public TestPaymasterAcceptAllDeployment(string byteCode) : base(byteCode) { }
    }

    public class TestPaymasterAcceptAllDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "60a0806040523461017b576040816109fd803803809161001f82856101a5565b83398101031261017b578051906001600160a01b0382169081830361017b57602001516001600160a01b0381169081900361017b57801561019257600180546001600160a01b03199081169091555f80549182168317815560405192916001600160a01b0316907f8be0079c531659141344cd1fd0a4f28419497f9722a3daafe3b4186f6b6457e09080a36301ffc9a760e01b815263283f548960e01b6004820152602081602481855afa908115610187575f91610148575b501561012b575060805260405161082090816101dd823960805181818161016301528181610209015281816102a70152818161032f0152818161039901528181610619015281816106b1015261079d0152f35b6365d25c7160e01b5f5260045263283f548960e01b60245260445ffd5b90506020813d60201161017f575b81610163602093836101a5565b8101031261017b5751801515810361017b575f6100d8565b5f80fd5b3d9150610156565b6040513d5f823e3d90fd5b631e4fbdf760e01b5f525f60045260245ffd5b601f909101601f19168101906001600160401b038211908210176101c857604052565b634e487b7160e01b5f52604160045260245ffdfe60806040526004361015610011575f80fd5b5f5f3560e01c80630396cb6014610689578063205c2878146105f457806352b7512c1461054d578063715018a6146104e857806379ba5097146104635780637c627b21146103ef5780638da5cb5b146103c8578063b0d691fe14610383578063bb9fe6bf14610313578063c23a5cea14610282578063c399ec88146101dd578063d0e30db014610154578063e30c39781461012b5763f2fde38b146100b4575f80fd5b34610128576020366003190112610128576004356001600160a01b03811690819003610126576100e2610775565b600180546001600160a01b0319168217905581546001600160a01b03167f38d16b8cac22d99fc7c124b9cd0de2d3fa1faef420bfe791d8c362d765e227008380a380f35b505b80fd5b50346101285780600319360112610128576001546040516001600160a01b039091168152602090f35b508060031936011261012857807f00000000000000000000000000000000000000000000000000000000000000006001600160a01b0316803b156101da57816024916040519283809263b760faf960e01b825230600483015234905af180156101cf576101be5750f35b816101c89161073f565b6101285780f35b6040513d84823e3d90fd5b50fd5b50346101285780600319360112610128576040516370a0823160e01b81523060048201526020816024817f00000000000000000000000000000000000000000000000000000000000000006001600160a01b03165afa9081156101cf57829161024c575b602082604051908152f35b90506020813d60201161027a575b816102676020938361073f565b810103126101265760209150515f610241565b3d915061025a565b5034610128576020366003190112610128578061029d610729565b6102a5610775565b7f00000000000000000000000000000000000000000000000000000000000000006001600160a01b031690813b1561030f5760405163611d2e7560e11b81526001600160a01b0390911660048201529082908290602490829084905af180156101cf576101be5750f35b5050fd5b503461012857806003193601126101285761032c610775565b807f00000000000000000000000000000000000000000000000000000000000000006001600160a01b0316803b156101da5781809160046040518094819363bb9fe6bf60e01b83525af180156101cf576101be5750f35b50346101285780600319360112610128576040517f00000000000000000000000000000000000000000000000000000000000000006001600160a01b03168152602090f35b5034610128578060031936011261012857546040516001600160a01b039091168152602090f35b503461012857608036600319011261012857600360043510156101285760243567ffffffffffffffff8111610126573660238201121561012657806004013567ffffffffffffffff811161045f57369101602401116101285760049061045361079b565b6325ad501f60e01b8152fd5b8280fd5b5034610128578060031936011261012857600154336001600160a01b03909116036104d557600180546001600160a01b0319908116909155815433918116821783556001600160a01b03167f8be0079c531659141344cd1fd0a4f28419497f9722a3daafe3b4186f6b6457e08380a380f35b63118cdaa760e01b815233600452602490fd5b5034610128578060031936011261012857610501610775565b600180546001600160a01b03199081169091558154908116825581906001600160a01b03167f8be0079c531659141344cd1fd0a4f28419497f9722a3daafe3b4186f6b6457e08280a380f35b50346101285760603660031901126101285760043567ffffffffffffffff8111610126576101209060031990360301126101285761058961079b565b6040516020810181811067ffffffffffffffff8211176105e0579081606092604052838252604051938492604084525180928160408601528585015e82820184018190526020830152601f01601f19168101030190f35b634e487b7160e01b83526041600452602483fd5b5034610128576040366003190112610128578061060f610729565b610617610775565b7f00000000000000000000000000000000000000000000000000000000000000006001600160a01b031690813b1561030f5760405163040b850f60e31b81526001600160a01b03909116600482015260248035908201529082908290604490829084905af180156101cf576101be5750f35b5060203660031901126107255760043563ffffffff8116809103610725576106af610775565b7f00000000000000000000000000000000000000000000000000000000000000006001600160a01b031690813b15610725575f90602460405180948193621cb65b60e51b8352600483015234905af1801561071a5761070c575080f35b61071891505f9061073f565b005b6040513d5f823e3d90fd5b5f80fd5b600435906001600160a01b038216820361072557565b90601f8019910116810190811067ffffffffffffffff82111761076157604052565b634e487b7160e01b5f52604160045260245ffd5b5f546001600160a01b0316330361078857565b63118cdaa760e01b5f523360045260245ffd5b7f00000000000000000000000000000000000000000000000000000000000000006001600160a01b0316338190036107d05750565b63fe34a6d360e01b5f52336004523060245260445260645ffdfea26469706673582212206a30378b216ba337023a9a4561050389ccbf0ff3ea85ff2ddd777ac36c75bbad64736f6c634300081c0033";
        public TestPaymasterAcceptAllDeploymentBase() : base(BYTECODE) { }
        public TestPaymasterAcceptAllDeploymentBase(string byteCode) : base(byteCode) { }
        [Parameter("address", "_entryPoint", 1)]
        public virtual string EntryPoint { get; set; }
        [Parameter("address", "_owner", 2)]
        public virtual string Owner { get; set; }
    }

    public partial class AcceptOwnershipFunction : AcceptOwnershipFunctionBase { }

    [Function("acceptOwnership")]
    public class AcceptOwnershipFunctionBase : FunctionMessage
    {

    }

    public partial class AddStakeFunction : AddStakeFunctionBase { }

    [Function("addStake")]
    public class AddStakeFunctionBase : FunctionMessage
    {
        [Parameter("uint32", "unstakeDelaySec", 1)]
        public virtual uint UnstakeDelaySec { get; set; }
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

    public partial class OwnerFunction : OwnerFunctionBase { }

    [Function("owner", "address")]
    public class OwnerFunctionBase : FunctionMessage
    {

    }

    public partial class PendingOwnerFunction : PendingOwnerFunctionBase { }

    [Function("pendingOwner", "address")]
    public class PendingOwnerFunctionBase : FunctionMessage
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
        [Parameter("uint256", "actualUserOpFeePerGas", 4)]
        public virtual BigInteger ActualUserOpFeePerGas { get; set; }
    }

    public partial class RenounceOwnershipFunction : RenounceOwnershipFunctionBase { }

    [Function("renounceOwnership")]
    public class RenounceOwnershipFunctionBase : FunctionMessage
    {

    }

    public partial class TransferOwnershipFunction : TransferOwnershipFunctionBase { }

    [Function("transferOwnership")]
    public class TransferOwnershipFunctionBase : FunctionMessage
    {
        [Parameter("address", "newOwner", 1)]
        public virtual string NewOwner { get; set; }
    }

    public partial class UnlockStakeFunction : UnlockStakeFunctionBase { }

    [Function("unlockStake")]
    public class UnlockStakeFunctionBase : FunctionMessage
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

    public partial class WithdrawStakeFunction : WithdrawStakeFunctionBase { }

    [Function("withdrawStake")]
    public class WithdrawStakeFunctionBase : FunctionMessage
    {
        [Parameter("address", "withdrawAddress", 1)]
        public virtual string WithdrawAddress { get; set; }
    }

    public partial class WithdrawToFunction : WithdrawToFunctionBase { }

    [Function("withdrawTo")]
    public class WithdrawToFunctionBase : FunctionMessage
    {
        [Parameter("address", "withdrawAddress", 1)]
        public virtual string WithdrawAddress { get; set; }
        [Parameter("uint256", "amount", 2)]
        public virtual BigInteger Amount { get; set; }
    }

    public partial class OwnershipTransferStartedEventDTO : OwnershipTransferStartedEventDTOBase { }

    [Event("OwnershipTransferStarted")]
    public class OwnershipTransferStartedEventDTOBase : IEventDTO
    {
        [Parameter("address", "previousOwner", 1, true )]
        public virtual string PreviousOwner { get; set; }
        [Parameter("address", "newOwner", 2, true )]
        public virtual string NewOwner { get; set; }
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

    public partial class OwnerOutputDTO : OwnerOutputDTOBase { }

    [FunctionOutput]
    public class OwnerOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class PendingOwnerOutputDTO : PendingOwnerOutputDTOBase { }

    [FunctionOutput]
    public class PendingOwnerOutputDTOBase : IFunctionOutputDTO 
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




}
