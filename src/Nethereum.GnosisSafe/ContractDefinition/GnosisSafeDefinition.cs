using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Web3;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts.CQS;
using Nethereum.Contracts;
using System.Threading;

namespace Nethereum.GnosisSafe.ContractDefinition
{


    public partial class VersionFunction : VersionFunctionBase { }

    [Function("VERSION", "string")]
    public class VersionFunctionBase : FunctionMessage
    {

    }

    public partial class AddOwnerWithThresholdFunction : AddOwnerWithThresholdFunctionBase { }

    [Function("addOwnerWithThreshold")]
    public class AddOwnerWithThresholdFunctionBase : FunctionMessage
    {
        [Parameter("address", "owner", 1)]
        public virtual string Owner { get; set; }
        [Parameter("uint256", "_threshold", 2)]
        public virtual BigInteger Threshold { get; set; }
    }

    public partial class ApproveHashFunction : ApproveHashFunctionBase { }

    [Function("approveHash")]
    public class ApproveHashFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "hashToApprove", 1)]
        public virtual byte[] HashToApprove { get; set; }
    }

    public partial class ApprovedHashesFunction : ApprovedHashesFunctionBase { }

    [Function("approvedHashes", "uint256")]
    public class ApprovedHashesFunctionBase : FunctionMessage
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
        [Parameter("bytes32", "", 2)]
        public virtual byte[] ReturnValue2 { get; set; }
    }

    public partial class ChangeThresholdFunction : ChangeThresholdFunctionBase { }

    [Function("changeThreshold")]
    public class ChangeThresholdFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "_threshold", 1)]
        public virtual BigInteger Threshold { get; set; }
    }

    public partial class DisableModuleFunction : DisableModuleFunctionBase { }

    [Function("disableModule")]
    public class DisableModuleFunctionBase : FunctionMessage
    {
        [Parameter("address", "prevModule", 1)]
        public virtual string PrevModule { get; set; }
        [Parameter("address", "module", 2)]
        public virtual string Module { get; set; }
    }

    public partial class DomainSeparatorFunction : DomainSeparatorFunctionBase { }

    [Function("domainSeparator", "bytes32")]
    public class DomainSeparatorFunctionBase : FunctionMessage
    {

    }

    public partial class EnableModuleFunction : EnableModuleFunctionBase { }

    [Function("enableModule")]
    public class EnableModuleFunctionBase : FunctionMessage
    {
        [Parameter("address", "module", 1)]
        public virtual string Module { get; set; }
    }

    

    public partial class ExecTransactionFunction : ExecTransactionFunctionBase { }

    [Function("execTransaction", "bool")]
    public class ExecTransactionFunctionBase : FunctionMessage
    {
        [Parameter("address", "to", 1)]
        public virtual string To { get; set; }
        [Parameter("uint256", "value", 2)]
        public virtual BigInteger Value { get; set; }
        [Parameter("bytes", "data", 3)]
        public virtual byte[] Data { get; set; }
        [Parameter("uint8", "operation", 4)]
        public virtual byte Operation { get; set; }
        [Parameter("uint256", "safeTxGas", 5)]
        public virtual BigInteger SafeTxGas { get; set; }
        [Parameter("uint256", "baseGas", 6)]
        public virtual BigInteger BaseGas { get; set; }
        [Parameter("uint256", "gasPrice", 7)]
        public virtual BigInteger SafeGasPrice { get; set; }
        [Parameter("address", "gasToken", 8)]
        public virtual string GasToken { get; set; }
        [Parameter("address", "refundReceiver", 9)]
        public virtual string RefundReceiver { get; set; }
        [Parameter("bytes", "signatures", 10)]
        public virtual byte[] Signatures { get; set; }
    }

    public partial class ExecTransactionFromModuleFunction : ExecTransactionFromModuleFunctionBase { }

    [Function("execTransactionFromModule", "bool")]
    public class ExecTransactionFromModuleFunctionBase : FunctionMessage
    {
        [Parameter("address", "to", 1)]
        public virtual string To { get; set; }
        [Parameter("uint256", "value", 2)]
        public virtual BigInteger Value { get; set; }
        [Parameter("bytes", "data", 3)]
        public virtual byte[] Data { get; set; }
        [Parameter("uint8", "operation", 4)]
        public virtual byte Operation { get; set; }
    }

    public partial class ExecTransactionFromModuleReturnDataFunction : ExecTransactionFromModuleReturnDataFunctionBase { }

    [Function("execTransactionFromModuleReturnData", typeof(ExecTransactionFromModuleReturnDataOutputDTO))]
    public class ExecTransactionFromModuleReturnDataFunctionBase : FunctionMessage
    {
        [Parameter("address", "to", 1)]
        public virtual string To { get; set; }
        [Parameter("uint256", "value", 2)]
        public virtual BigInteger Value { get; set; }
        [Parameter("bytes", "data", 3)]
        public virtual byte[] Data { get; set; }
        [Parameter("uint8", "operation", 4)]
        public virtual byte Operation { get; set; }
    }

    public partial class GetChainIdFunction : GetChainIdFunctionBase { }

    [Function("getChainId", "uint256")]
    public class GetChainIdFunctionBase : FunctionMessage
    {

    }

    public partial class GetModulesPaginatedFunction : GetModulesPaginatedFunctionBase { }

    [Function("getModulesPaginated", typeof(GetModulesPaginatedOutputDTO))]
    public class GetModulesPaginatedFunctionBase : FunctionMessage
    {
        [Parameter("address", "start", 1)]
        public virtual string Start { get; set; }
        [Parameter("uint256", "pageSize", 2)]
        public virtual BigInteger PageSize { get; set; }
    }

    public partial class GetOwnersFunction : GetOwnersFunctionBase { }

    [Function("getOwners", "address[]")]
    public class GetOwnersFunctionBase : FunctionMessage
    {

    }

    public partial class GetStorageAtFunction : GetStorageAtFunctionBase { }

    [Function("getStorageAt", "bytes")]
    public class GetStorageAtFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "offset", 1)]
        public virtual BigInteger Offset { get; set; }
        [Parameter("uint256", "length", 2)]
        public virtual BigInteger Length { get; set; }
    }

    public partial class GetThresholdFunction : GetThresholdFunctionBase { }

    [Function("getThreshold", "uint256")]
    public class GetThresholdFunctionBase : FunctionMessage
    {

    }

    public partial class GetTransactionHashFunction : GetTransactionHashFunctionBase { }

    [Function("getTransactionHash", "bytes32")]
    public class GetTransactionHashFunctionBase : FunctionMessage
    {
        [Parameter("address", "to", 1)]
        public virtual string To { get; set; }
        [Parameter("uint256", "value", 2)]
        public virtual BigInteger Value { get; set; }
        [Parameter("bytes", "data", 3)]
        public virtual byte[] Data { get; set; }
        [Parameter("uint8", "operation", 4)]
        public virtual byte Operation { get; set; }
        [Parameter("uint256", "safeTxGas", 5)]
        public virtual BigInteger SafeTxGas { get; set; }
        [Parameter("uint256", "baseGas", 6)]
        public virtual BigInteger BaseGas { get; set; }
        [Parameter("uint256", "gasPrice", 7)]
        public virtual BigInteger SafeGasPrice { get; set; }
        [Parameter("address", "gasToken", 8)]
        public virtual string GasToken { get; set; }
        [Parameter("address", "refundReceiver", 9)]
        public virtual string RefundReceiver { get; set; }
        [Parameter("uint256", "_nonce", 10)]
        public virtual BigInteger SafeNonce { get; set; }
    }

    public partial class IsModuleEnabledFunction : IsModuleEnabledFunctionBase { }

    [Function("isModuleEnabled", "bool")]
    public class IsModuleEnabledFunctionBase : FunctionMessage
    {
        [Parameter("address", "module", 1)]
        public virtual string Module { get; set; }
    }

    public partial class IsOwnerFunction : IsOwnerFunctionBase { }

    [Function("isOwner", "bool")]
    public class IsOwnerFunctionBase : FunctionMessage
    {
        [Parameter("address", "owner", 1)]
        public virtual string Owner { get; set; }
    }

    public partial class NonceFunction : NonceFunctionBase { }

    [Function("nonce", "uint256")]
    public class NonceFunctionBase : FunctionMessage
    {

    }

    public partial class RemoveOwnerFunction : RemoveOwnerFunctionBase { }

    [Function("removeOwner")]
    public class RemoveOwnerFunctionBase : FunctionMessage
    {
        [Parameter("address", "prevOwner", 1)]
        public virtual string PrevOwner { get; set; }
        [Parameter("address", "owner", 2)]
        public virtual string Owner { get; set; }
        [Parameter("uint256", "_threshold", 3)]
        public virtual BigInteger Threshold { get; set; }
    }

    public partial class RequiredTxGasFunction : RequiredTxGasFunctionBase { }

    [Function("requiredTxGas", "uint256")]
    public class RequiredTxGasFunctionBase : FunctionMessage
    {
        [Parameter("address", "to", 1)]
        public virtual string To { get; set; }
        [Parameter("uint256", "value", 2)]
        public virtual BigInteger Value { get; set; }
        [Parameter("bytes", "data", 3)]
        public virtual byte[] Data { get; set; }
        [Parameter("uint8", "operation", 4)]
        public virtual byte Operation { get; set; }
    }

    public partial class SetFallbackHandlerFunction : SetFallbackHandlerFunctionBase { }

    [Function("setFallbackHandler")]
    public class SetFallbackHandlerFunctionBase : FunctionMessage
    {
        [Parameter("address", "handler", 1)]
        public virtual string Handler { get; set; }
    }

    public partial class SetGuardFunction : SetGuardFunctionBase { }

    [Function("setGuard")]
    public class SetGuardFunctionBase : FunctionMessage
    {
        [Parameter("address", "guard", 1)]
        public virtual string Guard { get; set; }
    }

    public partial class SetupFunction : SetupFunctionBase { }

    [Function("setup")]
    public class SetupFunctionBase : FunctionMessage
    {
        [Parameter("address[]", "_owners", 1)]
        public virtual List<string> Owners { get; set; }
        [Parameter("uint256", "_threshold", 2)]
        public virtual BigInteger Threshold { get; set; }
        [Parameter("address", "to", 3)]
        public virtual string To { get; set; }
        [Parameter("bytes", "data", 4)]
        public virtual byte[] Data { get; set; }
        [Parameter("address", "fallbackHandler", 5)]
        public virtual string FallbackHandler { get; set; }
        [Parameter("address", "paymentToken", 6)]
        public virtual string PaymentToken { get; set; }
        [Parameter("uint256", "payment", 7)]
        public virtual BigInteger Payment { get; set; }
        [Parameter("address", "paymentReceiver", 8)]
        public virtual string PaymentReceiver { get; set; }
    }

    public partial class SignedMessagesFunction : SignedMessagesFunctionBase { }

    [Function("signedMessages", "uint256")]
    public class SignedMessagesFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class SimulateAndRevertFunction : SimulateAndRevertFunctionBase { }

    [Function("simulateAndRevert")]
    public class SimulateAndRevertFunctionBase : FunctionMessage
    {
        [Parameter("address", "targetContract", 1)]
        public virtual string TargetContract { get; set; }
        [Parameter("bytes", "calldataPayload", 2)]
        public virtual byte[] CalldataPayload { get; set; }
    }

    public partial class SwapOwnerFunction : SwapOwnerFunctionBase { }

    [Function("swapOwner")]
    public class SwapOwnerFunctionBase : FunctionMessage
    {
        [Parameter("address", "prevOwner", 1)]
        public virtual string PrevOwner { get; set; }
        [Parameter("address", "oldOwner", 2)]
        public virtual string OldOwner { get; set; }
        [Parameter("address", "newOwner", 3)]
        public virtual string NewOwner { get; set; }
    }

    public partial class AddedOwnerEventDTO : AddedOwnerEventDTOBase { }

    [Event("AddedOwner")]
    public class AddedOwnerEventDTOBase : IEventDTO
    {
        [Parameter("address", "owner", 1, false)]
        public virtual string Owner { get; set; }
    }

    public partial class ApproveHashEventDTO : ApproveHashEventDTOBase { }

    [Event("ApproveHash")]
    public class ApproveHashEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "approvedHash", 1, true)]
        public virtual byte[] ApprovedHash { get; set; }
        [Parameter("address", "owner", 2, true)]
        public virtual string Owner { get; set; }
    }

    public partial class ChangedFallbackHandlerEventDTO : ChangedFallbackHandlerEventDTOBase { }

    [Event("ChangedFallbackHandler")]
    public class ChangedFallbackHandlerEventDTOBase : IEventDTO
    {
        [Parameter("address", "handler", 1, false)]
        public virtual string Handler { get; set; }
    }

    public partial class ChangedGuardEventDTO : ChangedGuardEventDTOBase { }

    [Event("ChangedGuard")]
    public class ChangedGuardEventDTOBase : IEventDTO
    {
        [Parameter("address", "guard", 1, false)]
        public virtual string Guard { get; set; }
    }

    public partial class ChangedThresholdEventDTO : ChangedThresholdEventDTOBase { }

    [Event("ChangedThreshold")]
    public class ChangedThresholdEventDTOBase : IEventDTO
    {
        [Parameter("uint256", "threshold", 1, false)]
        public virtual BigInteger Threshold { get; set; }
    }

    public partial class DisabledModuleEventDTO : DisabledModuleEventDTOBase { }

    [Event("DisabledModule")]
    public class DisabledModuleEventDTOBase : IEventDTO
    {
        [Parameter("address", "module", 1, false)]
        public virtual string Module { get; set; }
    }

    public partial class EnabledModuleEventDTO : EnabledModuleEventDTOBase { }

    [Event("EnabledModule")]
    public class EnabledModuleEventDTOBase : IEventDTO
    {
        [Parameter("address", "module", 1, false)]
        public virtual string Module { get; set; }
    }

    public partial class ExecutionFailureEventDTO : ExecutionFailureEventDTOBase { }

    [Event("ExecutionFailure")]
    public class ExecutionFailureEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "txHash", 1, false)]
        public virtual byte[] TxHash { get; set; }
        [Parameter("uint256", "payment", 2, false)]
        public virtual BigInteger Payment { get; set; }
    }

    public partial class ExecutionFromModuleFailureEventDTO : ExecutionFromModuleFailureEventDTOBase { }

    [Event("ExecutionFromModuleFailure")]
    public class ExecutionFromModuleFailureEventDTOBase : IEventDTO
    {
        [Parameter("address", "module", 1, true)]
        public virtual string Module { get; set; }
    }

    public partial class ExecutionFromModuleSuccessEventDTO : ExecutionFromModuleSuccessEventDTOBase { }

    [Event("ExecutionFromModuleSuccess")]
    public class ExecutionFromModuleSuccessEventDTOBase : IEventDTO
    {
        [Parameter("address", "module", 1, true)]
        public virtual string Module { get; set; }
    }

    public partial class ExecutionSuccessEventDTO : ExecutionSuccessEventDTOBase { }

    [Event("ExecutionSuccess")]
    public class ExecutionSuccessEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "txHash", 1, false)]
        public virtual byte[] TxHash { get; set; }
        [Parameter("uint256", "payment", 2, false)]
        public virtual BigInteger Payment { get; set; }
    }

    public partial class RemovedOwnerEventDTO : RemovedOwnerEventDTOBase { }

    [Event("RemovedOwner")]
    public class RemovedOwnerEventDTOBase : IEventDTO
    {
        [Parameter("address", "owner", 1, false)]
        public virtual string Owner { get; set; }
    }

    public partial class SafeReceivedEventDTO : SafeReceivedEventDTOBase { }

    [Event("SafeReceived")]
    public class SafeReceivedEventDTOBase : IEventDTO
    {
        [Parameter("address", "sender", 1, true)]
        public virtual string Sender { get; set; }
        [Parameter("uint256", "value", 2, false)]
        public virtual BigInteger Value { get; set; }
    }

    public partial class SafeSetupEventDTO : SafeSetupEventDTOBase { }

    [Event("SafeSetup")]
    public class SafeSetupEventDTOBase : IEventDTO
    {
        [Parameter("address", "initiator", 1, true)]
        public virtual string Initiator { get; set; }
        [Parameter("address[]", "owners", 2, false)]
        public virtual List<string> Owners { get; set; }
        [Parameter("uint256", "threshold", 3, false)]
        public virtual BigInteger Threshold { get; set; }
        [Parameter("address", "initializer", 4, false)]
        public virtual string Initializer { get; set; }
        [Parameter("address", "fallbackHandler", 5, false)]
        public virtual string FallbackHandler { get; set; }
    }

    public partial class SignMsgEventDTO : SignMsgEventDTOBase { }

    [Event("SignMsg")]
    public class SignMsgEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "msgHash", 1, true)]
        public virtual byte[] MsgHash { get; set; }
    }

    public partial class VersionOutputDTO : VersionOutputDTOBase { }

    [FunctionOutput]
    public class VersionOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("string", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }





    public partial class ApprovedHashesOutputDTO : ApprovedHashesOutputDTOBase { }

    [FunctionOutput]
    public class ApprovedHashesOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }





    public partial class DomainSeparatorOutputDTO : DomainSeparatorOutputDTOBase { }

    [FunctionOutput]
    public class DomainSeparatorOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }



    public partial class EncodeTransactionDataOutputDTO : EncodeTransactionDataOutputDTOBase { }

    [FunctionOutput]
    public class EncodeTransactionDataOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("bytes", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }





    public partial class ExecTransactionFromModuleReturnDataOutputDTO : ExecTransactionFromModuleReturnDataOutputDTOBase { }

    [FunctionOutput]
    public class ExecTransactionFromModuleReturnDataOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("bool", "success", 1)]
        public virtual bool Success { get; set; }
        [Parameter("bytes", "returnData", 2)]
        public virtual byte[] ReturnData { get; set; }
    }

    public partial class GetChainIdOutputDTO : GetChainIdOutputDTOBase { }

    [FunctionOutput]
    public class GetChainIdOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class GetModulesPaginatedOutputDTO : GetModulesPaginatedOutputDTOBase { }

    [FunctionOutput]
    public class GetModulesPaginatedOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("address[]", "array", 1)]
        public virtual List<string> Array { get; set; }
        [Parameter("address", "next", 2)]
        public virtual string Next { get; set; }
    }

    public partial class GetOwnersOutputDTO : GetOwnersOutputDTOBase { }

    [FunctionOutput]
    public class GetOwnersOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("address[]", "", 1)]
        public virtual List<string> ReturnValue1 { get; set; }
    }

    public partial class GetStorageAtOutputDTO : GetStorageAtOutputDTOBase { }

    [FunctionOutput]
    public class GetStorageAtOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("bytes", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class GetThresholdOutputDTO : GetThresholdOutputDTOBase { }

    [FunctionOutput]
    public class GetThresholdOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class GetTransactionHashOutputDTO : GetTransactionHashOutputDTOBase { }

    [FunctionOutput]
    public class GetTransactionHashOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class IsModuleEnabledOutputDTO : IsModuleEnabledOutputDTOBase { }

    [FunctionOutput]
    public class IsModuleEnabledOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class IsOwnerOutputDTO : IsOwnerOutputDTOBase { }

    [FunctionOutput]
    public class IsOwnerOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class NonceOutputDTO : NonceOutputDTOBase { }

    [FunctionOutput]
    public class NonceOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }



    public partial class SignedMessagesOutputDTO : SignedMessagesOutputDTOBase { }

    [FunctionOutput]
    public class SignedMessagesOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }


}
