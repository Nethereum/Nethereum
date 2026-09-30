using System;
using System.Numerics;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nethereum.AccountAbstraction.Client;
using Nethereum.AccountAbstraction.Example.Contracts.TestCounter.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.AccountAbstraction.DTOs;
using Nethereum.Util;

namespace Nethereum.AccountAbstraction.Example.Core
{
    public partial class DiagnosticsViewModel : TabViewModel
    {
        private static readonly UserOperationGasEstimate GenerousFixedEstimate = new()
        {
            CallGasLimit = new HexBigInteger(200_000),
            VerificationGasLimit = new HexBigInteger(500_000),
            PreVerificationGas = new HexBigInteger(100_000)
        };

        private static readonly UserOperationGasEstimate StarvedCallGasEstimate = new()
        {
            CallGasLimit = new HexBigInteger(1),
            VerificationGasLimit = new HexBigInteger(500_000),
            PreVerificationGas = new HexBigInteger(100_000)
        };

        private readonly SessionState _session;

        [ObservableProperty]
        private string? _flavor;

        [ObservableProperty]
        private AATransactionReceipt? _lastReceipt;

        [ObservableProperty]
        private DiagnosticsGasPanel? _gasPanel;

        [ObservableProperty]
        private DiagnosticsBundlerLookup? _bundlerLookup;

        [ObservableProperty]
        private DiagnosticsPackedView? _packedView;

        public int LogCount => LastReceipt?.Logs?.Length ?? 0;

        private const string ForcedOpNotice =
            "(Deliberately forced past the bundler's pre-flight gas-estimate check so it reaches mining " +
            "and yields a receipt to inspect - a real bundler would reject this during " +
            "eth_estimateUserOperationGas with an error, not return a receipt.) ";

        public DiagnosticsViewModel(SessionState session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        partial void OnLastReceiptChanged(AATransactionReceipt? value) => OnPropertyChanged(nameof(LogCount));

        [RelayCommand]
        private Task RunDiagnosedOpAsync() => RunAsync(async () =>
        {
            _session.RequireReady();
            var account = RequireAccount();
            await EnsureFundedAsync(account.Address).ConfigureAwait(false);

            var handler = _session.Client!.Configure(_session.Counter!, account);
            var receipt = (AATransactionReceipt)await handler.SendRequestAndWaitForReceiptAsync(new CountFunction()).ConfigureAwait(false);

            LastReceipt = receipt;
            Flavor = "Diagnosed operation - count()";
            await LoadSentOpDiagnosticsAsync(receipt, handler.GasConfig, handler.EntryPointAddress).ConfigureAwait(false);

            StatusMessage = receipt.UserOpSuccess
                ? $"Diagnosed a successful count() (userOpHash {receipt.UserOpHash}) - see the gas, bundler-lookup and packed-userop panels below"
                : receipt.FailureDiagnostic;
        });

        [RelayCommand]
        private Task RunRevertingOpAsync() => RunAsync(async () =>
        {
            _session.RequireReady();
            var account = RequireAccount();
            await EnsureDeployedAsync(account).ConfigureAwait(false);
            var counter = _session.Counter!;

            var entryPointAddress = _session.Client!.Configure(counter, account).EntryPointAddress;
            var overrideBundler = new DiagnosticsGasOverrideBundlerService(_session.Bundler!, GenerousFixedEstimate);
            var handler = counter.ChangeContractHandlerToAA(account, account.Validator, overrideBundler, entryPointAddress)
                .WithErc7579Execution();

            var receipt = (AATransactionReceipt)await handler.SendRequestAndWaitForReceiptAsync(new CountFailFunction()).ConfigureAwait(false);

            LastReceipt = receipt;
            Flavor = "Reverting operation - countFail()";
            await LoadSentOpDiagnosticsAsync(receipt, handler.GasConfig, entryPointAddress).ConfigureAwait(false);

            StatusMessage = ForcedOpNotice + (receipt.FailureDiagnostic ?? "UNEXPECTED: countFail() succeeded.");
        });

        [RelayCommand]
        private Task RunOutOfGasOpAsync() => RunAsync(async () =>
        {
            _session.RequireReady();
            var account = RequireAccount();
            await EnsureDeployedAsync(account).ConfigureAwait(false);
            var counter = _session.Counter!;

            var entryPointAddress = _session.Client!.Configure(counter, account).EntryPointAddress;
            var overrideBundler = new DiagnosticsGasOverrideBundlerService(_session.Bundler!, StarvedCallGasEstimate);
            var handler = counter.ChangeContractHandlerToAA(account, account.Validator, overrideBundler, entryPointAddress)
                .WithErc7579Execution();

            var receipt = (AATransactionReceipt)await handler.SendRequestAndWaitForReceiptAsync(new CountFunction()).ConfigureAwait(false);

            LastReceipt = receipt;
            Flavor = "Out-of-gas operation - count() with callGasLimit = 1";
            await LoadSentOpDiagnosticsAsync(receipt, handler.GasConfig, entryPointAddress).ConfigureAwait(false);

            StatusMessage = ForcedOpNotice + (receipt.FailureDiagnostic ?? "UNEXPECTED: the starved-gas count() succeeded.");
        });

        private async Task LoadSentOpDiagnosticsAsync(AATransactionReceipt receipt, AAGasConfig gasConfig, string entryPointAddress)
        {
            var lookup = await _session.Bundler!.GetUserOperationByHash.SendRequestAsync(receipt.UserOpHash).ConfigureAwait(false);
            var op = lookup?.UserOperation;

            if (op == null)
            {
                BundlerLookup = null;
                PackedView = null;
                GasPanel = BuildGasPanel(null, receipt, gasConfig);
                return;
            }

            var lookupView = new DiagnosticsBundlerLookup(
                Sender: op.Sender ?? receipt.Sender,
                Nonce: op.Nonce?.Value ?? BigInteger.Zero,
                CallDataHex: string.IsNullOrEmpty(op.CallData) ? "0x" : op.CallData,
                CallGasLimit: op.CallGasLimit?.Value ?? BigInteger.Zero,
                VerificationGasLimit: op.VerificationGasLimit?.Value ?? BigInteger.Zero,
                PreVerificationGas: op.PreVerificationGas?.Value ?? BigInteger.Zero,
                MaxFeePerGas: op.MaxFeePerGas?.Value ?? BigInteger.Zero,
                MaxPriorityFeePerGas: op.MaxPriorityFeePerGas?.Value ?? BigInteger.Zero,
                Paymaster: string.IsNullOrEmpty(op.Paymaster) || op.Paymaster.IsAnEmptyAddress() ? "(none - self-paid)" : op.Paymaster,
                SignatureHex: string.IsNullOrEmpty(op.Signature) ? "0x" : op.Signature,
                EntryPoint: string.IsNullOrEmpty(lookup!.EntryPoint) ? entryPointAddress : lookup.EntryPoint,
                IsPending: lookup.BlockNumber == null,
                BlockNumber: lookup.BlockNumber?.Value,
                TransactionHash: lookup.TransactionHash);

            BundlerLookup = lookupView;
            GasPanel = BuildGasPanel(lookupView, receipt, gasConfig);
            PackedView = BuildPackedView(lookupView);
        }

        private static DiagnosticsGasPanel BuildGasPanel(DiagnosticsBundlerLookup? lookup, AATransactionReceipt receipt, AAGasConfig gasConfig) =>
            new(
                RequestedCallGasLimit: lookup?.CallGasLimit ?? BigInteger.Zero,
                RequestedVerificationGasLimit: lookup?.VerificationGasLimit ?? BigInteger.Zero,
                RequestedPreVerificationGas: lookup?.PreVerificationGas ?? BigInteger.Zero,
                ActualGasUsed: receipt.ActualGasUsed,
                ActualGasCost: receipt.ActualGasCost,
                TransactionGasUsed: receipt.GasUsed?.Value ?? BigInteger.Zero,
                EffectiveGasPrice: receipt.EffectiveGasPrice?.Value ?? BigInteger.Zero,
                CallGasBuffer: gasConfig.CallGasBuffer,
                VerificationGasBuffer: gasConfig.VerificationGasBuffer,
                PreVerificationGasBuffer: gasConfig.PreVerificationGasBuffer,
                CallGasMultiplier: gasConfig.CallGasMultiplier,
                VerificationGasMultiplier: gasConfig.VerificationGasMultiplier);

        private static DiagnosticsPackedView BuildPackedView(DiagnosticsBundlerLookup lookup)
        {
            var accountGasLimitsBytes = UserOperationBuilder.PackAccountGasLimits(lookup.VerificationGasLimit, lookup.CallGasLimit);
            var gasFeesBytes = UserOperationBuilder.PackAccountGasLimits(lookup.MaxPriorityFeePerGas, lookup.MaxFeePerGas);

            var packed = new PackedUserOperation
            {
                Sender = lookup.Sender,
                Nonce = lookup.Nonce,
                CallData = lookup.CallDataHex.HexToByteArray(),
                AccountGasLimits = accountGasLimitsBytes,
                PreVerificationGas = lookup.PreVerificationGas,
                GasFees = gasFeesBytes,
                PaymasterAndData = Array.Empty<byte>(),
                Signature = lookup.SignatureHex.HexToByteArray()
            };

            var (verificationGasLimit, callGasLimit) = packed.UnpackAccountGasLimits();
            var (maxPriorityFeePerGas, maxFeePerGas) = packed.UnpackGasFees();
            var (paymasterVerificationGasLimit, paymasterPostOpGasLimit) = packed.UnpackPaymasterGasLimits();

            return new DiagnosticsPackedView(
                AccountGasLimitsHex: accountGasLimitsBytes.ToHex(true),
                GasFeesHex: gasFeesBytes.ToHex(true),
                UnpackedVerificationGasLimit: verificationGasLimit,
                UnpackedCallGasLimit: callGasLimit,
                UnpackedMaxPriorityFeePerGas: maxPriorityFeePerGas,
                UnpackedMaxFeePerGas: maxFeePerGas,
                UnpackedPaymasterVerificationGasLimit: paymasterVerificationGasLimit,
                UnpackedPaymasterPostOpGasLimit: paymasterPostOpGasLimit,
                TotalGas: packed.GetTotalGas());
        }

        private async Task EnsureFundedAsync(string address)
        {
            var balance = await _session.Faucet!.GetBalanceAsync(address).ConfigureAwait(false);
            if (balance == BigInteger.Zero)
                await _session.Faucet!.FundAsync(address).ConfigureAwait(false);
        }

        private async Task EnsureDeployedAsync(NethereumSmartAccount account)
        {
            var code = await _session.Counter!.Web3.Eth.GetCode.SendRequestAsync(account.Address).ConfigureAwait(false);
            if (string.IsNullOrEmpty(code) || code == "0x")
                throw new InvalidOperationException(
                    "The active account is not deployed yet - run the diagnosed operation first (it " +
                    "deploys the account on its first successful operation) before exploring the " +
                    "revert/out-of-gas flavors.");
        }

        private NethereumSmartAccount RequireAccount() =>
            _session.Account ?? throw new InvalidOperationException(
                "No active account - create or attach one first via SetupViewModel.CreateAccountAsync.");
    }
}
