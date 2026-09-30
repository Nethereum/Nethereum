using System;
using System.Numerics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nethereum.AccountAbstraction;
using Nethereum.AccountAbstraction.Client;
using Nethereum.AccountAbstraction.Example.Contracts.TestCounter.ContractDefinition;

namespace Nethereum.AccountAbstraction.Example.Core
{
    public partial class BatchViewModel : TabViewModel
    {
        private readonly SessionState _session;

        [ObservableProperty]
        private AATransactionReceipt? _lastReceipt;

        [ObservableProperty]
        private BigInteger _count;

        public BatchViewModel(SessionState session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        [RelayCommand]
        private Task SendBatchAsync() => RunAsync(async () =>
        {
            _session.RequireReady();
            var account = RequireAccount();
            var counter = _session.Counter!;
            var handler = _session.Client!.Configure(counter, account);
            _session.ApplyPaymentMode(handler);

            var receipt = await handler.BatchExecuteAsync(
                new CountFunction().ToBatchCall(),
                new CountFunction().ToBatchCall());

            LastReceipt = receipt;
            Count = await counter.CountersQueryAsync(account.Address).ConfigureAwait(false);

            StatusMessage = receipt.UserOpSuccess
                ? $"Batch of 2 increments landed in one UserOperation - counter is now {Count} (userOpHash {receipt.UserOpHash})"
                : receipt.FailureDiagnostic;
        });

        [RelayCommand]
        private Task SendBatchWithAFailingCallAsync() => RunAsync(async () =>
        {
            _session.RequireReady();
            var account = RequireAccount();
            var counter = _session.Counter!;
            var handler = _session.Client!.Configure(counter, account);
            _session.ApplyPaymentMode(handler);

            var receipt = await handler.BatchExecuteAsync(
                new CountFunction().ToBatchCall(),
                new CountFailFunction().ToBatchCall());

            LastReceipt = receipt;
            Count = await counter.CountersQueryAsync(account.Address).ConfigureAwait(false);
            StatusMessage = receipt.FailureDiagnostic;
        });

        private NethereumSmartAccount RequireAccount() =>
            _session.Account ?? throw new InvalidOperationException(
                "No active account - create or attach one first via SetupViewModel.CreateAccountAsync.");
    }
}
