using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nethereum.Signer;

namespace Nethereum.AccountAbstraction.Example.Core
{
    public partial class SetupViewModel : TabViewModel
    {
        private readonly SessionState _session;

        [ObservableProperty]
        private string? _smartAccountAddress;

        [ObservableProperty]
        private bool _isDeployed;

        [ObservableProperty]
        private string? _ownerAddress;

        [ObservableProperty]
        private AccountPaymentMode _selectedPaymentMode = AccountPaymentMode.SelfFunded;

        public SetupViewModel(SessionState session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _session.PropertyChanged += OnSessionPropertyChanged;
            _session.InfraChanged += OnInfraChanged;
        }

        public bool HasActiveAccount => _session.Account is not null;

        public string? ActiveAccountAddress => _session.Account?.Address;

        public bool ActiveAccountIsPaymasterSponsored => _session.PaymentMode == AccountPaymentMode.PaymasterSponsored;

        public bool HasPaymaster => _session.HasPaymaster;

        public IReadOnlyList<AccountPaymentMode> AvailablePaymentModes => _session.AvailablePaymentModes;

        private void OnSessionPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SessionState.Account))
            {
                OnPropertyChanged(nameof(HasActiveAccount));
                OnPropertyChanged(nameof(ActiveAccountAddress));
            }
            if (e.PropertyName == nameof(SessionState.PaymentMode))
                OnPropertyChanged(nameof(ActiveAccountIsPaymasterSponsored));
        }

        private void OnInfraChanged()
        {
            OnPropertyChanged(nameof(HasPaymaster));
            OnPropertyChanged(nameof(AvailablePaymentModes));
            if (!HasPaymaster) SelectedPaymentMode = AccountPaymentMode.SelfFunded;
        }

        [RelayCommand]
        private Task CreateAccountAsync() => RunAsync(async () =>
        {
            _session.RequireReady();
            var owner = EthECKey.GenerateKey();
            var account = await _session.Client!.CreateAccountAsync(owner).ConfigureAwait(false);

            OwnerAddress = owner.GetPublicAddress();
            SmartAccountAddress = account.Address;
            IsDeployed = account.IsDeployed;

            _session.RaiseAccountChanged(account, "ECDSA owner (secp256k1)");
            var sponsored = SelectedPaymentMode == AccountPaymentMode.PaymasterSponsored && HasPaymaster;
            _session.PaymentMode = sponsored ? AccountPaymentMode.PaymasterSponsored : AccountPaymentMode.SelfFunded;

            var deployedNote = account.IsDeployed
                ? $"Attached to already-deployed account {account.Address}"
                : $"Counterfactual account ready at {account.Address} - it deploys on the first operation";
            StatusMessage = sponsored
                ? $"{deployedNote} - paymaster-sponsored: gas is covered by the paymaster, no funding needed"
                : $"{deployedNote} - self-funded: fund it below before sending";
        });

        [RelayCommand]
        private Task FundAccountAsync() => RunAsync(async () =>
        {
            _session.RequireReady();
            var account = RequireAccount();
            await _session.Faucet!.FundAsync(account.Address).ConfigureAwait(false);
            var balance = await _session.Faucet!.GetBalanceAsync(account.Address).ConfigureAwait(false);
            _session.NotifyBalanceChanged();

            StatusMessage = $"Funded {account.Address} with 10 ETH - balance now {Nethereum.Web3.Web3.Convert.FromWei(balance)} ETH";
        });

        private NethereumSmartAccount RequireAccount() =>
            _session.Account ?? throw new InvalidOperationException(
                "No active account - create one first via CreateAccountAsync.");
    }
}
