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
using Nethereum.AccountAbstraction.AppChain.Contracts.Interfaces.IAppChainAccountRegistry.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.AppChain.Contracts.Interfaces.IAppChainAccountRegistry.ContractDefinition
{


    public partial class IAppChainAccountRegistryDeployment : IAppChainAccountRegistryDeploymentBase
    {
        public IAppChainAccountRegistryDeployment() : base(BYTECODE) { }
        public IAppChainAccountRegistryDeployment(string byteCode) : base(byteCode) { }
    }

    public class IAppChainAccountRegistryDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x";
        public IAppChainAccountRegistryDeploymentBase() : base(BYTECODE) { }
        public IAppChainAccountRegistryDeploymentBase(string byteCode) : base(byteCode) { }

    }

    public partial class ActivateFunction : ActivateFunctionBase { }

    [Function("activate")]
    public class ActivateFunctionBase : FunctionMessage
    {

    }

    public partial class ActivateAccountFunction : ActivateAccountFunctionBase { }

    [Function("activateAccount")]
    public class ActivateAccountFunctionBase : FunctionMessage
    {
        [Parameter("address", "account", 1)]
        public virtual string Account { get; set; }
    }

    public partial class BanFunction : BanFunctionBase { }

    [Function("ban")]
    public class BanFunctionBase : FunctionMessage
    {
        [Parameter("address", "account", 1)]
        public virtual string Account { get; set; }
        [Parameter("string", "reason", 2)]
        public virtual string Reason { get; set; }
    }

    public partial class CheckQuotaFunction : CheckQuotaFunctionBase { }

    [Function("checkQuota", typeof(CheckQuotaOutputDTO))]
    public class CheckQuotaFunctionBase : FunctionMessage
    {
        [Parameter("address", "account", 1)]
        public virtual string Account { get; set; }
        [Parameter("uint256", "gasEstimate", 2)]
        public virtual BigInteger GasEstimate { get; set; }
        [Parameter("uint256", "valueEstimate", 3)]
        public virtual BigInteger ValueEstimate { get; set; }
    }

    public partial class DefaultGasQuotaFunction : DefaultGasQuotaFunctionBase { }

    [Function("defaultGasQuota", "uint256")]
    public class DefaultGasQuotaFunctionBase : FunctionMessage
    {

    }

    public partial class DefaultOpQuotaFunction : DefaultOpQuotaFunctionBase { }

    [Function("defaultOpQuota", "uint32")]
    public class DefaultOpQuotaFunctionBase : FunctionMessage
    {

    }

    public partial class DefaultValueQuotaFunction : DefaultValueQuotaFunctionBase { }

    [Function("defaultValueQuota", "uint256")]
    public class DefaultValueQuotaFunctionBase : FunctionMessage
    {

    }

    public partial class GetAccountCountFunction : GetAccountCountFunctionBase { }

    [Function("getAccountCount", "uint256")]
    public class GetAccountCountFunctionBase : FunctionMessage
    {

    }

    public partial class GetAccountInfoFunction : GetAccountInfoFunctionBase { }

    [Function("getAccountInfo", typeof(GetAccountInfoOutputDTO))]
    public class GetAccountInfoFunctionBase : FunctionMessage
    {
        [Parameter("address", "account", 1)]
        public virtual string Account { get; set; }
    }

    public partial class GetAccountsFunction : GetAccountsFunctionBase { }

    [Function("getAccounts", "address[]")]
    public class GetAccountsFunctionBase : FunctionMessage
    {

    }

    public partial class GetRemainingQuotaFunction : GetRemainingQuotaFunctionBase { }

    [Function("getRemainingQuota", typeof(GetRemainingQuotaOutputDTO))]
    public class GetRemainingQuotaFunctionBase : FunctionMessage
    {
        [Parameter("address", "account", 1)]
        public virtual string Account { get; set; }
    }

    public partial class GetStatusFunction : GetStatusFunctionBase { }

    [Function("getStatus", "uint8")]
    public class GetStatusFunctionBase : FunctionMessage
    {
        [Parameter("address", "account", 1)]
        public virtual string Account { get; set; }
    }

    public partial class InviteFunction : InviteFunctionBase { }

    [Function("invite")]
    public class InviteFunctionBase : FunctionMessage
    {
        [Parameter("address", "account", 1)]
        public virtual string Account { get; set; }
    }

    public partial class InviteBatchFunction : InviteBatchFunctionBase { }

    [Function("inviteBatch")]
    public class InviteBatchFunctionBase : FunctionMessage
    {
        [Parameter("address[]", "accounts", 1)]
        public virtual List<string> Accounts { get; set; }
    }

    public partial class InviteRequiredFunction : InviteRequiredFunctionBase { }

    [Function("inviteRequired", "bool")]
    public class InviteRequiredFunctionBase : FunctionMessage
    {

    }

    public partial class IsActiveFunction : IsActiveFunctionBase { }

    [Function("isActive", "bool")]
    public class IsActiveFunctionBase : FunctionMessage
    {
        [Parameter("address", "account", 1)]
        public virtual string Account { get; set; }
    }

    public partial class ResetQuotaFunction : ResetQuotaFunctionBase { }

    [Function("resetQuota")]
    public class ResetQuotaFunctionBase : FunctionMessage
    {
        [Parameter("address", "account", 1)]
        public virtual string Account { get; set; }
    }

    public partial class SelfActivationEnabledFunction : SelfActivationEnabledFunctionBase { }

    [Function("selfActivationEnabled", "bool")]
    public class SelfActivationEnabledFunctionBase : FunctionMessage
    {

    }

    public partial class SetDefaultQuotasFunction : SetDefaultQuotasFunctionBase { }

    [Function("setDefaultQuotas")]
    public class SetDefaultQuotasFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "gasQuota", 1)]
        public virtual BigInteger GasQuota { get; set; }
        [Parameter("uint32", "opQuota", 2)]
        public virtual uint OpQuota { get; set; }
        [Parameter("uint256", "valueQuota", 3)]
        public virtual BigInteger ValueQuota { get; set; }
    }

    public partial class SetInviteRequiredFunction : SetInviteRequiredFunctionBase { }

    [Function("setInviteRequired")]
    public class SetInviteRequiredFunctionBase : FunctionMessage
    {
        [Parameter("bool", "required", 1)]
        public virtual bool Required { get; set; }
    }

    public partial class SetQuotaFunction : SetQuotaFunctionBase { }

    [Function("setQuota")]
    public class SetQuotaFunctionBase : FunctionMessage
    {
        [Parameter("address", "account", 1)]
        public virtual string Account { get; set; }
        [Parameter("uint256", "gasQuota", 2)]
        public virtual BigInteger GasQuota { get; set; }
        [Parameter("uint32", "opQuota", 3)]
        public virtual uint OpQuota { get; set; }
        [Parameter("uint256", "valueQuota", 4)]
        public virtual BigInteger ValueQuota { get; set; }
    }

    public partial class SetSelfActivationEnabledFunction : SetSelfActivationEnabledFunctionBase { }

    [Function("setSelfActivationEnabled")]
    public class SetSelfActivationEnabledFunctionBase : FunctionMessage
    {
        [Parameter("bool", "enabled", 1)]
        public virtual bool Enabled { get; set; }
    }

    public partial class SuspendFunction : SuspendFunctionBase { }

    [Function("suspend")]
    public class SuspendFunctionBase : FunctionMessage
    {
        [Parameter("address", "account", 1)]
        public virtual string Account { get; set; }
        [Parameter("uint64", "until", 2)]
        public virtual ulong Until { get; set; }
    }

    public partial class UnbanFunction : UnbanFunctionBase { }

    [Function("unban")]
    public class UnbanFunctionBase : FunctionMessage
    {
        [Parameter("address", "account", 1)]
        public virtual string Account { get; set; }
    }

    public partial class UnsuspendFunction : UnsuspendFunctionBase { }

    [Function("unsuspend")]
    public class UnsuspendFunctionBase : FunctionMessage
    {
        [Parameter("address", "account", 1)]
        public virtual string Account { get; set; }
    }

    public partial class UseQuotaFunction : UseQuotaFunctionBase { }

    [Function("useQuota", "bool")]
    public class UseQuotaFunctionBase : FunctionMessage
    {
        [Parameter("address", "account", 1)]
        public virtual string Account { get; set; }
        [Parameter("uint256", "gasUsed", 2)]
        public virtual BigInteger GasUsed { get; set; }
        [Parameter("uint256", "valueUsed", 3)]
        public virtual BigInteger ValueUsed { get; set; }
    }







    public partial class CheckQuotaOutputDTO : CheckQuotaOutputDTOBase { }

    [FunctionOutput]
    public class CheckQuotaOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "allowed", 1)]
        public virtual bool Allowed { get; set; }
        [Parameter("string", "reason", 2)]
        public virtual string Reason { get; set; }
    }

    public partial class DefaultGasQuotaOutputDTO : DefaultGasQuotaOutputDTOBase { }

    [FunctionOutput]
    public class DefaultGasQuotaOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class DefaultOpQuotaOutputDTO : DefaultOpQuotaOutputDTOBase { }

    [FunctionOutput]
    public class DefaultOpQuotaOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint32", "", 1)]
        public virtual uint ReturnValue1 { get; set; }
    }

    public partial class DefaultValueQuotaOutputDTO : DefaultValueQuotaOutputDTOBase { }

    [FunctionOutput]
    public class DefaultValueQuotaOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class GetAccountCountOutputDTO : GetAccountCountOutputDTOBase { }

    [FunctionOutput]
    public class GetAccountCountOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class GetAccountInfoOutputDTO : GetAccountInfoOutputDTOBase { }

    [FunctionOutput]
    public class GetAccountInfoOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("tuple", "", 1)]
        public virtual AccountInfo ReturnValue1 { get; set; }
    }

    public partial class GetAccountsOutputDTO : GetAccountsOutputDTOBase { }

    [FunctionOutput]
    public class GetAccountsOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address[]", "", 1)]
        public virtual List<string> ReturnValue1 { get; set; }
    }

    public partial class GetRemainingQuotaOutputDTO : GetRemainingQuotaOutputDTOBase { }

    [FunctionOutput]
    public class GetRemainingQuotaOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "remainingGas", 1)]
        public virtual BigInteger RemainingGas { get; set; }
        [Parameter("uint32", "remainingOps", 2)]
        public virtual uint RemainingOps { get; set; }
        [Parameter("uint256", "remainingValue", 3)]
        public virtual BigInteger RemainingValue { get; set; }
    }

    public partial class GetStatusOutputDTO : GetStatusOutputDTOBase { }

    [FunctionOutput]
    public class GetStatusOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint8", "", 1)]
        public virtual byte ReturnValue1 { get; set; }
    }





    public partial class InviteRequiredOutputDTO : InviteRequiredOutputDTOBase { }

    [FunctionOutput]
    public class InviteRequiredOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class IsActiveOutputDTO : IsActiveOutputDTOBase { }

    [FunctionOutput]
    public class IsActiveOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }



    public partial class SelfActivationEnabledOutputDTO : SelfActivationEnabledOutputDTOBase { }

    [FunctionOutput]
    public class SelfActivationEnabledOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

















    public partial class AccountActivatedEventDTO : AccountActivatedEventDTOBase { }

    [Event("AccountActivated")]
    public class AccountActivatedEventDTOBase : IEventDTO
    {
        [Parameter("address", "account", 1, true )]
        public virtual string Account { get; set; }
    }

    public partial class AccountBannedEventDTO : AccountBannedEventDTOBase { }

    [Event("AccountBanned")]
    public class AccountBannedEventDTOBase : IEventDTO
    {
        [Parameter("address", "account", 1, true )]
        public virtual string Account { get; set; }
        [Parameter("address", "bannedBy", 2, true )]
        public virtual string BannedBy { get; set; }
        [Parameter("string", "reason", 3, false )]
        public virtual string Reason { get; set; }
    }

    public partial class AccountInvitedEventDTO : AccountInvitedEventDTOBase { }

    [Event("AccountInvited")]
    public class AccountInvitedEventDTOBase : IEventDTO
    {
        [Parameter("address", "account", 1, true )]
        public virtual string Account { get; set; }
        [Parameter("address", "invitedBy", 2, true )]
        public virtual string InvitedBy { get; set; }
    }

    public partial class AccountSuspendedEventDTO : AccountSuspendedEventDTOBase { }

    [Event("AccountSuspended")]
    public class AccountSuspendedEventDTOBase : IEventDTO
    {
        [Parameter("address", "account", 1, true )]
        public virtual string Account { get; set; }
        [Parameter("address", "suspendedBy", 2, true )]
        public virtual string SuspendedBy { get; set; }
        [Parameter("uint64", "until", 3, false )]
        public virtual ulong Until { get; set; }
    }

    public partial class AccountUnbannedEventDTO : AccountUnbannedEventDTOBase { }

    [Event("AccountUnbanned")]
    public class AccountUnbannedEventDTOBase : IEventDTO
    {
        [Parameter("address", "account", 1, true )]
        public virtual string Account { get; set; }
        [Parameter("address", "unbannedBy", 2, true )]
        public virtual string UnbannedBy { get; set; }
    }

    public partial class AccountUnsuspendedEventDTO : AccountUnsuspendedEventDTOBase { }

    [Event("AccountUnsuspended")]
    public class AccountUnsuspendedEventDTOBase : IEventDTO
    {
        [Parameter("address", "account", 1, true )]
        public virtual string Account { get; set; }
        [Parameter("address", "unsuspendedBy", 2, true )]
        public virtual string UnsuspendedBy { get; set; }
    }

    public partial class QuotaResetEventDTO : QuotaResetEventDTOBase { }

    [Event("QuotaReset")]
    public class QuotaResetEventDTOBase : IEventDTO
    {
        [Parameter("address", "account", 1, true )]
        public virtual string Account { get; set; }
    }

    public partial class QuotaUpdatedEventDTO : QuotaUpdatedEventDTOBase { }

    [Event("QuotaUpdated")]
    public class QuotaUpdatedEventDTOBase : IEventDTO
    {
        [Parameter("address", "account", 1, true )]
        public virtual string Account { get; set; }
        [Parameter("uint256", "gasQuota", 2, false )]
        public virtual BigInteger GasQuota { get; set; }
        [Parameter("uint32", "opQuota", 3, false )]
        public virtual uint OpQuota { get; set; }
        [Parameter("uint256", "valueQuota", 4, false )]
        public virtual BigInteger ValueQuota { get; set; }
    }

    public partial class QuotaUsedEventDTO : QuotaUsedEventDTOBase { }

    [Event("QuotaUsed")]
    public class QuotaUsedEventDTOBase : IEventDTO
    {
        [Parameter("address", "account", 1, true )]
        public virtual string Account { get; set; }
        [Parameter("uint256", "gas", 2, false )]
        public virtual BigInteger Gas { get; set; }
        [Parameter("uint256", "value", 3, false )]
        public virtual BigInteger Value { get; set; }
    }
}
