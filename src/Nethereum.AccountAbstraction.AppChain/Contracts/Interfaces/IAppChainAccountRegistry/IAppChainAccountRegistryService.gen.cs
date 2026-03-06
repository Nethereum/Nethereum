using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Web3;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts.CQS;
using Nethereum.Contracts.ContractHandlers;
using Nethereum.Contracts;
using System.Threading;
using Nethereum.AccountAbstraction.AppChain.Contracts.Interfaces.IAppChainAccountRegistry.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.AppChain.Contracts.Interfaces.IAppChainAccountRegistry
{
    public partial class IAppChainAccountRegistryService: IAppChainAccountRegistryServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, IAppChainAccountRegistryDeployment iAppChainAccountRegistryDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<IAppChainAccountRegistryDeployment>().SendRequestAndWaitForReceiptAsync(iAppChainAccountRegistryDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, IAppChainAccountRegistryDeployment iAppChainAccountRegistryDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<IAppChainAccountRegistryDeployment>().SendRequestAsync(iAppChainAccountRegistryDeployment);
        }

        public static async Task<IAppChainAccountRegistryService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, IAppChainAccountRegistryDeployment iAppChainAccountRegistryDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, iAppChainAccountRegistryDeployment, cancellationTokenSource);
            return new IAppChainAccountRegistryService(web3, receipt.ContractAddress);
        }

        public IAppChainAccountRegistryService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class IAppChainAccountRegistryServiceBase: ContractWeb3ServiceBase
    {

        public IAppChainAccountRegistryServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public virtual Task<string> ActivateRequestAsync(ActivateFunction activateFunction)
        {
             return ContractHandler.SendRequestAsync(activateFunction);
        }

        public virtual Task<string> ActivateRequestAsync()
        {
             return ContractHandler.SendRequestAsync<ActivateFunction>();
        }

        public virtual Task<TransactionReceipt> ActivateRequestAndWaitForReceiptAsync(ActivateFunction activateFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(activateFunction, cancellationToken);
        }

        public virtual Task<TransactionReceipt> ActivateRequestAndWaitForReceiptAsync(CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync<ActivateFunction>(null, cancellationToken);
        }

        public virtual Task<string> ActivateAccountRequestAsync(ActivateAccountFunction activateAccountFunction)
        {
             return ContractHandler.SendRequestAsync(activateAccountFunction);
        }

        public virtual Task<TransactionReceipt> ActivateAccountRequestAndWaitForReceiptAsync(ActivateAccountFunction activateAccountFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(activateAccountFunction, cancellationToken);
        }

        public virtual Task<string> ActivateAccountRequestAsync(string account)
        {
            var activateAccountFunction = new ActivateAccountFunction();
                activateAccountFunction.Account = account;
            
             return ContractHandler.SendRequestAsync(activateAccountFunction);
        }

        public virtual Task<TransactionReceipt> ActivateAccountRequestAndWaitForReceiptAsync(string account, CancellationTokenSource cancellationToken = null)
        {
            var activateAccountFunction = new ActivateAccountFunction();
                activateAccountFunction.Account = account;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(activateAccountFunction, cancellationToken);
        }

        public virtual Task<string> BanRequestAsync(BanFunction banFunction)
        {
             return ContractHandler.SendRequestAsync(banFunction);
        }

        public virtual Task<TransactionReceipt> BanRequestAndWaitForReceiptAsync(BanFunction banFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(banFunction, cancellationToken);
        }

        public virtual Task<string> BanRequestAsync(string account, string reason)
        {
            var banFunction = new BanFunction();
                banFunction.Account = account;
                banFunction.Reason = reason;
            
             return ContractHandler.SendRequestAsync(banFunction);
        }

        public virtual Task<TransactionReceipt> BanRequestAndWaitForReceiptAsync(string account, string reason, CancellationTokenSource cancellationToken = null)
        {
            var banFunction = new BanFunction();
                banFunction.Account = account;
                banFunction.Reason = reason;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(banFunction, cancellationToken);
        }

        public virtual Task<CheckQuotaOutputDTO> CheckQuotaQueryAsync(CheckQuotaFunction checkQuotaFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<CheckQuotaFunction, CheckQuotaOutputDTO>(checkQuotaFunction, blockParameter);
        }

        public virtual Task<CheckQuotaOutputDTO> CheckQuotaQueryAsync(string account, BigInteger gasEstimate, BigInteger valueEstimate, BlockParameter blockParameter = null)
        {
            var checkQuotaFunction = new CheckQuotaFunction();
                checkQuotaFunction.Account = account;
                checkQuotaFunction.GasEstimate = gasEstimate;
                checkQuotaFunction.ValueEstimate = valueEstimate;
            
            return ContractHandler.QueryDeserializingToObjectAsync<CheckQuotaFunction, CheckQuotaOutputDTO>(checkQuotaFunction, blockParameter);
        }

        public Task<BigInteger> DefaultGasQuotaQueryAsync(DefaultGasQuotaFunction defaultGasQuotaFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<DefaultGasQuotaFunction, BigInteger>(defaultGasQuotaFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> DefaultGasQuotaQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<DefaultGasQuotaFunction, BigInteger>(null, blockParameter);
        }

        public Task<uint> DefaultOpQuotaQueryAsync(DefaultOpQuotaFunction defaultOpQuotaFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<DefaultOpQuotaFunction, uint>(defaultOpQuotaFunction, blockParameter);
        }

        
        public virtual Task<uint> DefaultOpQuotaQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<DefaultOpQuotaFunction, uint>(null, blockParameter);
        }

        public Task<BigInteger> DefaultValueQuotaQueryAsync(DefaultValueQuotaFunction defaultValueQuotaFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<DefaultValueQuotaFunction, BigInteger>(defaultValueQuotaFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> DefaultValueQuotaQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<DefaultValueQuotaFunction, BigInteger>(null, blockParameter);
        }

        public Task<BigInteger> GetAccountCountQueryAsync(GetAccountCountFunction getAccountCountFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetAccountCountFunction, BigInteger>(getAccountCountFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> GetAccountCountQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetAccountCountFunction, BigInteger>(null, blockParameter);
        }

        public virtual Task<GetAccountInfoOutputDTO> GetAccountInfoQueryAsync(GetAccountInfoFunction getAccountInfoFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<GetAccountInfoFunction, GetAccountInfoOutputDTO>(getAccountInfoFunction, blockParameter);
        }

        public virtual Task<GetAccountInfoOutputDTO> GetAccountInfoQueryAsync(string account, BlockParameter blockParameter = null)
        {
            var getAccountInfoFunction = new GetAccountInfoFunction();
                getAccountInfoFunction.Account = account;
            
            return ContractHandler.QueryDeserializingToObjectAsync<GetAccountInfoFunction, GetAccountInfoOutputDTO>(getAccountInfoFunction, blockParameter);
        }

        public Task<List<string>> GetAccountsQueryAsync(GetAccountsFunction getAccountsFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetAccountsFunction, List<string>>(getAccountsFunction, blockParameter);
        }

        
        public virtual Task<List<string>> GetAccountsQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetAccountsFunction, List<string>>(null, blockParameter);
        }

        public virtual Task<GetRemainingQuotaOutputDTO> GetRemainingQuotaQueryAsync(GetRemainingQuotaFunction getRemainingQuotaFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<GetRemainingQuotaFunction, GetRemainingQuotaOutputDTO>(getRemainingQuotaFunction, blockParameter);
        }

        public virtual Task<GetRemainingQuotaOutputDTO> GetRemainingQuotaQueryAsync(string account, BlockParameter blockParameter = null)
        {
            var getRemainingQuotaFunction = new GetRemainingQuotaFunction();
                getRemainingQuotaFunction.Account = account;
            
            return ContractHandler.QueryDeserializingToObjectAsync<GetRemainingQuotaFunction, GetRemainingQuotaOutputDTO>(getRemainingQuotaFunction, blockParameter);
        }

        public Task<byte> GetStatusQueryAsync(GetStatusFunction getStatusFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetStatusFunction, byte>(getStatusFunction, blockParameter);
        }

        
        public virtual Task<byte> GetStatusQueryAsync(string account, BlockParameter blockParameter = null)
        {
            var getStatusFunction = new GetStatusFunction();
                getStatusFunction.Account = account;
            
            return ContractHandler.QueryAsync<GetStatusFunction, byte>(getStatusFunction, blockParameter);
        }

        public virtual Task<string> InviteRequestAsync(InviteFunction inviteFunction)
        {
             return ContractHandler.SendRequestAsync(inviteFunction);
        }

        public virtual Task<TransactionReceipt> InviteRequestAndWaitForReceiptAsync(InviteFunction inviteFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(inviteFunction, cancellationToken);
        }

        public virtual Task<string> InviteRequestAsync(string account)
        {
            var inviteFunction = new InviteFunction();
                inviteFunction.Account = account;
            
             return ContractHandler.SendRequestAsync(inviteFunction);
        }

        public virtual Task<TransactionReceipt> InviteRequestAndWaitForReceiptAsync(string account, CancellationTokenSource cancellationToken = null)
        {
            var inviteFunction = new InviteFunction();
                inviteFunction.Account = account;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(inviteFunction, cancellationToken);
        }

        public virtual Task<string> InviteBatchRequestAsync(InviteBatchFunction inviteBatchFunction)
        {
             return ContractHandler.SendRequestAsync(inviteBatchFunction);
        }

        public virtual Task<TransactionReceipt> InviteBatchRequestAndWaitForReceiptAsync(InviteBatchFunction inviteBatchFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(inviteBatchFunction, cancellationToken);
        }

        public virtual Task<string> InviteBatchRequestAsync(List<string> accounts)
        {
            var inviteBatchFunction = new InviteBatchFunction();
                inviteBatchFunction.Accounts = accounts;
            
             return ContractHandler.SendRequestAsync(inviteBatchFunction);
        }

        public virtual Task<TransactionReceipt> InviteBatchRequestAndWaitForReceiptAsync(List<string> accounts, CancellationTokenSource cancellationToken = null)
        {
            var inviteBatchFunction = new InviteBatchFunction();
                inviteBatchFunction.Accounts = accounts;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(inviteBatchFunction, cancellationToken);
        }

        public Task<bool> InviteRequiredQueryAsync(InviteRequiredFunction inviteRequiredFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<InviteRequiredFunction, bool>(inviteRequiredFunction, blockParameter);
        }

        
        public virtual Task<bool> InviteRequiredQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<InviteRequiredFunction, bool>(null, blockParameter);
        }

        public Task<bool> IsActiveQueryAsync(IsActiveFunction isActiveFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<IsActiveFunction, bool>(isActiveFunction, blockParameter);
        }

        
        public virtual Task<bool> IsActiveQueryAsync(string account, BlockParameter blockParameter = null)
        {
            var isActiveFunction = new IsActiveFunction();
                isActiveFunction.Account = account;
            
            return ContractHandler.QueryAsync<IsActiveFunction, bool>(isActiveFunction, blockParameter);
        }

        public virtual Task<string> ResetQuotaRequestAsync(ResetQuotaFunction resetQuotaFunction)
        {
             return ContractHandler.SendRequestAsync(resetQuotaFunction);
        }

        public virtual Task<TransactionReceipt> ResetQuotaRequestAndWaitForReceiptAsync(ResetQuotaFunction resetQuotaFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(resetQuotaFunction, cancellationToken);
        }

        public virtual Task<string> ResetQuotaRequestAsync(string account)
        {
            var resetQuotaFunction = new ResetQuotaFunction();
                resetQuotaFunction.Account = account;
            
             return ContractHandler.SendRequestAsync(resetQuotaFunction);
        }

        public virtual Task<TransactionReceipt> ResetQuotaRequestAndWaitForReceiptAsync(string account, CancellationTokenSource cancellationToken = null)
        {
            var resetQuotaFunction = new ResetQuotaFunction();
                resetQuotaFunction.Account = account;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(resetQuotaFunction, cancellationToken);
        }

        public Task<bool> SelfActivationEnabledQueryAsync(SelfActivationEnabledFunction selfActivationEnabledFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<SelfActivationEnabledFunction, bool>(selfActivationEnabledFunction, blockParameter);
        }

        
        public virtual Task<bool> SelfActivationEnabledQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<SelfActivationEnabledFunction, bool>(null, blockParameter);
        }

        public virtual Task<string> SetDefaultQuotasRequestAsync(SetDefaultQuotasFunction setDefaultQuotasFunction)
        {
             return ContractHandler.SendRequestAsync(setDefaultQuotasFunction);
        }

        public virtual Task<TransactionReceipt> SetDefaultQuotasRequestAndWaitForReceiptAsync(SetDefaultQuotasFunction setDefaultQuotasFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setDefaultQuotasFunction, cancellationToken);
        }

        public virtual Task<string> SetDefaultQuotasRequestAsync(BigInteger gasQuota, uint opQuota, BigInteger valueQuota)
        {
            var setDefaultQuotasFunction = new SetDefaultQuotasFunction();
                setDefaultQuotasFunction.GasQuota = gasQuota;
                setDefaultQuotasFunction.OpQuota = opQuota;
                setDefaultQuotasFunction.ValueQuota = valueQuota;
            
             return ContractHandler.SendRequestAsync(setDefaultQuotasFunction);
        }

        public virtual Task<TransactionReceipt> SetDefaultQuotasRequestAndWaitForReceiptAsync(BigInteger gasQuota, uint opQuota, BigInteger valueQuota, CancellationTokenSource cancellationToken = null)
        {
            var setDefaultQuotasFunction = new SetDefaultQuotasFunction();
                setDefaultQuotasFunction.GasQuota = gasQuota;
                setDefaultQuotasFunction.OpQuota = opQuota;
                setDefaultQuotasFunction.ValueQuota = valueQuota;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setDefaultQuotasFunction, cancellationToken);
        }

        public virtual Task<string> SetInviteRequiredRequestAsync(SetInviteRequiredFunction setInviteRequiredFunction)
        {
             return ContractHandler.SendRequestAsync(setInviteRequiredFunction);
        }

        public virtual Task<TransactionReceipt> SetInviteRequiredRequestAndWaitForReceiptAsync(SetInviteRequiredFunction setInviteRequiredFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setInviteRequiredFunction, cancellationToken);
        }

        public virtual Task<string> SetInviteRequiredRequestAsync(bool required)
        {
            var setInviteRequiredFunction = new SetInviteRequiredFunction();
                setInviteRequiredFunction.Required = required;
            
             return ContractHandler.SendRequestAsync(setInviteRequiredFunction);
        }

        public virtual Task<TransactionReceipt> SetInviteRequiredRequestAndWaitForReceiptAsync(bool required, CancellationTokenSource cancellationToken = null)
        {
            var setInviteRequiredFunction = new SetInviteRequiredFunction();
                setInviteRequiredFunction.Required = required;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setInviteRequiredFunction, cancellationToken);
        }

        public virtual Task<string> SetQuotaRequestAsync(SetQuotaFunction setQuotaFunction)
        {
             return ContractHandler.SendRequestAsync(setQuotaFunction);
        }

        public virtual Task<TransactionReceipt> SetQuotaRequestAndWaitForReceiptAsync(SetQuotaFunction setQuotaFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setQuotaFunction, cancellationToken);
        }

        public virtual Task<string> SetQuotaRequestAsync(string account, BigInteger gasQuota, uint opQuota, BigInteger valueQuota)
        {
            var setQuotaFunction = new SetQuotaFunction();
                setQuotaFunction.Account = account;
                setQuotaFunction.GasQuota = gasQuota;
                setQuotaFunction.OpQuota = opQuota;
                setQuotaFunction.ValueQuota = valueQuota;
            
             return ContractHandler.SendRequestAsync(setQuotaFunction);
        }

        public virtual Task<TransactionReceipt> SetQuotaRequestAndWaitForReceiptAsync(string account, BigInteger gasQuota, uint opQuota, BigInteger valueQuota, CancellationTokenSource cancellationToken = null)
        {
            var setQuotaFunction = new SetQuotaFunction();
                setQuotaFunction.Account = account;
                setQuotaFunction.GasQuota = gasQuota;
                setQuotaFunction.OpQuota = opQuota;
                setQuotaFunction.ValueQuota = valueQuota;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setQuotaFunction, cancellationToken);
        }

        public virtual Task<string> SetSelfActivationEnabledRequestAsync(SetSelfActivationEnabledFunction setSelfActivationEnabledFunction)
        {
             return ContractHandler.SendRequestAsync(setSelfActivationEnabledFunction);
        }

        public virtual Task<TransactionReceipt> SetSelfActivationEnabledRequestAndWaitForReceiptAsync(SetSelfActivationEnabledFunction setSelfActivationEnabledFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setSelfActivationEnabledFunction, cancellationToken);
        }

        public virtual Task<string> SetSelfActivationEnabledRequestAsync(bool enabled)
        {
            var setSelfActivationEnabledFunction = new SetSelfActivationEnabledFunction();
                setSelfActivationEnabledFunction.Enabled = enabled;
            
             return ContractHandler.SendRequestAsync(setSelfActivationEnabledFunction);
        }

        public virtual Task<TransactionReceipt> SetSelfActivationEnabledRequestAndWaitForReceiptAsync(bool enabled, CancellationTokenSource cancellationToken = null)
        {
            var setSelfActivationEnabledFunction = new SetSelfActivationEnabledFunction();
                setSelfActivationEnabledFunction.Enabled = enabled;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setSelfActivationEnabledFunction, cancellationToken);
        }

        public virtual Task<string> SuspendRequestAsync(SuspendFunction suspendFunction)
        {
             return ContractHandler.SendRequestAsync(suspendFunction);
        }

        public virtual Task<TransactionReceipt> SuspendRequestAndWaitForReceiptAsync(SuspendFunction suspendFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(suspendFunction, cancellationToken);
        }

        public virtual Task<string> SuspendRequestAsync(string account, ulong until)
        {
            var suspendFunction = new SuspendFunction();
                suspendFunction.Account = account;
                suspendFunction.Until = until;
            
             return ContractHandler.SendRequestAsync(suspendFunction);
        }

        public virtual Task<TransactionReceipt> SuspendRequestAndWaitForReceiptAsync(string account, ulong until, CancellationTokenSource cancellationToken = null)
        {
            var suspendFunction = new SuspendFunction();
                suspendFunction.Account = account;
                suspendFunction.Until = until;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(suspendFunction, cancellationToken);
        }

        public virtual Task<string> UnbanRequestAsync(UnbanFunction unbanFunction)
        {
             return ContractHandler.SendRequestAsync(unbanFunction);
        }

        public virtual Task<TransactionReceipt> UnbanRequestAndWaitForReceiptAsync(UnbanFunction unbanFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(unbanFunction, cancellationToken);
        }

        public virtual Task<string> UnbanRequestAsync(string account)
        {
            var unbanFunction = new UnbanFunction();
                unbanFunction.Account = account;
            
             return ContractHandler.SendRequestAsync(unbanFunction);
        }

        public virtual Task<TransactionReceipt> UnbanRequestAndWaitForReceiptAsync(string account, CancellationTokenSource cancellationToken = null)
        {
            var unbanFunction = new UnbanFunction();
                unbanFunction.Account = account;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(unbanFunction, cancellationToken);
        }

        public virtual Task<string> UnsuspendRequestAsync(UnsuspendFunction unsuspendFunction)
        {
             return ContractHandler.SendRequestAsync(unsuspendFunction);
        }

        public virtual Task<TransactionReceipt> UnsuspendRequestAndWaitForReceiptAsync(UnsuspendFunction unsuspendFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(unsuspendFunction, cancellationToken);
        }

        public virtual Task<string> UnsuspendRequestAsync(string account)
        {
            var unsuspendFunction = new UnsuspendFunction();
                unsuspendFunction.Account = account;
            
             return ContractHandler.SendRequestAsync(unsuspendFunction);
        }

        public virtual Task<TransactionReceipt> UnsuspendRequestAndWaitForReceiptAsync(string account, CancellationTokenSource cancellationToken = null)
        {
            var unsuspendFunction = new UnsuspendFunction();
                unsuspendFunction.Account = account;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(unsuspendFunction, cancellationToken);
        }

        public virtual Task<string> UseQuotaRequestAsync(UseQuotaFunction useQuotaFunction)
        {
             return ContractHandler.SendRequestAsync(useQuotaFunction);
        }

        public virtual Task<TransactionReceipt> UseQuotaRequestAndWaitForReceiptAsync(UseQuotaFunction useQuotaFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(useQuotaFunction, cancellationToken);
        }

        public virtual Task<string> UseQuotaRequestAsync(string account, BigInteger gasUsed, BigInteger valueUsed)
        {
            var useQuotaFunction = new UseQuotaFunction();
                useQuotaFunction.Account = account;
                useQuotaFunction.GasUsed = gasUsed;
                useQuotaFunction.ValueUsed = valueUsed;
            
             return ContractHandler.SendRequestAsync(useQuotaFunction);
        }

        public virtual Task<TransactionReceipt> UseQuotaRequestAndWaitForReceiptAsync(string account, BigInteger gasUsed, BigInteger valueUsed, CancellationTokenSource cancellationToken = null)
        {
            var useQuotaFunction = new UseQuotaFunction();
                useQuotaFunction.Account = account;
                useQuotaFunction.GasUsed = gasUsed;
                useQuotaFunction.ValueUsed = valueUsed;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(useQuotaFunction, cancellationToken);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(ActivateFunction),
                typeof(ActivateAccountFunction),
                typeof(BanFunction),
                typeof(CheckQuotaFunction),
                typeof(DefaultGasQuotaFunction),
                typeof(DefaultOpQuotaFunction),
                typeof(DefaultValueQuotaFunction),
                typeof(GetAccountCountFunction),
                typeof(GetAccountInfoFunction),
                typeof(GetAccountsFunction),
                typeof(GetRemainingQuotaFunction),
                typeof(GetStatusFunction),
                typeof(InviteFunction),
                typeof(InviteBatchFunction),
                typeof(InviteRequiredFunction),
                typeof(IsActiveFunction),
                typeof(ResetQuotaFunction),
                typeof(SelfActivationEnabledFunction),
                typeof(SetDefaultQuotasFunction),
                typeof(SetInviteRequiredFunction),
                typeof(SetQuotaFunction),
                typeof(SetSelfActivationEnabledFunction),
                typeof(SuspendFunction),
                typeof(UnbanFunction),
                typeof(UnsuspendFunction),
                typeof(UseQuotaFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(AccountActivatedEventDTO),
                typeof(AccountBannedEventDTO),
                typeof(AccountInvitedEventDTO),
                typeof(AccountSuspendedEventDTO),
                typeof(AccountUnbannedEventDTO),
                typeof(AccountUnsuspendedEventDTO),
                typeof(QuotaResetEventDTO),
                typeof(QuotaUpdatedEventDTO),
                typeof(QuotaUsedEventDTO)
            };
        }

        public override List<Type> GetAllErrorTypes()
        {
            return new List<Type>
            {

            };
        }
    }
}
