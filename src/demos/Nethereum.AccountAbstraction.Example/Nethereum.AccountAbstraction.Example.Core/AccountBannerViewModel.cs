using System;
using System.ComponentModel;
using System.Numerics;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Nethereum.AccountAbstraction.Example.Core
{
    public partial class AccountBannerViewModel : ObservableObject, IDisposable
    {
        private readonly SessionState _session;

        [ObservableProperty]
        private string? _address;

        [ObservableProperty]
        private string? _description;

        [ObservableProperty]
        private bool _isDeployed;

        [ObservableProperty]
        private BigInteger _balance;

        [ObservableProperty]
        private AccountPaymentMode _paymentMode;

        public bool HasAccount => Address is not null;

        public string DisplayText => HasAccount
            ? $"Account {Address} · {Description} · {(IsDeployed ? "Deployed" : "Counterfactual")} · {Nethereum.Web3.Web3.Convert.FromWei(Balance)} ETH · {(PaymentMode == AccountPaymentMode.PaymasterSponsored ? "Paymaster-sponsored" : "Self-funded")}"
            : "No account yet - create one in Setup or Passkey";

        public AccountBannerViewModel(SessionState session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));

            _session.PropertyChanged += OnSessionPropertyChanged;
            _session.BalanceMayHaveChanged += OnBalanceMayHaveChanged;
            SyncFromSession();
        }

        private void OnSessionPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(SessionState.Account) or nameof(SessionState.AccountDescription)
                or nameof(SessionState.PaymentMode))
            {
                SyncFromSession();
                _ = RefreshBalanceAsync();
            }
        }

        private void OnBalanceMayHaveChanged() => _ = RefreshBalanceAsync();

        private void SyncFromSession()
        {
            Address = _session.Account?.Address;
            Description = _session.AccountDescription;
            IsDeployed = _session.Account?.IsDeployed ?? false;
            PaymentMode = _session.PaymentMode;
        }

        public async Task RefreshBalanceAsync()
        {
            var account = _session.Account;
            var faucet = _session.Faucet;
            if (account is null || faucet is null)
            {
                Balance = BigInteger.Zero;
                return;
            }

            try
            {
                Balance = await faucet.GetBalanceAsync(account.Address).ConfigureAwait(false);
            }
            catch
            {
            }
        }

        partial void OnAddressChanged(string? value)
        {
            OnPropertyChanged(nameof(HasAccount));
            OnPropertyChanged(nameof(DisplayText));
        }

        partial void OnDescriptionChanged(string? value) => OnPropertyChanged(nameof(DisplayText));
        partial void OnIsDeployedChanged(bool value) => OnPropertyChanged(nameof(DisplayText));
        partial void OnBalanceChanged(BigInteger value) => OnPropertyChanged(nameof(DisplayText));
        partial void OnPaymentModeChanged(AccountPaymentMode value) => OnPropertyChanged(nameof(DisplayText));

        public void Dispose()
        {
            _session.PropertyChanged -= OnSessionPropertyChanged;
            _session.BalanceMayHaveChanged -= OnBalanceMayHaveChanged;
        }
    }
}
