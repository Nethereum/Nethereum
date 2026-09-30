using System;
using System.Numerics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nethereum.AccountAbstraction;
using Nethereum.AccountAbstraction.Client;

namespace Nethereum.AccountAbstraction.Example.Core
{
    public partial class InteractionViewModel : TabViewModel
    {
        private readonly SessionState _session;

        [ObservableProperty]
        private AATransactionReceipt? _lastReceipt;

        [ObservableProperty]
        private BigInteger _count;

        public InteractionViewModel(SessionState session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        [RelayCommand]
        private Task SendCountAsync() => RunAsync(async () =>
        {
            _session.RequireReady();
            var account = RequireAccount();
            var counter = _session.Counter!;
            var handler = _session.Client!.Configure(counter, account);
            _session.ApplyPaymentMode(handler);

            var receipt = (AATransactionReceipt)await counter.CountRequestAndWaitForReceiptAsync().ConfigureAwait(false);
            LastReceipt = receipt;
            Count = await counter.CountersQueryAsync(account.Address).ConfigureAwait(false);

            StatusMessage = receipt.UserOpSuccess
                ? $"Counted to {Count} (userOpHash {receipt.UserOpHash})"
                : receipt.FailureDiagnostic;
        });

        [RelayCommand]
        private Task SendFailingCountAsync() => RunAsync(async () =>
        {
            _session.RequireReady();
            var account = RequireAccount();
            var counter = _session.Counter!;
            var handler = _session.Client!.Configure(counter, account);
            _session.ApplyPaymentMode(handler);

            var receipt = (AATransactionReceipt)await counter.CountFailRequestAndWaitForReceiptAsync().ConfigureAwait(false);
            LastReceipt = receipt;
            StatusMessage = receipt.FailureDiagnostic;
        });

        private NethereumSmartAccount RequireAccount() =>
            _session.Account ?? throw new InvalidOperationException(
                "No active account - create or attach one first via SetupViewModel.CreateAccountAsync.");
    }
}
