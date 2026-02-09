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
using Nethereum.AccountAbstraction.Contracts.Interfaces.SmartSessions.ISmartSession.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Interfaces.SmartSessions.ISmartSession.ContractDefinition
{


    public partial class ISmartSessionDeployment : ISmartSessionDeploymentBase
    {
        public ISmartSessionDeployment() : base(BYTECODE) { }
        public ISmartSessionDeployment(string byteCode) : base(byteCode) { }
    }

    public class ISmartSessionDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x";
        public ISmartSessionDeploymentBase() : base(BYTECODE) { }
        public ISmartSessionDeploymentBase(string byteCode) : base(byteCode) { }

    }

    public partial class DisableActionIdFunction : DisableActionIdFunctionBase { }

    [Function("disableActionId")]
    public class DisableActionIdFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "permissionId", 1)]
        public virtual byte[] PermissionId { get; set; }
        [Parameter("bytes32", "actionId", 2)]
        public virtual byte[] ActionId { get; set; }
    }

    public partial class DisableActionPoliciesFunction : DisableActionPoliciesFunctionBase { }

    [Function("disableActionPolicies")]
    public class DisableActionPoliciesFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "permissionId", 1)]
        public virtual byte[] PermissionId { get; set; }
        [Parameter("bytes32", "actionId", 2)]
        public virtual byte[] ActionId { get; set; }
        [Parameter("address[]", "policies", 3)]
        public virtual List<string> Policies { get; set; }
    }

    public partial class DisableERC1271PoliciesFunction : DisableERC1271PoliciesFunctionBase { }

    [Function("disableERC1271Policies")]
    public class DisableERC1271PoliciesFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "permissionId", 1)]
        public virtual byte[] PermissionId { get; set; }
        [Parameter("address[]", "policies", 2)]
        public virtual List<string> Policies { get; set; }
        [Parameter("tuple[]", "contexts", 3)]
        public virtual List<ERC7739Context> Contexts { get; set; }
    }

    public partial class DisableUserOpPoliciesFunction : DisableUserOpPoliciesFunctionBase { }

    [Function("disableUserOpPolicies")]
    public class DisableUserOpPoliciesFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "permissionId", 1)]
        public virtual byte[] PermissionId { get; set; }
        [Parameter("address[]", "policies", 2)]
        public virtual List<string> Policies { get; set; }
    }

    public partial class EnableActionPoliciesFunction : EnableActionPoliciesFunctionBase { }

    [Function("enableActionPolicies")]
    public class EnableActionPoliciesFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "permissionId", 1)]
        public virtual byte[] PermissionId { get; set; }
        [Parameter("tuple[]", "actionPolicies", 2)]
        public virtual List<ActionData> ActionPolicies { get; set; }
    }

    public partial class EnableERC1271PoliciesFunction : EnableERC1271PoliciesFunctionBase { }

    [Function("enableERC1271Policies")]
    public class EnableERC1271PoliciesFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "permissionId", 1)]
        public virtual byte[] PermissionId { get; set; }
        [Parameter("tuple", "erc1271Policies", 2)]
        public virtual ERC7739Data Erc1271Policies { get; set; }
    }

    public partial class EnableSessionsFunction : EnableSessionsFunctionBase { }

    [Function("enableSessions", "bytes32[]")]
    public class EnableSessionsFunctionBase : FunctionMessage
    {
        [Parameter("tuple[]", "sessions", 1)]
        public virtual List<Session> Sessions { get; set; }
    }

    public partial class EnableUserOpPoliciesFunction : EnableUserOpPoliciesFunctionBase { }

    [Function("enableUserOpPolicies")]
    public class EnableUserOpPoliciesFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "permissionId", 1)]
        public virtual byte[] PermissionId { get; set; }
        [Parameter("tuple[]", "userOpPolicies", 2)]
        public virtual List<PolicyData> UserOpPolicies { get; set; }
    }

    public partial class GetNonceFunction : GetNonceFunctionBase { }

    [Function("getNonce", "uint256")]
    public class GetNonceFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "permissionId", 1)]
        public virtual byte[] PermissionId { get; set; }
        [Parameter("address", "account", 2)]
        public virtual string Account { get; set; }
    }

    public partial class GetPermissionIDsFunction : GetPermissionIDsFunctionBase { }

    [Function("getPermissionIDs", "bytes32[]")]
    public class GetPermissionIDsFunctionBase : FunctionMessage
    {
        [Parameter("address", "account", 1)]
        public virtual string Account { get; set; }
    }

    public partial class GetPermissionIdFunction : GetPermissionIdFunctionBase { }

    [Function("getPermissionId", "bytes32")]
    public class GetPermissionIdFunctionBase : FunctionMessage
    {
        [Parameter("tuple", "session", 1)]
        public virtual Session Session { get; set; }
    }

    public partial class GetSessionDigestFunction : GetSessionDigestFunctionBase { }

    [Function("getSessionDigest", "bytes32")]
    public class GetSessionDigestFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "permissionId", 1)]
        public virtual byte[] PermissionId { get; set; }
        [Parameter("address", "account", 2)]
        public virtual string Account { get; set; }
        [Parameter("tuple", "data", 3)]
        public virtual Session Data { get; set; }
        [Parameter("uint8", "mode", 4)]
        public virtual byte Mode { get; set; }
    }

    public partial class IsInitializedFunction : IsInitializedFunctionBase { }

    [Function("isInitialized", "bool")]
    public class IsInitializedFunctionBase : FunctionMessage
    {
        [Parameter("address", "smartAccount", 1)]
        public virtual string SmartAccount { get; set; }
    }

    public partial class IsModuleTypeFunction : IsModuleTypeFunctionBase { }

    [Function("isModuleType", "bool")]
    public class IsModuleTypeFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "typeID", 1)]
        public virtual BigInteger TypeID { get; set; }
    }

    public partial class IsPermissionEnabledFunction : IsPermissionEnabledFunctionBase { }

    [Function("isPermissionEnabled", "bool")]
    public class IsPermissionEnabledFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "permissionId", 1)]
        public virtual byte[] PermissionId { get; set; }
        [Parameter("address", "account", 2)]
        public virtual string Account { get; set; }
    }

    public partial class IsValidSignatureWithSenderFunction : IsValidSignatureWithSenderFunctionBase { }

    [Function("isValidSignatureWithSender", "bytes4")]
    public class IsValidSignatureWithSenderFunctionBase : FunctionMessage
    {
        [Parameter("address", "sender", 1)]
        public virtual string Sender { get; set; }
        [Parameter("bytes32", "hash", 2)]
        public virtual byte[] Hash { get; set; }
        [Parameter("bytes", "signature", 3)]
        public virtual byte[] Signature { get; set; }
    }

    public partial class OnInstallFunction : OnInstallFunctionBase { }

    [Function("onInstall")]
    public class OnInstallFunctionBase : FunctionMessage
    {
        [Parameter("bytes", "data", 1)]
        public virtual byte[] Data { get; set; }
    }

    public partial class OnUninstallFunction : OnUninstallFunctionBase { }

    [Function("onUninstall")]
    public class OnUninstallFunctionBase : FunctionMessage
    {
        [Parameter("bytes", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class RemoveSessionFunction : RemoveSessionFunctionBase { }

    [Function("removeSession")]
    public class RemoveSessionFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "permissionId", 1)]
        public virtual byte[] PermissionId { get; set; }
    }

    public partial class RevokeEnableSignatureFunction : RevokeEnableSignatureFunctionBase { }

    [Function("revokeEnableSignature")]
    public class RevokeEnableSignatureFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "permissionId", 1)]
        public virtual byte[] PermissionId { get; set; }
    }

    public partial class ValidateUserOpFunction : ValidateUserOpFunctionBase { }

    [Function("validateUserOp", "uint256")]
    public class ValidateUserOpFunctionBase : FunctionMessage
    {
        [Parameter("tuple", "userOp", 1)]
        public virtual PackedUserOperation UserOp { get; set; }
        [Parameter("bytes32", "userOpHash", 2)]
        public virtual byte[] UserOpHash { get; set; }
    }

















    public partial class GetNonceOutputDTO : GetNonceOutputDTOBase { }

    [FunctionOutput]
    public class GetNonceOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class GetPermissionIDsOutputDTO : GetPermissionIDsOutputDTOBase { }

    [FunctionOutput]
    public class GetPermissionIDsOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes32[]", "permissionIds", 1)]
        public virtual List<byte[]> PermissionIds { get; set; }
    }

    public partial class GetPermissionIdOutputDTO : GetPermissionIdOutputDTOBase { }

    [FunctionOutput]
    public class GetPermissionIdOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes32", "permissionId", 1)]
        public virtual byte[] PermissionId { get; set; }
    }

    public partial class GetSessionDigestOutputDTO : GetSessionDigestOutputDTOBase { }

    [FunctionOutput]
    public class GetSessionDigestOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class IsInitializedOutputDTO : IsInitializedOutputDTOBase { }

    [FunctionOutput]
    public class IsInitializedOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class IsModuleTypeOutputDTO : IsModuleTypeOutputDTOBase { }

    [FunctionOutput]
    public class IsModuleTypeOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class IsPermissionEnabledOutputDTO : IsPermissionEnabledOutputDTOBase { }

    [FunctionOutput]
    public class IsPermissionEnabledOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class IsValidSignatureWithSenderOutputDTO : IsValidSignatureWithSenderOutputDTOBase { }

    [FunctionOutput]
    public class IsValidSignatureWithSenderOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes4", "result", 1)]
        public virtual byte[] Result { get; set; }
    }











    public partial class ActionIdDisabledEventDTO : ActionIdDisabledEventDTOBase { }

    [Event("ActionIdDisabled")]
    public class ActionIdDisabledEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "permissionId", 1, false )]
        public virtual byte[] PermissionId { get; set; }
        [Parameter("bytes32", "actionId", 2, false )]
        public virtual byte[] ActionId { get; set; }
        [Parameter("address", "smartAccount", 3, false )]
        public virtual string SmartAccount { get; set; }
    }

    public partial class NonceIteratedEventDTO : NonceIteratedEventDTOBase { }

    [Event("NonceIterated")]
    public class NonceIteratedEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "permissionId", 1, false )]
        public virtual byte[] PermissionId { get; set; }
        [Parameter("address", "account", 2, false )]
        public virtual string Account { get; set; }
        [Parameter("uint256", "newValue", 3, false )]
        public virtual BigInteger NewValue { get; set; }
    }

    public partial class PermissionIdPermit4337PaymasterEventDTO : PermissionIdPermit4337PaymasterEventDTOBase { }

    [Event("PermissionIdPermit4337Paymaster")]
    public class PermissionIdPermit4337PaymasterEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "permissionId", 1, false )]
        public virtual byte[] PermissionId { get; set; }
        [Parameter("address", "smartAccount", 2, false )]
        public virtual string SmartAccount { get; set; }
        [Parameter("bool", "enabled", 3, false )]
        public virtual bool Enabled { get; set; }
    }

    public partial class PolicyDisabledEventDTO : PolicyDisabledEventDTOBase { }

    [Event("PolicyDisabled")]
    public class PolicyDisabledEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "permissionId", 1, false )]
        public virtual byte[] PermissionId { get; set; }
        [Parameter("uint8", "policyType", 2, false )]
        public virtual byte PolicyType { get; set; }
        [Parameter("address", "policy", 3, false )]
        public virtual string Policy { get; set; }
        [Parameter("address", "smartAccount", 4, false )]
        public virtual string SmartAccount { get; set; }
    }

    public partial class PolicyEnabledEventDTO : PolicyEnabledEventDTOBase { }

    [Event("PolicyEnabled")]
    public class PolicyEnabledEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "permissionId", 1, false )]
        public virtual byte[] PermissionId { get; set; }
        [Parameter("uint8", "policyType", 2, false )]
        public virtual byte PolicyType { get; set; }
        [Parameter("address", "policy", 3, false )]
        public virtual string Policy { get; set; }
        [Parameter("address", "smartAccount", 4, false )]
        public virtual string SmartAccount { get; set; }
    }

    public partial class SessionCreatedEventDTO : SessionCreatedEventDTOBase { }

    [Event("SessionCreated")]
    public class SessionCreatedEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "permissionId", 1, false )]
        public virtual byte[] PermissionId { get; set; }
        [Parameter("address", "account", 2, false )]
        public virtual string Account { get; set; }
    }

    public partial class SessionRemovedEventDTO : SessionRemovedEventDTOBase { }

    [Event("SessionRemoved")]
    public class SessionRemovedEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "permissionId", 1, false )]
        public virtual byte[] PermissionId { get; set; }
        [Parameter("address", "smartAccount", 2, false )]
        public virtual string SmartAccount { get; set; }
    }

    public partial class SessionValidatorDisabledEventDTO : SessionValidatorDisabledEventDTOBase { }

    [Event("SessionValidatorDisabled")]
    public class SessionValidatorDisabledEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "permissionId", 1, false )]
        public virtual byte[] PermissionId { get; set; }
        [Parameter("address", "sessionValidator", 2, false )]
        public virtual string SessionValidator { get; set; }
        [Parameter("address", "smartAccount", 3, false )]
        public virtual string SmartAccount { get; set; }
    }

    public partial class SessionValidatorEnabledEventDTO : SessionValidatorEnabledEventDTOBase { }

    [Event("SessionValidatorEnabled")]
    public class SessionValidatorEnabledEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "permissionId", 1, false )]
        public virtual byte[] PermissionId { get; set; }
        [Parameter("address", "sessionValidator", 2, false )]
        public virtual string SessionValidator { get; set; }
        [Parameter("address", "smartAccount", 3, false )]
        public virtual string SmartAccount { get; set; }
    }

    public partial class AssociatedarrayOutofboundsError : AssociatedarrayOutofboundsErrorBase { }

    [Error("AssociatedArray_OutOfBounds")]
    public class AssociatedarrayOutofboundsErrorBase : IErrorDTO
    {
        [Parameter("uint256", "index", 1)]
        public virtual BigInteger Index { get; set; }
    }

    public partial class ChainIdMismatchError : ChainIdMismatchErrorBase { }

    [Error("ChainIdMismatch")]
    public class ChainIdMismatchErrorBase : IErrorDTO
    {
        [Parameter("uint64", "providedChainId", 1)]
        public virtual ulong ProvidedChainId { get; set; }
    }

    public partial class ForbiddenValidationDataError : ForbiddenValidationDataErrorBase { }
    [Error("ForbiddenValidationData")]
    public class ForbiddenValidationDataErrorBase : IErrorDTO
    {
    }

    public partial class HashIndexOutOfBoundsError : HashIndexOutOfBoundsErrorBase { }

    [Error("HashIndexOutOfBounds")]
    public class HashIndexOutOfBoundsErrorBase : IErrorDTO
    {
        [Parameter("uint256", "index", 1)]
        public virtual BigInteger Index { get; set; }
    }

    public partial class HashMismatchError : HashMismatchErrorBase { }

    [Error("HashMismatch")]
    public class HashMismatchErrorBase : IErrorDTO
    {
        [Parameter("bytes32", "providedHash", 1)]
        public virtual byte[] ProvidedHash { get; set; }
        [Parameter("bytes32", "computedHash", 2)]
        public virtual byte[] ComputedHash { get; set; }
    }

    public partial class InvalidActionIdError : InvalidActionIdErrorBase { }
    [Error("InvalidActionId")]
    public class InvalidActionIdErrorBase : IErrorDTO
    {
    }

    public partial class InvalidCallTargetError : InvalidCallTargetErrorBase { }
    [Error("InvalidCallTarget")]
    public class InvalidCallTargetErrorBase : IErrorDTO
    {
    }

    public partial class InvalidDataError : InvalidDataErrorBase { }
    [Error("InvalidData")]
    public class InvalidDataErrorBase : IErrorDTO
    {
    }

    public partial class InvalidEnableSignatureError : InvalidEnableSignatureErrorBase { }

    [Error("InvalidEnableSignature")]
    public class InvalidEnableSignatureErrorBase : IErrorDTO
    {
        [Parameter("address", "account", 1)]
        public virtual string Account { get; set; }
        [Parameter("bytes32", "hash", 2)]
        public virtual byte[] Hash { get; set; }
    }

    public partial class InvalidISessionValidatorError : InvalidISessionValidatorErrorBase { }

    [Error("InvalidISessionValidator")]
    public class InvalidISessionValidatorErrorBase : IErrorDTO
    {
        [Parameter("address", "sessionValidator", 1)]
        public virtual string SessionValidator { get; set; }
    }

    public partial class InvalidModeError : InvalidModeErrorBase { }
    [Error("InvalidMode")]
    public class InvalidModeErrorBase : IErrorDTO
    {
    }

    public partial class InvalidPermissionIdError : InvalidPermissionIdErrorBase { }

    [Error("InvalidPermissionId")]
    public class InvalidPermissionIdErrorBase : IErrorDTO
    {
        [Parameter("bytes32", "permissionId", 1)]
        public virtual byte[] PermissionId { get; set; }
    }

    public partial class InvalidSelfCallError : InvalidSelfCallErrorBase { }
    [Error("InvalidSelfCall")]
    public class InvalidSelfCallErrorBase : IErrorDTO
    {
    }

    public partial class InvalidSessionError : InvalidSessionErrorBase { }

    [Error("InvalidSession")]
    public class InvalidSessionErrorBase : IErrorDTO
    {
        [Parameter("bytes32", "permissionId", 1)]
        public virtual byte[] PermissionId { get; set; }
    }

    public partial class InvalidSessionKeySignatureError : InvalidSessionKeySignatureErrorBase { }

    [Error("InvalidSessionKeySignature")]
    public class InvalidSessionKeySignatureErrorBase : IErrorDTO
    {
        [Parameter("bytes32", "permissionId", 1)]
        public virtual byte[] PermissionId { get; set; }
        [Parameter("address", "sessionValidator", 2)]
        public virtual string SessionValidator { get; set; }
        [Parameter("address", "account", 3)]
        public virtual string Account { get; set; }
        [Parameter("bytes32", "userOpHash", 4)]
        public virtual byte[] UserOpHash { get; set; }
    }

    public partial class InvalidTargetError : InvalidTargetErrorBase { }
    [Error("InvalidTarget")]
    public class InvalidTargetErrorBase : IErrorDTO
    {
    }

    public partial class InvalidUserOpSenderError : InvalidUserOpSenderErrorBase { }

    [Error("InvalidUserOpSender")]
    public class InvalidUserOpSenderErrorBase : IErrorDTO
    {
        [Parameter("address", "sender", 1)]
        public virtual string Sender { get; set; }
    }

    public partial class NoExecutionsInBatchError : NoExecutionsInBatchErrorBase { }
    [Error("NoExecutionsInBatch")]
    public class NoExecutionsInBatchErrorBase : IErrorDTO
    {
    }

    public partial class NoPoliciesSetError : NoPoliciesSetErrorBase { }

    [Error("NoPoliciesSet")]
    public class NoPoliciesSetErrorBase : IErrorDTO
    {
        [Parameter("bytes32", "permissionId", 1)]
        public virtual byte[] PermissionId { get; set; }
    }

    public partial class PartlyEnabledActionsError : PartlyEnabledActionsErrorBase { }
    [Error("PartlyEnabledActions")]
    public class PartlyEnabledActionsErrorBase : IErrorDTO
    {
    }

    public partial class PartlyEnabledPoliciesError : PartlyEnabledPoliciesErrorBase { }
    [Error("PartlyEnabledPolicies")]
    public class PartlyEnabledPoliciesErrorBase : IErrorDTO
    {
    }

    public partial class PaymasterValidationNotEnabledError : PaymasterValidationNotEnabledErrorBase { }

    [Error("PaymasterValidationNotEnabled")]
    public class PaymasterValidationNotEnabledErrorBase : IErrorDTO
    {
        [Parameter("bytes32", "permissionId", 1)]
        public virtual byte[] PermissionId { get; set; }
    }

    public partial class PolicyViolationError : PolicyViolationErrorBase { }

    [Error("PolicyViolation")]
    public class PolicyViolationErrorBase : IErrorDTO
    {
        [Parameter("bytes32", "permissionId", 1)]
        public virtual byte[] PermissionId { get; set; }
        [Parameter("address", "policy", 2)]
        public virtual string Policy { get; set; }
    }

    public partial class SignerNotFoundError : SignerNotFoundErrorBase { }

    [Error("SignerNotFound")]
    public class SignerNotFoundErrorBase : IErrorDTO
    {
        [Parameter("bytes32", "permissionId", 1)]
        public virtual byte[] PermissionId { get; set; }
        [Parameter("address", "account", 2)]
        public virtual string Account { get; set; }
    }

    public partial class SmartSessionModuleAlreadyInstalledError : SmartSessionModuleAlreadyInstalledErrorBase { }

    [Error("SmartSessionModuleAlreadyInstalled")]
    public class SmartSessionModuleAlreadyInstalledErrorBase : IErrorDTO
    {
        [Parameter("address", "account", 1)]
        public virtual string Account { get; set; }
    }

    public partial class UnsupportedExecutionTypeError : UnsupportedExecutionTypeErrorBase { }
    [Error("UnsupportedExecutionType")]
    public class UnsupportedExecutionTypeErrorBase : IErrorDTO
    {
    }

    public partial class UnsupportedPolicyError : UnsupportedPolicyErrorBase { }

    [Error("UnsupportedPolicy")]
    public class UnsupportedPolicyErrorBase : IErrorDTO
    {
        [Parameter("address", "policy", 1)]
        public virtual string Policy { get; set; }
    }

    public partial class UnsupportedSmartSessionModeError : UnsupportedSmartSessionModeErrorBase { }

    [Error("UnsupportedSmartSessionMode")]
    public class UnsupportedSmartSessionModeErrorBase : IErrorDTO
    {
        [Parameter("uint8", "mode", 1)]
        public virtual byte Mode { get; set; }
    }
}
