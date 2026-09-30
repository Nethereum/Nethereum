using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nethereum.AccountAbstraction.Client;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccount;
using Nethereum.AccountAbstraction.ERC7579.Modules;
using Nethereum.AccountAbstraction.WebAuthn.Client;
using Nethereum.AccountAbstraction.WebAuthn.ERC7579.Modules;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.WebAuthn;

namespace Nethereum.AccountAbstraction.Example.Core
{
    public partial class ModulesViewModel : TabViewModel
    {
        private readonly SessionState _session;
        private readonly IWebAuthnCredentialFactory _credentialFactory;
        private readonly IWebAuthnAuthenticator _authenticator;

        private WebAuthnCreatedCredential? _credential;

        [ObservableProperty]
        private bool _isPasskeyValidatorInstalled;

        [ObservableProperty]
        private string? _credentialId;

        [ObservableProperty]
        private AATransactionReceipt? _lastReceipt;

        public string? ModuleAddress => _session.WebAuthnConfig?.ValidatorAddress;

        public ModulesViewModel(
            SessionState session,
            IWebAuthnCredentialFactory credentialFactory,
            IWebAuthnAuthenticator authenticator)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _credentialFactory = credentialFactory ?? throw new ArgumentNullException(nameof(credentialFactory));
            _authenticator = authenticator ?? throw new ArgumentNullException(nameof(authenticator));
        }

        [RelayCommand]
        private Task InstallPasskeyValidatorAsync() => RunAsync(async () =>
        {
            _session.RequireReady();
            var account = RequireAccount();
            var config = _session.WebAuthnConfig!;

            _credential = await _credentialFactory.CreateCredentialAsync(new WebAuthnCredentialCreationOptions
            {
                RpId = config.RpId,
                RpName = "Nethereum AA Demo",
                UserName = "demo-second-signer",
                RequireUserVerification = true
            }).ConfigureAwait(false);

            var moduleConfig = new WebAuthnValidatorConfig(config.ValidatorAddress, threshold: 1, _credential.ToCredential());

            var accountService = new NethereumAccountService(_session.Web3!, account.Address);
            accountService.UseAccountAbstraction(account, _session.Client!);

            var receipt = (AATransactionReceipt)await accountService.InstallModuleAndWaitForReceiptAsync(moduleConfig).ConfigureAwait(false);
            LastReceipt = receipt;
            CredentialId = _credential.OnChainCredentialId.ToHex(true);

            IsPasskeyValidatorInstalled = await accountService.IsModuleInstalledAsync(moduleConfig).ConfigureAwait(false);
            StatusMessage = receipt.UserOpSuccess
                ? $"Passkey installed as a second validator on {account.Address} (userOpHash {receipt.UserOpHash}) - isModuleInstalled: {IsPasskeyValidatorInstalled}"
                : receipt.FailureDiagnostic;
        });

        [RelayCommand]
        private Task ProvePasskeyCanSignAsync() => RunAsync(async () =>
        {
            _session.RequireReady();
            var account = RequireAccount();
            var config = _session.WebAuthnConfig!;
            var counter = _session.Counter!;
            var credential = _credential ?? throw new InvalidOperationException(
                "No passkey installed yet - install the passkey validator first.");

            var passkeyAccount = _session.Client!.GetWebAuthnAccount(
                account.Address, credential, _authenticator, config.ValidatorAddress, config.RpId, usePrecompile: false);

            _session.Client!.Configure(counter, passkeyAccount);

            var receipt = (AATransactionReceipt)await counter.CountRequestAndWaitForReceiptAsync().ConfigureAwait(false);
            LastReceipt = receipt;

            StatusMessage = receipt.UserOpSuccess
                ? $"Passkey-signed operation succeeded on {account.Address} (userOpHash {receipt.UserOpHash}) - the installed module validated it, not the account's original signer"
                : receipt.FailureDiagnostic;
        });

        [RelayCommand]
        private Task UninstallPasskeyValidatorAsync() => RunAsync(async () =>
        {
            _session.RequireReady();
            var account = RequireAccount();
            var config = _session.WebAuthnConfig!;
            var credential = _credential ?? throw new InvalidOperationException(
                "No passkey installed yet - install the passkey validator first.");

            var moduleConfig = new WebAuthnValidatorConfig(config.ValidatorAddress, threshold: 1, credential.ToCredential());

            var accountService = new NethereumAccountService(_session.Web3!, account.Address);
            accountService.UseAccountAbstraction(account, _session.Client!);

            var receipt = (AATransactionReceipt)await accountService.UninstallModuleAndWaitForReceiptAsync(moduleConfig).ConfigureAwait(false);
            LastReceipt = receipt;

            IsPasskeyValidatorInstalled = await accountService.IsModuleInstalledAsync(moduleConfig).ConfigureAwait(false);
            StatusMessage = receipt.UserOpSuccess
                ? $"Passkey validator removed from {account.Address} (userOpHash {receipt.UserOpHash}) - isModuleInstalled: {IsPasskeyValidatorInstalled}"
                : receipt.FailureDiagnostic;

            if (receipt.UserOpSuccess)
                _credential = null;
        });

        private NethereumSmartAccount RequireAccount() =>
            _session.Account ?? throw new InvalidOperationException(
                "No active account - create and fund a modular account in Setup first.");
    }
}
