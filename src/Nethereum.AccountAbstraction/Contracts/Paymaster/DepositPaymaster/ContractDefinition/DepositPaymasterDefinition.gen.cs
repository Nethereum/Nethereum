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
using Nethereum.AccountAbstraction.Contracts.Paymaster.DepositPaymaster.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Paymaster.DepositPaymaster.ContractDefinition
{


    public partial class DepositPaymasterDeployment : DepositPaymasterDeploymentBase
    {
        public DepositPaymasterDeployment() : base(BYTECODE) { }
        public DepositPaymasterDeployment(string byteCode) : base(byteCode) { }
    }

    public class DepositPaymasterDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x60a03461011957601f610b3538819003918201601f19168301916001600160401b0383118484101761011d5780849260409485528339810103126101195761004681610131565b906001600160a01b039061005c90602001610131565b16908115610106575f80546001600160a01b031981168417825560405193916001600160a01b03909116907f8be0079c531659141344cd1fd0a4f28419497f9722a3daafe3b4186f6b6457e09080a360805260017f9b779b17422d0df92223018b32b4d1fa46e071723d6817e2486d003becc55f00555f6002556109ef90816101468239608051818181601a015281816102930152818161031d015281816104a2015261057b0152f35b631e4fbdf760e01b5f525f60045260245ffd5b5f80fd5b634e487b7160e01b5f52604160045260245ffd5b51906001600160a01b03821682036101195756fe6080604052600436101561008f575b3615610018575f80fd5b7f00000000000000000000000000000000000000000000000000000000000000006001600160a01b0316803b1561008b575f6024916040519283809263b760faf960e01b825230600483015234905af180156100805761007457005b5f61007e916106c8565b005b6040513d5f823e3d90fd5b5f80fd5b5f3560e01c8063205c28781461068e5780632e1a7d4d1461065557806341b3d185146106385780635287ce12146105f357806352b7512c14610547578063715018a6146104f05780637c627b21146104465780638da5cb5b1461041f5780638fcc9cfb146103cb578063aa67c9191461034c578063b0d691fe14610308578063c399ec8814610268578063d0e30db0146101fd578063f2fde38b146101745763fc7e286d0361000e573461008b57602036600319011261008b576004356001600160a01b0381169081900361008b575f526001602052602060405f2054604051908152f35b3461008b57602036600319011261008b576004356001600160a01b0381169081900361008b576101a2610973565b80156101ea575f80546001600160a01b03198116831782556001600160a01b0316907f8be0079c531659141344cd1fd0a4f28419497f9722a3daafe3b4186f6b6457e09080a3005b631e4fbdf760e01b5f525f60045260245ffd5b5f36600319011261008b5761021061083a565b335f52600160205260405f2061022734825461079a565b90556040513481527f2da466a7b24304f47e87fa2e1e5a81b9831ce54fec19055ce277ca2f39ba42c460203392a260015f51602061099a5f395f51905f5255005b3461008b575f36600319011261008b576040516370a0823160e01b81523060048201526020816024817f00000000000000000000000000000000000000000000000000000000000000006001600160a01b03165afa8015610080575f906102d5575b602090604051908152f35b506020813d602011610300575b816102ef602093836106c8565b8101031261008b57602090516102ca565b3d91506102e2565b3461008b575f36600319011261008b576040517f00000000000000000000000000000000000000000000000000000000000000006001600160a01b03168152602090f35b602036600319011261008b576004356001600160a01b0381169081900361008b5761037561083a565b805f52600160205260405f2061038c34825461079a565b90557f2da466a7b24304f47e87fa2e1e5a81b9831ce54fec19055ce277ca2f39ba42c46020604051348152a260015f51602061099a5f395f51905f5255005b3461008b57602036600319011261008b577fcacd94bd1e7bb1185c816a740d9439bc8eff8159f6f4ffad8d306b5aca2ebd92604060043561040a610973565b600254908060025582519182526020820152a1005b3461008b575f36600319011261008b575f546040516001600160a01b039091168152602090f35b3461008b57608036600319011261008b57600435600381101561008b5760243567ffffffffffffffff811161008b573660238201121561008b57806004013567ffffffffffffffff811161008b57366024828401011161008b577f00000000000000000000000000000000000000000000000000000000000000006001600160a01b031633036104e15761007e9260246044359301906107a7565b63bd07c55160e01b5f5260045ffd5b3461008b575f36600319011261008b57610508610973565b5f80546001600160a01b0319811682556001600160a01b03167f8be0079c531659141344cd1fd0a4f28419497f9722a3daafe3b4186f6b6457e08280a3005b3461008b57606036600319011261008b5760043567ffffffffffffffff811161008b57610120600319823603011261008b577f00000000000000000000000000000000000000000000000000000000000000006001600160a01b031633036104e1576105bb6060916044359060040161071f565b91906020604051938492604084528051928391826040870152018585015e5f8383018501526020830152601f01601f19168101030190f35b3461008b57602036600319011261008b576004356001600160a01b0381169081900361008b575f5260016020526040805f205460025481101582519182526020820152f35b3461008b575f36600319011261008b576020600254604051908152f35b3461008b57602036600319011261008b5761066e61083a565b61067b6004353333610872565b60015f51602061099a5f395f51905f5255005b3461008b57604036600319011261008b576004356001600160a01b038116810361008b5761067b90602435906106c261083a565b33610872565b90601f8019910116810190811067ffffffffffffffff8211176106ea57604052565b634e487b7160e01b5f52604160045260245ffd5b9190820391821161070b57565b634e487b7160e01b5f52601160045260245ffd5b356001600160a01b038116919082900361008b57815f5260016020528060405f20541061078157815f52600160205260405f2061075d8282546106fe565b905560405191602083015260408201526040815261077c6060826106c8565b905f90565b50506040516107916020826106c8565b5f815290600190565b9190820180921161070b57565b929091826040918101031261008b5781356001600160a01b0381169283820361008b576020915001359260038110156108265760011461080f576107eb91926106fe565b90816107f5575050565b5f52600160205261080b60405f2091825461079a565b9055565b505f52600160205261080b60405f2091825461079a565b634e487b7160e01b5f52602160045260245ffd5b60025f51602061099a5f395f51905f5254146108635760025f51602061099a5f395f51905f5255565b633ee5aeb560e01b5f5260045ffd5b60018060a01b031690815f5260016020528260405f20541061093757815f52600160205260405f206108a58482546106fe565b90556001600160a01b0316915f80808084875af13d15610932573d67ffffffffffffffff81116106ea57604051906108e7601f8201601f1916602001836106c8565b81525f60203d92013e5b156109235760207fd1c19fbcd4551a5edfb66d43d2e337c04837afda3482b42bdf569a8fccdae5fb91604051908152a3565b631d42c86760e21b5f5260045ffd5b6108f1565b60405162461bcd60e51b8152602060048201526014602482015273125b9cdd59999a58da595b9d0819195c1bdcda5d60621b6044820152606490fd5b5f546001600160a01b0316330361098657565b63118cdaa760e01b5f523360045260245ffdfe9b779b17422d0df92223018b32b4d1fa46e071723d6817e2486d003becc55f00a264697066735822122074be00c06cd1b2e368aede37a7cf2345d7a700039f01b61f32aadbe539c83bfc64736f6c634300081c0033";
        public DepositPaymasterDeploymentBase() : base(BYTECODE) { }
        public DepositPaymasterDeploymentBase(string byteCode) : base(byteCode) { }
        [Parameter("address", "_entryPoint", 1)]
        public virtual string EntryPoint { get; set; }
        [Parameter("address", "_owner", 2)]
        public virtual string Owner { get; set; }
    }

    public partial class DepositFunction : DepositFunctionBase { }

    [Function("deposit")]
    public class DepositFunctionBase : FunctionMessage
    {

    }

    public partial class DepositForFunction : DepositForFunctionBase { }

    [Function("depositFor")]
    public class DepositForFunctionBase : FunctionMessage
    {
        [Parameter("address", "account", 1)]
        public virtual string Account { get; set; }
    }

    public partial class DepositsFunction : DepositsFunctionBase { }

    [Function("deposits", "uint256")]
    public class DepositsFunctionBase : FunctionMessage
    {
        [Parameter("address", "account", 1)]
        public virtual string Account { get; set; }
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

    public partial class GetDepositInfoFunction : GetDepositInfoFunctionBase { }

    [Function("getDepositInfo", typeof(GetDepositInfoOutputDTO))]
    public class GetDepositInfoFunctionBase : FunctionMessage
    {
        [Parameter("address", "account", 1)]
        public virtual string Account { get; set; }
    }

    public partial class MinDepositFunction : MinDepositFunctionBase { }

    [Function("minDeposit", "uint256")]
    public class MinDepositFunctionBase : FunctionMessage
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

    public partial class RenounceOwnershipFunction : RenounceOwnershipFunctionBase { }

    [Function("renounceOwnership")]
    public class RenounceOwnershipFunctionBase : FunctionMessage
    {

    }

    public partial class SetMinDepositFunction : SetMinDepositFunctionBase { }

    [Function("setMinDeposit")]
    public class SetMinDepositFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "min", 1)]
        public virtual BigInteger Min { get; set; }
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

    public partial class WithdrawFunction : WithdrawFunctionBase { }

    [Function("withdraw")]
    public class WithdrawFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "amount", 1)]
        public virtual BigInteger Amount { get; set; }
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





    public partial class DepositsOutputDTO : DepositsOutputDTOBase { }

    [FunctionOutput]
    public class DepositsOutputDTOBase : IFunctionOutputDTO 
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

    public partial class GetDepositOutputDTO : GetDepositOutputDTOBase { }

    [FunctionOutput]
    public class GetDepositOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class GetDepositInfoOutputDTO : GetDepositInfoOutputDTOBase { }

    [FunctionOutput]
    public class GetDepositInfoOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "balance", 1)]
        public virtual BigInteger Balance { get; set; }
        [Parameter("bool", "canPayFor", 2)]
        public virtual bool CanPayFor { get; set; }
    }

    public partial class MinDepositOutputDTO : MinDepositOutputDTOBase { }

    [FunctionOutput]
    public class MinDepositOutputDTOBase : IFunctionOutputDTO 
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









    public partial class ValidatePaymasterUserOpOutputDTO : ValidatePaymasterUserOpOutputDTOBase { }

    [FunctionOutput]
    public class ValidatePaymasterUserOpOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes", "context", 1)]
        public virtual byte[] Context { get; set; }
        [Parameter("uint256", "validationData", 2)]
        public virtual BigInteger ValidationData { get; set; }
    }





    public partial class DepositedEventDTO : DepositedEventDTOBase { }

    [Event("Deposited")]
    public class DepositedEventDTOBase : IEventDTO
    {
        [Parameter("address", "account", 1, true )]
        public virtual string Account { get; set; }
        [Parameter("uint256", "amount", 2, false )]
        public virtual BigInteger Amount { get; set; }
    }

    public partial class MinDepositChangedEventDTO : MinDepositChangedEventDTOBase { }

    [Event("MinDepositChanged")]
    public class MinDepositChangedEventDTOBase : IEventDTO
    {
        [Parameter("uint256", "oldMin", 1, false )]
        public virtual BigInteger OldMin { get; set; }
        [Parameter("uint256", "newMin", 2, false )]
        public virtual BigInteger NewMin { get; set; }
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

    public partial class WithdrawnEventDTO : WithdrawnEventDTOBase { }

    [Event("Withdrawn")]
    public class WithdrawnEventDTOBase : IEventDTO
    {
        [Parameter("address", "account", 1, true )]
        public virtual string Account { get; set; }
        [Parameter("address", "to", 2, true )]
        public virtual string To { get; set; }
        [Parameter("uint256", "amount", 3, false )]
        public virtual BigInteger Amount { get; set; }
    }

    public partial class InsufficientDepositError : InsufficientDepositErrorBase { }
    [Error("InsufficientDeposit")]
    public class InsufficientDepositErrorBase : IErrorDTO
    {
    }

    public partial class InsufficientUserDepositError : InsufficientUserDepositErrorBase { }
    [Error("InsufficientUserDeposit")]
    public class InsufficientUserDepositErrorBase : IErrorDTO
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

    public partial class ReentrancyGuardReentrantCallError : ReentrancyGuardReentrantCallErrorBase { }
    [Error("ReentrancyGuardReentrantCall")]
    public class ReentrancyGuardReentrantCallErrorBase : IErrorDTO
    {
    }

    public partial class WithdrawFailedError : WithdrawFailedErrorBase { }
    [Error("WithdrawFailed")]
    public class WithdrawFailedErrorBase : IErrorDTO
    {
    }
}
