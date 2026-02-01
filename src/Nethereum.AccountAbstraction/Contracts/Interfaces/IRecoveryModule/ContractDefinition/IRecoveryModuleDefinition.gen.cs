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
using Nethereum.AccountAbstraction.Contracts.Interfaces.IRecoveryModule.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Interfaces.IRecoveryModule.ContractDefinition
{


    public partial class IRecoveryModuleDeployment : IRecoveryModuleDeploymentBase
    {
        public IRecoveryModuleDeployment() : base(BYTECODE) { }
        public IRecoveryModuleDeployment(string byteCode) : base(byteCode) { }
    }

    public class IRecoveryModuleDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x";
        public IRecoveryModuleDeploymentBase() : base(BYTECODE) { }
        public IRecoveryModuleDeploymentBase(string byteCode) : base(byteCode) { }

    }

    public partial class ApproveRecoveryFunction : ApproveRecoveryFunctionBase { }

    [Function("approveRecovery")]
    public class ApproveRecoveryFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "recoveryId", 1)]
        public virtual byte[] RecoveryId { get; set; }
    }

    public partial class CancelRecoveryFunction : CancelRecoveryFunctionBase { }

    [Function("cancelRecovery")]
    public class CancelRecoveryFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "recoveryId", 1)]
        public virtual byte[] RecoveryId { get; set; }
    }

    public partial class ExecuteRecoveryFunction : ExecuteRecoveryFunctionBase { }

    [Function("executeRecovery")]
    public class ExecuteRecoveryFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "recoveryId", 1)]
        public virtual byte[] RecoveryId { get; set; }
    }

    public partial class GetRecoveryDelayFunction : GetRecoveryDelayFunctionBase { }

    [Function("getRecoveryDelay", "uint256")]
    public class GetRecoveryDelayFunctionBase : FunctionMessage
    {

    }

    public partial class GetRecoveryRequestFunction : GetRecoveryRequestFunctionBase { }

    [Function("getRecoveryRequest", typeof(GetRecoveryRequestOutputDTO))]
    public class GetRecoveryRequestFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "recoveryId", 1)]
        public virtual byte[] RecoveryId { get; set; }
    }

    public partial class GetRequiredApprovalsFunction : GetRequiredApprovalsFunctionBase { }

    [Function("getRequiredApprovals", "uint256")]
    public class GetRequiredApprovalsFunctionBase : FunctionMessage
    {
        [Parameter("address", "account", 1)]
        public virtual string Account { get; set; }
    }

    public partial class InitiateRecoveryFunction : InitiateRecoveryFunctionBase { }

    [Function("initiateRecovery", "bytes32")]
    public class InitiateRecoveryFunctionBase : FunctionMessage
    {
        [Parameter("address", "account", 1)]
        public virtual string Account { get; set; }
        [Parameter("address", "newOwner", 2)]
        public virtual string NewOwner { get; set; }
    }

    public partial class IsApproverFunction : IsApproverFunctionBase { }

    [Function("isApprover", "bool")]
    public class IsApproverFunctionBase : FunctionMessage
    {
        [Parameter("address", "account", 1)]
        public virtual string Account { get; set; }
        [Parameter("address", "approver", 2)]
        public virtual string Approver { get; set; }
    }







    public partial class GetRecoveryDelayOutputDTO : GetRecoveryDelayOutputDTOBase { }

    [FunctionOutput]
    public class GetRecoveryDelayOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class GetRecoveryRequestOutputDTO : GetRecoveryRequestOutputDTOBase { }

    [FunctionOutput]
    public class GetRecoveryRequestOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("tuple", "", 1)]
        public virtual RecoveryRequest ReturnValue1 { get; set; }
    }

    public partial class GetRequiredApprovalsOutputDTO : GetRequiredApprovalsOutputDTOBase { }

    [FunctionOutput]
    public class GetRequiredApprovalsOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }



    public partial class IsApproverOutputDTO : IsApproverOutputDTOBase { }

    [FunctionOutput]
    public class IsApproverOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class RecoveryApprovedEventDTO : RecoveryApprovedEventDTOBase { }

    [Event("RecoveryApproved")]
    public class RecoveryApprovedEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "recoveryId", 1, true )]
        public virtual byte[] RecoveryId { get; set; }
        [Parameter("address", "approver", 2, true )]
        public virtual string Approver { get; set; }
        [Parameter("uint32", "approvalCount", 3, false )]
        public virtual uint ApprovalCount { get; set; }
    }

    public partial class RecoveryCancelledEventDTO : RecoveryCancelledEventDTOBase { }

    [Event("RecoveryCancelled")]
    public class RecoveryCancelledEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "recoveryId", 1, true )]
        public virtual byte[] RecoveryId { get; set; }
    }

    public partial class RecoveryExecutedEventDTO : RecoveryExecutedEventDTOBase { }

    [Event("RecoveryExecuted")]
    public class RecoveryExecutedEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "recoveryId", 1, true )]
        public virtual byte[] RecoveryId { get; set; }
        [Parameter("address", "oldOwner", 2, true )]
        public virtual string OldOwner { get; set; }
        [Parameter("address", "newOwner", 3, true )]
        public virtual string NewOwner { get; set; }
    }

    public partial class RecoveryInitiatedEventDTO : RecoveryInitiatedEventDTOBase { }

    [Event("RecoveryInitiated")]
    public class RecoveryInitiatedEventDTOBase : IEventDTO
    {
        [Parameter("address", "account", 1, true )]
        public virtual string Account { get; set; }
        [Parameter("address", "newOwner", 2, true )]
        public virtual string NewOwner { get; set; }
        [Parameter("uint64", "executeAfter", 3, false )]
        public virtual ulong ExecuteAfter { get; set; }
        [Parameter("bytes32", "recoveryId", 4, true )]
        public virtual byte[] RecoveryId { get; set; }
    }
}
