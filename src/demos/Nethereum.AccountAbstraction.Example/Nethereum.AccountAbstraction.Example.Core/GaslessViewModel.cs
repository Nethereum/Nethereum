using System;
using System.Numerics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nethereum.AccountAbstraction;
using Nethereum.AccountAbstraction.Client;

namespace Nethereum.AccountAbstraction.Example.Core
{
    public partial class GaslessViewModel : TabViewModel
    {
        private readonly SessionState _session;

        [ObservableProperty]
        private AATransactionReceipt? _lastReceipt;

        [ObservableProperty]
        private BigInteger _senderBalanceBefore;

        [ObservableProperty]
        private BigInteger _senderBalanceAfter;

        [ObservableProperty]
        private bool _senderBalanceUnchanged;

        public GaslessViewModel(SessionState session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _session.InfraChanged += () =>
            {
                OnPropertyChanged(nameof(HasPaymaster));
                OnPropertyChanged(nameof(CanSendGasless));
            };
            PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(IsBusy)) OnPropertyChanged(nameof(CanSendGasless));
            };
        }

        public bool HasPaymaster => _session.HasPaymaster;

        public bool CanSendGasless => HasPaymaster && !IsBusy;

        [RelayCommand]
        private Task SendGaslessAsync() => RunAsync(async () =>
        {
            _session.RequireReady();
            if (!HasPaymaster)
                throw new InvalidOperationException("No paymaster is configured for this infrastructure (use-existing mode); gasless is unavailable here.");
            var account = RequireAccount();
            var counter = _session.Counter!;
            var handler = _session.Client!.Configure(counter, account);
            handler.WithPaymaster(_session.Paymaster!);

            SenderBalanceBefore = (await counter.Web3.Eth.GetBalance.SendRequestAsync(account.Address).ConfigureAwait(false)).Value;

            var receipt = (AATransactionReceipt)await counter.CountRequestAndWaitForReceiptAsync().ConfigureAwait(false);
            LastReceipt = receipt;

            SenderBalanceAfter = (await counter.Web3.Eth.GetBalance.SendRequestAsync(account.Address).ConfigureAwait(false)).Value;
            SenderBalanceUnchanged = SenderBalanceAfter == SenderBalanceBefore;

            StatusMessage = receipt.UserOpSuccess
                ? $"Paymaster sponsored the operation - sender balance stayed at {SenderBalanceAfter} wei (userOpHash {receipt.UserOpHash})"
                : receipt.FailureDiagnostic;
        });

        private NethereumSmartAccount RequireAccount() =>
            _session.Account ?? throw new InvalidOperationException(
                "No active account - create or attach one first via SetupViewModel.CreateAccountAsync.");
    }
}
