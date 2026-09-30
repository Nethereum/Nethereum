using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Numerics;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nethereum.AccountAbstraction.Client;
using Nethereum.AccountAbstraction.WebAuthn.Client;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.WebAuthn;

namespace Nethereum.AccountAbstraction.Example.Core
{
    public partial class PasskeyAccountViewModel : TabViewModel
    {
        private readonly SessionState _session;
        private readonly IWebAuthnCredentialFactory _credentialFactory;
        private readonly IWebAuthnAuthenticator _authenticator;

        [ObservableProperty]
        private string? _smartAccountAddress;

        [ObservableProperty]
        private bool _isDeployed;

        [ObservableProperty]
        private string? _credentialId;

        [ObservableProperty]
        private AATransactionReceipt? _lastReceipt;

        [ObservableProperty]
        private BigInteger _count;

        [ObservableProperty]
        private AccountPaymentMode _selectedPaymentMode = AccountPaymentMode.SelfFunded;

        public PasskeyAccountViewModel(
            SessionState session,
            IWebAuthnCredentialFactory credentialFactory,
            IWebAuthnAuthenticator authenticator)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _credentialFactory = credentialFactory ?? throw new ArgumentNullException(nameof(credentialFactory));
            _authenticator = authenticator ?? throw new ArgumentNullException(nameof(authenticator));
            _session.InfraChanged += OnInfraChanged;
            _session.PropertyChanged += OnSessionPropertyChanged;
        }

        public bool HasPaymaster => _session.HasPaymaster;

        public IReadOnlyList<AccountPaymentMode> AvailablePaymentModes => _session.AvailablePaymentModes;

        public bool ActiveAccountIsPaymasterSponsored => _session.PaymentMode == AccountPaymentMode.PaymasterSponsored;

        private void OnInfraChanged()
        {
            OnPropertyChanged(nameof(HasPaymaster));
            OnPropertyChanged(nameof(AvailablePaymentModes));
            if (!HasPaymaster) SelectedPaymentMode = AccountPaymentMode.SelfFunded;
        }

        private void OnSessionPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SessionState.PaymentMode))
                OnPropertyChanged(nameof(ActiveAccountIsPaymasterSponsored));
        }

        [RelayCommand]
        private Task CreateAccountAsync() => RunAsync(async () =>
        {
            _session.RequireReady();
            var config = _session.WebAuthnConfig!;

            var created = await _credentialFactory.CreateCredentialAsync(new WebAuthnCredentialCreationOptions
            {
                RpId = config.RpId,
                RpName = "Nethereum AA Demo",
                UserName = "demo",
                RequireUserVerification = true
            }).ConfigureAwait(false);

            var account = await _session.Client!.CreateWebAuthnAccountAsync(
                created, _authenticator, config.ValidatorAddress, config.RpId, usePrecompile: false).ConfigureAwait(false);

            SmartAccountAddress = account.Address;
            IsDeployed = account.IsDeployed;
            CredentialId = created.OnChainCredentialId.ToHex(true);

            _session.RaiseAccountChanged(account, "Passkey · WebAuthn P-256");
            var sponsored = SelectedPaymentMode == AccountPaymentMode.PaymasterSponsored && HasPaymaster;
            _session.PaymentMode = sponsored ? AccountPaymentMode.PaymasterSponsored : AccountPaymentMode.SelfFunded;

            var deployedNote = account.IsDeployed
                ? $"Attached to already-deployed passkey account {account.Address}"
                : $"Counterfactual passkey account ready at {account.Address} - it deploys on the first operation";
            StatusMessage = sponsored
                ? $"{deployedNote} - paymaster-sponsored: gas is covered by the paymaster, no funding needed"
                : $"{deployedNote} - self-funded: fund it below before sending";
        });

        [RelayCommand]
        private Task SendAsync() => RunAsync(async () =>
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
                ? $"Passkey-signed operation counted to {Count} (userOpHash {receipt.UserOpHash})"
                : receipt.FailureDiagnostic;
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
                "No active account - create the passkey account first via CreateAccountAsync.");
    }
}
