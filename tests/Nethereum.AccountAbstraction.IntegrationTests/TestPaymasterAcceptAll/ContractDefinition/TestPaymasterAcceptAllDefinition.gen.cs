using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.AccountAbstraction.Structs;
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
        public static string BYTECODE = "60a0806040523461013457602081610e9d803803809161001f828561015e565b83398101031261013457516001600160a01b03811680820361013457331561014b5761004a33610195565b6040516301ffc9a760e01b815263283f548960e01b6004820152602081602481855afa908115610140575f91610101575b50156100e457506080523332036100d6575b604051610cb390816101ea823960805181818161020e01528181610311015281816103da015281816104af0152818161055f015281816109bd01528181610aa20152610c200152f35b6100df32610195565b61008d565b6365d25c7160e01b5f5260045263283f548960e01b60245260445ffd5b90506020813d602011610138575b8161011c6020938361015e565b8101031261013457518015158103610134575f61007b565b5f80fd5b3d915061010f565b6040513d5f823e3d90fd5b631e4fbdf760e01b5f525f60045260245ffd5b601f909101601f19168101906001600160401b0382119082101761018157604052565b634e487b7160e01b5f52604160045260245ffd5b600180546001600160a01b03199081169091555f80546001600160a01b03938416928116831782559192909116907f8be0079c531659141344cd1fd0a4f28419497f9722a3daafe3b4186f6b6457e09080a356fe60806040526004361015610011575f80fd5b5f5f3560e01c80630396cb6014610a47578063205c28781461096557806352b7512c1461084d578063715018a61461078957806379ba50971461067f5780637c627b21146105d45780638da5cb5b14610583578063b0d691fe14610514578063bb9fe6bf14610460578063c23a5cea14610382578063c399ec8814610299578063d0e30db0146101cc578063e30c39781461017a5763f2fde38b146100b4575f80fd5b346101775760207ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc3601126101775760043573ffffffffffffffffffffffffffffffffffffffff81168091036101755761010c610bbd565b807fffffffffffffffffffffffff0000000000000000000000000000000000000000600154161760015573ffffffffffffffffffffffffffffffffffffffff8254167f38d16b8cac22d99fc7c124b9cd0de2d3fa1faef420bfe791d8c362d765e227008380a380f35b505b80fd5b503461017757807ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc36011261017757602073ffffffffffffffffffffffffffffffffffffffff60015416604051908152f35b50807ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc360112610177578073ffffffffffffffffffffffffffffffffffffffff7f000000000000000000000000000000000000000000000000000000000000000016803b156102965781602491604051928380927fb760faf900000000000000000000000000000000000000000000000000000000825230600483015234905af1801561028b5761027a5750f35b8161028491610b4f565b6101775780f35b6040513d84823e3d90fd5b50fd5b503461017757807ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc360112610177576040517f70a0823100000000000000000000000000000000000000000000000000000000815230600482015260208160248173ffffffffffffffffffffffffffffffffffffffff7f0000000000000000000000000000000000000000000000000000000000000000165afa90811561028b57829161034c575b602082604051908152f35b90506020813d60201161037a575b8161036760209383610b4f565b810103126101755760209150515f610341565b3d915061035a565b50346101775760207ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc36011261017757806103bb610b2c565b6103c3610bbd565b73ffffffffffffffffffffffffffffffffffffffff7f00000000000000000000000000000000000000000000000000000000000000001690813b1561045c5773ffffffffffffffffffffffffffffffffffffffff602484928360405195869485937fc23a5cea0000000000000000000000000000000000000000000000000000000085521660048401525af1801561028b5761027a5750f35b5050fd5b503461017757807ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc36011261017757610497610bbd565b8073ffffffffffffffffffffffffffffffffffffffff7f000000000000000000000000000000000000000000000000000000000000000016803b15610296578180916004604051809481937fbb9fe6bf0000000000000000000000000000000000000000000000000000000083525af1801561028b5761027a5750f35b503461017757807ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc36011261017757602060405173ffffffffffffffffffffffffffffffffffffffff7f0000000000000000000000000000000000000000000000000000000000000000168152f35b503461017757807ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc3601126101775773ffffffffffffffffffffffffffffffffffffffff6020915416604051908152f35b50346101775760807ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc36011261017757600360043510156101775760243567ffffffffffffffff8111610175573660238201121561017557806004013567ffffffffffffffff811161067b573691016024011161017757600490610656610c09565b7f25ad501f000000000000000000000000000000000000000000000000000000008152fd5b8280fd5b503461017757807ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc360112610177573373ffffffffffffffffffffffffffffffffffffffff600154160361075d577fffffffffffffffffffffffff0000000000000000000000000000000000000000600154166001558054337fffffffffffffffffffffffff0000000000000000000000000000000000000000821617825573ffffffffffffffffffffffffffffffffffffffff3391167f8be0079c531659141344cd1fd0a4f28419497f9722a3daafe3b4186f6b6457e08380a380f35b807f118cdaa7000000000000000000000000000000000000000000000000000000006024925233600452fd5b503461017757807ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc360112610177576107c0610bbd565b7fffffffffffffffffffffffff0000000000000000000000000000000000000000600154166001558073ffffffffffffffffffffffffffffffffffffffff81547fffffffffffffffffffffffff000000000000000000000000000000000000000081168355167f8be0079c531659141344cd1fd0a4f28419497f9722a3daafe3b4186f6b6457e08280a380f35b50346101775760607ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc3601126101775760043567ffffffffffffffff8111610175577ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc6101209136030112610177576108c4610c09565b6040516020810181811067ffffffffffffffff82111761093857907fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffe0601f83606094604052858452604051958694604086525180928160408801528787015e80868387010152602085015201168101030190f35b6024837f4e487b710000000000000000000000000000000000000000000000000000000081526041600452fd5b50346101775760407ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc360112610177578061099e610b2c565b6109a6610bbd565b73ffffffffffffffffffffffffffffffffffffffff7f00000000000000000000000000000000000000000000000000000000000000001690813b1561045c5773ffffffffffffffffffffffffffffffffffffffff604484928360405195869485937f205c287800000000000000000000000000000000000000000000000000000000855216600484015260243560248401525af1801561028b5761027a5750f35b5060207ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffc360112610b285760043563ffffffff8116809103610b2857610a8b610bbd565b73ffffffffffffffffffffffffffffffffffffffff7f00000000000000000000000000000000000000000000000000000000000000001690813b15610b28575f906024604051809481937f0396cb60000000000000000000000000000000000000000000000000000000008352600483015234905af18015610b1d57610b0f575080f35b610b1b91505f90610b4f565b005b6040513d5f823e3d90fd5b5f80fd5b6004359073ffffffffffffffffffffffffffffffffffffffff82168203610b2857565b90601f7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffe0910116810190811067ffffffffffffffff821117610b9057604052565b7f4e487b71000000000000000000000000000000000000000000000000000000005f52604160045260245ffd5b73ffffffffffffffffffffffffffffffffffffffff5f54163303610bdd57565b7f118cdaa7000000000000000000000000000000000000000000000000000000005f523360045260245ffd5b73ffffffffffffffffffffffffffffffffffffffff7f000000000000000000000000000000000000000000000000000000000000000016803303610c4a5750565b7ffe34a6d3000000000000000000000000000000000000000000000000000000005f52336004523060245260445260645ffdfea2646970667358221220964be400de268596129f649ebe365d1c237541d58f0b88f43fac1912ab7ed1cc64736f6c634300081c0033";
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

    public partial class ERC165ErrorError : ERC165ErrorErrorBase { }

    [Error("ERC165Error")]
    public class ERC165ErrorErrorBase : IErrorDTO
    {
        [Parameter("address", "entryPoint", 1)]
        public virtual string EntryPoint { get; set; }
        [Parameter("bytes4", "interfaceId", 2)]
        public virtual byte[] InterfaceId { get; set; }
    }

    public partial class MustOverrideError : MustOverrideErrorBase { }
    [Error("MustOverride")]
    public class MustOverrideErrorBase : IErrorDTO
    {
    }

    public partial class NotFromEntryPointError : NotFromEntryPointErrorBase { }

    [Error("NotFromEntryPoint")]
    public class NotFromEntryPointErrorBase : IErrorDTO
    {
        [Parameter("address", "msgSender", 1)]
        public virtual string MsgSender { get; set; }
        [Parameter("address", "entity", 2)]
        public virtual string Entity { get; set; }
        [Parameter("address", "entryPoint", 3)]
        public virtual string EntryPoint { get; set; }
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
