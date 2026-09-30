using System;
using System.Numerics;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nethereum.AccountAbstraction.Client;
using Nethereum.Signer;

namespace Nethereum.AccountAbstraction.Example.Core
{
    public partial class Eip7702AccountViewModel : TabViewModel
    {
        private readonly SessionState _session;

        private EthECKey? _eoaKey;

        [ObservableProperty]
        private string? _eoaAddress;

        [ObservableProperty]
        private bool _isUpgraded;

        [ObservableProperty]
        private AATransactionReceipt? _lastReceipt;

        [ObservableProperty]
        private BigInteger _count;

        public Eip7702AccountViewModel(SessionState session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        [RelayCommand]
        private Task GenerateEoaAsync() => RunAsync(() =>
        {
            _eoaKey = EthECKey.GenerateKey();
            EoaAddress = _eoaKey.GetPublicAddress();
            IsUpgraded = false;
            LastReceipt = null;
            Count = BigInteger.Zero;
            StatusMessage = $"Generated plain EOA {EoaAddress} - a normal wallet, no smart-account code yet";
            return Task.CompletedTask;
        });

        [RelayCommand]
        private Task FundEoaAsync() => RunAsync(async () =>
        {
            _session.RequireReady();
            var address = RequireEoaAddress();
            await _session.Faucet!.FundAsync(address).ConfigureAwait(false);
            var balance = await _session.Faucet!.GetBalanceAsync(address).ConfigureAwait(false);
            StatusMessage = $"Funded {address} with 10 ETH - balance now {Nethereum.Web3.Web3.Convert.FromWei(balance)} ETH";
        });

        [RelayCommand]
        private Task UpgradeAndSendAsync() => RunAsync(async () =>
        {
            _session.RequireReady();
            var key = _eoaKey ?? throw new InvalidOperationException(
                "No EOA yet - generate one first via GenerateEoaAsync.");
            var config = _session.Eip7702Config!;
            var counter = _session.Counter!;

            var account = _session.Client!.CreateEip7702Account(key, config.EcdsaValidatorAddress);
            _session.Client!.ConfigureEip7702(counter, account, key, config.AccountImplementationAddress);

            var receipt = (AATransactionReceipt)await counter.CountRequestAndWaitForReceiptAsync().ConfigureAwait(false);
            LastReceipt = receipt;
            Count = await counter.CountersQueryAsync(account.Address).ConfigureAwait(false);
            IsUpgraded = receipt.UserOpSuccess;

            _session.RaiseAccountChanged(
                new NethereumSmartAccount(account.Address, account.AccountSigningService, account.Validator, isDeployed: true),
                "EOA · 7702-upgraded (secp256k1)");

            StatusMessage = receipt.UserOpSuccess
                ? $"Upgraded {account.Address} in place via EIP-7702 and counted to {Count} (userOpHash {receipt.UserOpHash}) - same address, now a smart account"
                : receipt.FailureDiagnostic;
        });

        private string RequireEoaAddress() =>
            EoaAddress ?? throw new InvalidOperationException("No EOA yet - generate one first via GenerateEoaAsync.");
    }
}
