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

namespace Nethereum.AccountAbstraction.IntegrationTests.TestPaymasterAcceptAll.ContractDefinition
{


    public partial class TestPaymasterAcceptAllDeployment : TestPaymasterAcceptAllDeploymentBase
    {
        public TestPaymasterAcceptAllDeployment() : base(BYTECODE) { }
        public TestPaymasterAcceptAllDeployment(string byteCode) : base(byteCode) { }
    }

    public class TestPaymasterAcceptAllDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "60a0806040523461015e57602081610a7a803803809161001f8285610188565b83398101031261015e57516001600160a01b03811680820361015e57331561017557602060249161004f336101bf565b6040516301ffc9a760e01b8152631313998b60e31b600482015292839182905afa90811561016a575f9161012b575b50156100e6576080523332036100d8575b6040516108669081610214823960805181818161016301528181610209015281816102a70152818161032f015281816103990152818161063f015281816106d701526107c30152f35b6100e1326101bf565b61008f565b60405162461bcd60e51b815260206004820152601e60248201527f49456e747279506f696e7420696e74657266616365206d69736d6174636800006044820152606490fd5b90506020813d602011610162575b8161014660209383610188565b8101031261015e5751801515810361015e575f61007e565b5f80fd5b3d9150610139565b6040513d5f823e3d90fd5b631e4fbdf760e01b5f525f60045260245ffd5b601f909101601f19168101906001600160401b038211908210176101ab57604052565b634e487b7160e01b5f52604160045260245ffd5b600180546001600160a01b03199081169091555f80546001600160a01b03938416928116831782559192909116907f8be0079c531659141344cd1fd0a4f28419497f9722a3daafe3b4186f6b6457e09080a356fe60806040526004361015610011575f80fd5b5f5f3560e01c80630396cb60146106af578063205c28781461061a57806352b7512c14610573578063715018a61461050e57806379ba5097146104895780637c627b21146103ef5780638da5cb5b146103c8578063b0d691fe14610383578063bb9fe6bf14610313578063c23a5cea14610282578063c399ec88146101dd578063d0e30db014610154578063e30c39781461012b5763f2fde38b146100b4575f80fd5b34610128576020366003190112610128576004356001600160a01b03811690819003610126576100e261079b565b600180546001600160a01b0319168217905581546001600160a01b03167f38d16b8cac22d99fc7c124b9cd0de2d3fa1faef420bfe791d8c362d765e227008380a380f35b505b80fd5b50346101285780600319360112610128576001546040516001600160a01b039091168152602090f35b508060031936011261012857807f00000000000000000000000000000000000000000000000000000000000000006001600160a01b0316803b156101da57816024916040519283809263b760faf960e01b825230600483015234905af180156101cf576101be5750f35b816101c891610765565b6101285780f35b6040513d84823e3d90fd5b50fd5b50346101285780600319360112610128576040516370a0823160e01b81523060048201526020816024817f00000000000000000000000000000000000000000000000000000000000000006001600160a01b03165afa9081156101cf57829161024c575b602082604051908152f35b90506020813d60201161027a575b8161026760209383610765565b810103126101265760209150515f610241565b3d915061025a565b5034610128576020366003190112610128578061029d61074f565b6102a561079b565b7f00000000000000000000000000000000000000000000000000000000000000006001600160a01b031690813b1561030f5760405163611d2e7560e11b81526001600160a01b0390911660048201529082908290602490829084905af180156101cf576101be5750f35b5050fd5b503461012857806003193601126101285761032c61079b565b807f00000000000000000000000000000000000000000000000000000000000000006001600160a01b0316803b156101da5781809160046040518094819363bb9fe6bf60e01b83525af180156101cf576101be5750f35b50346101285780600319360112610128576040517f00000000000000000000000000000000000000000000000000000000000000006001600160a01b03168152602090f35b5034610128578060031936011261012857546040516001600160a01b039091168152602090f35b503461012857608036600319011261012857600360043510156101285760243567ffffffffffffffff8111610126573660238201121561012657806004013567ffffffffffffffff81116104855736910160240111610128576104506107c1565b60405162461bcd60e51b815260206004820152600d60248201526c6d757374206f7665727269646560981b6044820152606490fd5b8280fd5b5034610128578060031936011261012857600154336001600160a01b03909116036104fb57600180546001600160a01b0319908116909155815433918116821783556001600160a01b03167f8be0079c531659141344cd1fd0a4f28419497f9722a3daafe3b4186f6b6457e08380a380f35b63118cdaa760e01b815233600452602490fd5b503461012857806003193601126101285761052761079b565b600180546001600160a01b03199081169091558154908116825581906001600160a01b03167f8be0079c531659141344cd1fd0a4f28419497f9722a3daafe3b4186f6b6457e08280a380f35b50346101285760603660031901126101285760043567ffffffffffffffff811161012657610120906003199036030112610128576105af6107c1565b6040516020810181811067ffffffffffffffff821117610606579081606092604052838252604051938492604084525180928160408601528585015e82820184018190526020830152601f01601f19168101030190f35b634e487b7160e01b83526041600452602483fd5b5034610128576040366003190112610128578061063561074f565b61063d61079b565b7f00000000000000000000000000000000000000000000000000000000000000006001600160a01b031690813b1561030f5760405163040b850f60e31b81526001600160a01b03909116600482015260248035908201529082908290604490829084905af180156101cf576101be5750f35b50602036600319011261074b5760043563ffffffff811680910361074b576106d561079b565b7f00000000000000000000000000000000000000000000000000000000000000006001600160a01b031690813b1561074b575f90602460405180948193621cb65b60e51b8352600483015234905af1801561074057610732575080f35b61073e91505f90610765565b005b6040513d5f823e3d90fd5b5f80fd5b600435906001600160a01b038216820361074b57565b90601f8019910116810190811067ffffffffffffffff82111761078757604052565b634e487b7160e01b5f52604160045260245ffd5b5f546001600160a01b031633036107ae57565b63118cdaa760e01b5f523360045260245ffd5b7f00000000000000000000000000000000000000000000000000000000000000006001600160a01b031633036107f357565b60405162461bcd60e51b815260206004820152601560248201527414d95b99195c881b9bdd08115b9d1c9e541bda5b9d605a1b6044820152606490fdfea26469706673582212204d4c87373957f5dbd4481555138208e0838d3896fe48d1ad6d2600fd9dd41bb964736f6c634300081d0033";
        public TestPaymasterAcceptAllDeploymentBase() : base(BYTECODE) { }
        public TestPaymasterAcceptAllDeploymentBase(string byteCode) : base(byteCode) { }
        [Parameter("address", "_entryPoint", 1)]
        public virtual string EntryPoint { get; set; }
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
