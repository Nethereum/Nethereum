using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Nethereum.ABI.EIP712;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Wallet.Diagnostics;
using Nethereum.Wallet.Hosting;
using Nethereum.Wallet.UI.Components.Services;

namespace Nethereum.Wallet.UI.Components.Prompts
{
    public partial class DAppSignaturePromptViewModel : ObservableObject
    {
        private readonly NethereumWalletHostProvider _walletHostProvider;

        [ObservableProperty] private SignaturePromptInfo? _promptInfo;
        [ObservableProperty] private bool _isSigning;
        [ObservableProperty] private string? _errorMessage;
        [ObservableProperty] private string? _typedDataDomainHash;
        [ObservableProperty] private string? _typedDataMessageHash;
        [ObservableProperty] private string? _typedDataHash;
        [ObservableProperty] private string? _signaturePreview;

        public DAppSignaturePromptViewModel(NethereumWalletHostProvider walletHostProvider)
        {
            _walletHostProvider = walletHostProvider ?? throw new ArgumentNullException(nameof(walletHostProvider));
        }

        public Task InitializeAsync(SignaturePromptInfo info)
        {
            PromptInfo = info;
            ErrorMessage = null;
            IsSigning = false;
            SignaturePreview = null;
            TypedDataDomainHash = null;
            TypedDataMessageHash = null;
            TypedDataHash = null;

            if (string.Equals(info.Method, "eth_signTypedData_v4", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(info.RawMessage))
            {
                ComputeTypedDataHashes(info.RawMessage);
            }

            return Task.CompletedTask;
        }

        public bool HasTypedDataHashes => !string.IsNullOrEmpty(TypedDataHash);
        public bool SignatureReady => !string.IsNullOrEmpty(SignaturePreview);

        public async Task<string?> SignAsync()
        {
            if (PromptInfo == null || string.IsNullOrEmpty(PromptInfo.Address))
            {
                return null;
            }

            IsSigning = true;
            ErrorMessage = null;
            WalletDiagnosticsLogger.Log("SignaturePrompt", $"SignAsync start method={PromptInfo.Method} address={PromptInfo.Address}");

            try
            {
                await _walletHostProvider.InitialiseAccountSignerAsync().ConfigureAwait(false);
                WalletDiagnosticsLogger.Log("SignaturePrompt", "InitialiseAccountSignerAsync completed");
                var web3 = await _walletHostProvider.GetWalletWeb3Async().ConfigureAwait(false);
                WalletDiagnosticsLogger.Log("SignaturePrompt", "GetWalletWeb3Async completed");

                if (string.Equals(PromptInfo.Method, "eth_signTypedData_v4", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrEmpty(PromptInfo.RawMessage))
                    {
                        throw new InvalidOperationException("Typed data payload is missing.");
                    }

                    var typedSignature = await web3.Eth.AccountSigning.SignTypedDataV4
                        .SendRequestAsync(PromptInfo.RawMessage)
                        .ConfigureAwait(false);
                    WalletDiagnosticsLogger.Log("SignaturePrompt", $"Typed data signature complete length={typedSignature?.Length ?? 0}");
                    SignaturePreview = typedSignature;
                    return typedSignature;
                }

                var normalized = NormalizeMessageForSigning(
                    PromptInfo.RawMessage ?? string.Empty,
                    PromptInfo.IsMessageHex);

                var signature = await web3.Eth.AccountSigning.PersonalSign
                    .SendRequestAsync(normalized.HexToByteArray(), PromptInfo.Address)
                    .ConfigureAwait(false);
                WalletDiagnosticsLogger.Log("SignaturePrompt", $"Personal sign complete length={signature?.Length ?? 0}");
                SignaturePreview = signature;
                return signature;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                WalletDiagnosticsLogger.Log("SignaturePrompt", $"SignAsync error: {ex.Message}");
                throw;
            }
            finally
            {
                IsSigning = false;
            }
        }

        private static string NormalizeMessageForSigning(string message, bool isHex)
        {
            if (string.IsNullOrEmpty(message))
            {
                return string.Empty;
            }

            if (isHex || message.IsHex())
            {
                return message;
            }

            return message.ToHexUTF8();
        }

        private void ComputeTypedDataHashes(string jsonPayload)
        {
            try
            {
                var hashResult = Eip712TypedDataEncoder.Current.CalculateTypedDataHashes(jsonPayload);
                TypedDataDomainHash = hashResult.DomainHash.ToHex(true);
                TypedDataMessageHash = hashResult.MessageHash?.ToHex(true);
                TypedDataHash = hashResult.TypedDataHash.ToHex(true);
            }
            catch (Exception ex)
            {
                TypedDataDomainHash = null;
                TypedDataMessageHash = null;
                TypedDataHash = null;
                WalletDiagnosticsLogger.Log("SignaturePrompt", $"Failed to compute typed data hashes: {ex.Message}");
            }
        }

        partial void OnSignaturePreviewChanged(string? value)
        {
            OnPropertyChanged(nameof(SignatureReady));
        }

        partial void OnTypedDataHashChanged(string? value)
        {
            OnPropertyChanged(nameof(HasTypedDataHashes));
        }
    }
}
