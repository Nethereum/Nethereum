using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nethereum.Accounts.AccountMessageSigning;
using Nethereum.AccountAbstraction.Client;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccount;
using Nethereum.AccountAbstraction.Contracts.Modules.Native.ECDSAValidator;
using Nethereum.AccountAbstraction.ERC7579.Modules;
using Nethereum.AccountAbstraction.ERC7579.Modules.MultiGuardian;
using Nethereum.AccountAbstraction.ERC7579.Modules.SocialRecovery;
using Nethereum.AccountAbstraction.Signing;
using Nethereum.AccountAbstraction.Validation;
using Nethereum.JsonRpc.Client;
using Nethereum.Signer;

namespace Nethereum.AccountAbstraction.Example.Core
{
    public partial class SocialRecoveryViewModel : TabViewModel
    {
        private const int DefaultGuardianCount = 3;
        private const int DefaultThreshold = 2;

        private readonly SessionState _session;

        private EthECKey[]? _guardianKeys;
        private int _installedThreshold;
        private EthECKey? _recoveredOwnerKey;

        [ObservableProperty]
        private int _guardianCount = DefaultGuardianCount;

        [ObservableProperty]
        private int _threshold = DefaultThreshold;

        [ObservableProperty]
        private bool _isGuardiansInstalled;

        [ObservableProperty]
        private string[]? _guardianAddresses;

        [ObservableProperty]
        private int _installedThresholdDisplay;

        [ObservableProperty]
        private AATransactionReceipt? _lastReceipt;

        [ObservableProperty]
        private string? _newOwnerAddress;

        [ObservableProperty]
        private string? _rotatedOwnerAddress;

        [ObservableProperty]
        private bool _isRecoveredAccountActive;

        [ObservableProperty]
        private BigInteger _provedCount;

        [ObservableProperty]
        private bool? _underThresholdRejected;

        public string? SocialRecoveryAddress => _session.SocialRecoveryConfig?.SocialRecoveryAddress;

        public SocialRecoveryViewModel(SessionState session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        [RelayCommand]
        private Task SetupGuardiansAsync() => RunAsync(async () =>
        {
            _session.RequireReady();
            var account = RequireAccount();
            var config = _session.SocialRecoveryConfig!;

            if (GuardianCount < 2)
                throw new ArgumentOutOfRangeException(nameof(GuardianCount), GuardianCount,
                    "At least 2 guardians are required - a single guardian is not a quorum.");
            if (Threshold < 1 || Threshold > GuardianCount)
                throw new ArgumentOutOfRangeException(nameof(Threshold), Threshold,
                    $"Threshold must be between 1 and the guardian count ({GuardianCount}).");

            var guardianKeys = Enumerable.Range(0, GuardianCount).Select(_ => EthECKey.GenerateKey()).ToArray();
            var sortedGuardians = SortAscending(guardianKeys);
            var threshold = Threshold;

            var accountService = new NethereumAccountService(_session.Web3!, account.Address);
            accountService.UseAccountAbstraction(account, _session.Client!);

            var receipt = (AATransactionReceipt)await accountService.InstallSocialRecoveryAndWaitForReceiptAsync(
                config.SocialRecoveryAddress, threshold, sortedGuardians).ConfigureAwait(false);
            LastReceipt = receipt;

            var moduleConfig = SocialRecoveryConfig.Create(config.SocialRecoveryAddress, threshold, sortedGuardians);
            IsGuardiansInstalled = await accountService.IsModuleInstalledAsync(moduleConfig).ConfigureAwait(false);

            if (IsGuardiansInstalled)
            {
                _guardianKeys = guardianKeys;
                _installedThreshold = threshold;
                GuardianAddresses = sortedGuardians;
                InstalledThresholdDisplay = threshold;
                _recoveredOwnerKey = null;
                NewOwnerAddress = null;
                RotatedOwnerAddress = null;
                IsRecoveredAccountActive = false;
                ProvedCount = BigInteger.Zero;
                UnderThresholdRejected = null;
            }

            StatusMessage = receipt.UserOpSuccess
                ? $"SocialRecovery installed on {account.Address} with a {threshold}-of-{GuardianCount} guardian quorum " +
                  $"(userOpHash {receipt.UserOpHash}) - isModuleInstalled: {IsGuardiansInstalled}"
                : receipt.FailureDiagnostic;
        });

        [RelayCommand]
        private Task RecoverAsync() => RunAsync(async () =>
        {
            _session.RequireReady();
            var account = RequireAccount();
            var config = _session.SocialRecoveryConfig!;
            var guardianKeys = RequireGuardianKeys();

            var newOwner = EthECKey.GenerateKey();
            var newOwnerAddress = newOwner.GetPublicAddress();

            var guardianSigningService = new MultiGuardianSigningService(guardianKeys, _installedThreshold);
            var socialRecoveryValidator = new SocialRecoveryValidatorModule(config.SocialRecoveryAddress, _installedThreshold);
            var recoveryAccount = _session.Client!.GetAccount(account.Address, guardianSigningService, socialRecoveryValidator);

            var ecdsaValidatorService = new ECDSAValidatorService(_session.Web3!, config.EcdsaValidatorAddress);
            ecdsaValidatorService.UseAccountAbstraction(recoveryAccount, _session.Client!);

            var receipt = (AATransactionReceipt)await ecdsaValidatorService.TransferOwnershipRequestAndWaitForReceiptAsync(newOwnerAddress).ConfigureAwait(false);
            LastReceipt = receipt;

            RotatedOwnerAddress = await new ECDSAValidatorService(_session.Web3!, config.EcdsaValidatorAddress)
                .GetOwnerQueryAsync(account.Address).ConfigureAwait(false);

            if (receipt.UserOpSuccess)
            {
                NewOwnerAddress = newOwnerAddress;
                _recoveredOwnerKey = newOwner;
            }

            StatusMessage = receipt.UserOpSuccess
                ? $"Guardian quorum ({_installedThreshold}-of-{guardianKeys.Length}) rotated the owner of {account.Address} " +
                  $"to {newOwnerAddress} (userOpHash {receipt.UserOpHash}) - GetOwner now returns {RotatedOwnerAddress}"
                : receipt.FailureDiagnostic;
        });

        [RelayCommand]
        private Task SwitchToRecoveredOwnerAsync() => RunAsync(async () =>
        {
            _session.RequireReady();
            var account = RequireAccount();
            var config = _session.SocialRecoveryConfig!;
            var counter = _session.Counter!;
            var newOwnerKey = _recoveredOwnerKey ?? throw new InvalidOperationException(
                "No completed recovery yet - rotate the owner first via RecoverCommand.");

            var recoveredAccount = _session.Client!.GetAccount(
                account.Address,
                new AccountSigningOfflineService(newOwnerKey),
                new EcdsaValidatorModule(config.EcdsaValidatorAddress));

            _session.RaiseAccountChanged(recoveredAccount, "Recovered - controlled by new owner key (post social-recovery)");

            counter.UseAccountAbstraction(recoveredAccount, _session.Client!);
            var receipt = (AATransactionReceipt)await counter.CountRequestAndWaitForReceiptAsync().ConfigureAwait(false);
            LastReceipt = receipt;
            ProvedCount = await counter.CountersQueryAsync(recoveredAccount.Address).ConfigureAwait(false);
            IsRecoveredAccountActive = receipt.UserOpSuccess;

            StatusMessage = receipt.UserOpSuccess
                ? $"Active account is now controlled by the new owner key {NewOwnerAddress} - count() succeeded " +
                  $"(userOpHash {receipt.UserOpHash}), the new signer genuinely authorises operations, not merely GetOwner"
                : receipt.FailureDiagnostic;
        });

        [RelayCommand]
        private Task TryUnderThresholdRecoveryAsync() => RunAsync(async () =>
        {
            _session.RequireReady();
            var account = RequireAccount();
            var config = _session.SocialRecoveryConfig!;
            var guardianKeys = RequireGuardianKeys();

            if (_installedThreshold < 2)
                throw new InvalidOperationException(
                    "The installed threshold is already 1 - there is no smaller quorum to demonstrate rejection with.");

            var partialGuardianKeys = guardianKeys.Take(_installedThreshold - 1).ToArray();
            var partialThreshold = partialGuardianKeys.Length;

            var ownerBefore = await new ECDSAValidatorService(_session.Web3!, config.EcdsaValidatorAddress)
                .GetOwnerQueryAsync(account.Address).ConfigureAwait(false);

            var partialSigningService = new MultiGuardianSigningService(partialGuardianKeys, partialThreshold);
            var partialValidator = new SocialRecoveryValidatorModule(config.SocialRecoveryAddress, partialThreshold);
            var partialAccount = _session.Client!.GetAccount(account.Address, partialSigningService, partialValidator);

            var attemptedNewOwner = EthECKey.GenerateKey();
            var ecdsaValidatorService = new ECDSAValidatorService(_session.Web3!, config.EcdsaValidatorAddress);
            ecdsaValidatorService.UseAccountAbstraction(partialAccount, _session.Client!);

            try
            {
                await ecdsaValidatorService.TransferOwnershipRequestAndWaitForReceiptAsync(attemptedNewOwner.GetPublicAddress()).ConfigureAwait(false);
                UnderThresholdRejected = false;
                StatusMessage = $"UNEXPECTED: {partialThreshold}-of-{_installedThreshold} guardians (below the required threshold) " +
                                 $"were able to recover {account.Address} - the on-chain quorum check did not hold.";
            }
            catch (InvalidOperationException ex) when (ex.InnerException is RpcResponseException rpcEx &&
                                                         rpcEx.RpcError.Code == Erc7769ErrorCodes.SimulateValidation)
            {
                UnderThresholdRejected = true;
                StatusMessage = $"Recovery attempt with only {partialThreshold} of the required {_installedThreshold} guardian " +
                                 $"signatures was REJECTED as expected during gas estimation (AA23): {rpcEx.RpcError.Message}";
            }

            var ownerAfter = await new ECDSAValidatorService(_session.Web3!, config.EcdsaValidatorAddress)
                .GetOwnerQueryAsync(account.Address).ConfigureAwait(false);
            if (!string.Equals(ownerBefore, ownerAfter, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"Owner changed from {ownerBefore} to {ownerAfter} despite the under-threshold recovery attempt - " +
                    "the on-chain quorum check did not hold.");
        });

        private static string[] SortAscending(EthECKey[] keys) =>
            MultiGuardianSignatureBlobBuilder.SortAddressesAscending(keys.Select(k => k.GetPublicAddress()));

        private NethereumSmartAccount RequireAccount() =>
            _session.Account ?? throw new InvalidOperationException(
                "No active account - create one first via Setup.");

        private EthECKey[] RequireGuardianKeys() =>
            _guardianKeys ?? throw new InvalidOperationException(
                "No guardians installed yet - set them up first via SetupGuardiansCommand.");
    }
}
