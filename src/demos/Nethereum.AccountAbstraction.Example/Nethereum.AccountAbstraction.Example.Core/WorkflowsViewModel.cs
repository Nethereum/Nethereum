using System;
using System.Numerics;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nethereum.AccountAbstraction.Client;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccount;
using Nethereum.AccountAbstraction.Contracts.Modules.Rhinestone.OwnableExecutor;
using Nethereum.AccountAbstraction.Contracts.Modules.Rhinestone.OwnableExecutor.ContractDefinition;
using Nethereum.AccountAbstraction.ERC7579;
using Nethereum.AccountAbstraction.ERC7579.Modules;
using Nethereum.AccountAbstraction.Example.Contracts.TestCounter;
using Nethereum.AccountAbstraction.Example.Contracts.TestCounter.ContractDefinition;
using Nethereum.Contracts;
using Nethereum.Signer;
using Nethereum.Web3;
using Web3Account = Nethereum.Web3.Accounts.Account;

namespace Nethereum.AccountAbstraction.Example.Core
{
    public partial class WorkflowsViewModel : TabViewModel
    {
        private readonly SessionState _session;

        private EthECKey? _agentKey;
        private IWeb3? _agentWeb3;

        [ObservableProperty]
        private string? _agentAddress;

        [ObservableProperty]
        private bool _isExecutorInstalled;

        [ObservableProperty]
        private AATransactionReceipt? _lastReceipt;

        [ObservableProperty]
        private string? _lastDelegatedTxHash;

        [ObservableProperty]
        private BigInteger _count;

        [ObservableProperty]
        private bool? _unauthorizedRejected;

        public string? ExecutorAddress => _session.OwnableExecutor?.ContractAddress;

        public WorkflowsViewModel(SessionState session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        [RelayCommand]
        private Task CreateAgentAsync() => RunAsync(async () =>
        {
            _session.RequireReady();
            _agentKey = EthECKey.GenerateKey();
            var agentAddress = _agentKey.GetPublicAddress();
            _agentWeb3 = new Nethereum.Web3.Web3(new Web3Account(_agentKey), _session.Web3!.Client);

            await _session.Faucet!.FundAsync(agentAddress).ConfigureAwait(false);

            AgentAddress = agentAddress;
            IsExecutorInstalled = false;
            UnauthorizedRejected = null;
            StatusMessage = $"Automation agent {agentAddress} generated and funded for gas - your account's own key will never sign for it.";
        });

        [RelayCommand]
        private Task InstallExecutorAsync() => RunAsync(async () =>
        {
            _session.RequireReady();
            var account = RequireAccount();
            var agentAddress = RequireAgentAddress();
            var executorAddress = ExecutorAddress!;

            var accountService = new NethereumAccountService(_session.Web3!, account.Address);
            accountService.UseAccountAbstraction(account, _session.Client!);

            var receipt = (AATransactionReceipt)await accountService.InstallOwnableExecutorAndWaitForReceiptAsync(
                executorAddress, agentAddress).ConfigureAwait(false);
            LastReceipt = receipt;

            var moduleConfig = OwnableExecutorConfig.Create(executorAddress, agentAddress);
            IsExecutorInstalled = await accountService.IsModuleInstalledAsync(moduleConfig).ConfigureAwait(false);
            StatusMessage = receipt.UserOpSuccess
                ? $"OwnableExecutor installed on {account.Address} with {agentAddress} as its owner (userOpHash {receipt.UserOpHash}) - isModuleInstalled: {IsExecutorInstalled}"
                : receipt.FailureDiagnostic;
        });

        [RelayCommand]
        private Task RunDelegatedActionAsync() => RunAsync(async () =>
        {
            _session.RequireReady();
            var account = RequireAccount();
            var agentWeb3 = RequireAgentWeb3();
            RequireExecutorInstalled();

            var executorAsAgent = new OwnableExecutorService(agentWeb3, ExecutorAddress!);
            var receipt = await executorAsAgent.ExecuteOnOwnedAccountRequestAndWaitForReceiptAsync(
                account.Address, BuildCountExecutionCallData()).ConfigureAwait(false);
            LastDelegatedTxHash = receipt.TransactionHash;

            Count = await _session.Counter!.CountersQueryAsync(account.Address).ConfigureAwait(false);
            StatusMessage = $"Agent {AgentAddress} executed count() on {account.Address} directly (tx {LastDelegatedTxHash}) - " +
                             $"the account's own key never signed this. Counter is now {Count}.";
        });

        [RelayCommand]
        private Task TryUnauthorizedActionAsync() => RunAsync(async () =>
        {
            _session.RequireReady();
            var account = RequireAccount();
            RequireExecutorInstalled();

            var strangerKey = EthECKey.GenerateKey();
            var strangerAddress = strangerKey.GetPublicAddress();
            var strangerWeb3 = new Nethereum.Web3.Web3(new Web3Account(strangerKey), _session.Web3!.Client);
            await _session.Faucet!.FundAsync(strangerAddress).ConfigureAwait(false);

            var executorAsStranger = new OwnableExecutorService(strangerWeb3, ExecutorAddress!);

            try
            {
                await executorAsStranger.ExecuteOnOwnedAccountRequestAndWaitForReceiptAsync(
                    account.Address, BuildCountExecutionCallData()).ConfigureAwait(false);
                UnauthorizedRejected = false;
                StatusMessage = $"UNEXPECTED: unauthorized caller {strangerAddress} was able to execute on {account.Address} - the executor's owner check did not hold.";
            }
            catch (SmartContractCustomErrorRevertException ex)
                when (ex.ExceptionEncodedData.IsExceptionEncodedDataForError<UnauthorizedAccessError>())
            {
                UnauthorizedRejected = true;
                StatusMessage = $"Unauthorized caller {strangerAddress} was REJECTED as expected (not a registered owner): UnauthorizedAccess()";
            }
        });

        [RelayCommand]
        private Task UninstallExecutorAsync() => RunAsync(async () =>
        {
            _session.RequireReady();
            var account = RequireAccount();
            var agentAddress = RequireAgentAddress();

            var moduleConfig = OwnableExecutorConfig.Create(ExecutorAddress!, agentAddress);

            var accountService = new NethereumAccountService(_session.Web3!, account.Address);
            accountService.UseAccountAbstraction(account, _session.Client!);

            var receipt = (AATransactionReceipt)await accountService.UninstallModuleAndWaitForReceiptAsync(moduleConfig).ConfigureAwait(false);
            LastReceipt = receipt;

            IsExecutorInstalled = await accountService.IsModuleInstalledAsync(moduleConfig).ConfigureAwait(false);
            StatusMessage = receipt.UserOpSuccess
                ? $"OwnableExecutor removed from {account.Address} - isModuleInstalled: {IsExecutorInstalled}"
                : receipt.FailureDiagnostic;

            if (receipt.UserOpSuccess)
            {
                Count = default;
                LastDelegatedTxHash = null;
                UnauthorizedRejected = null;
            }
        });

        private byte[] BuildCountExecutionCallData()
        {
            var countCallData = new CountFunction().GetCallData();
            return ERC7579ExecutionLib.EncodeSingle(_session.Counter!.ContractHandler.ContractAddress, BigInteger.Zero, countCallData);
        }

        private NethereumSmartAccount RequireAccount() =>
            _session.Account ?? throw new InvalidOperationException(
                "No active account - create and fund a modular account in Setup first.");

        private string RequireAgentAddress() =>
            AgentAddress ?? throw new InvalidOperationException(
                "No automation agent yet - create one first via CreateAgentAsync.");

        private IWeb3 RequireAgentWeb3() =>
            _agentWeb3 ?? throw new InvalidOperationException(
                "No automation agent yet - create one first via CreateAgentAsync.");

        private void RequireExecutorInstalled()
        {
            if (!IsExecutorInstalled)
                throw new InvalidOperationException("OwnableExecutor is not installed on this account yet - install it first via InstallExecutorAsync.");
        }
    }
}
