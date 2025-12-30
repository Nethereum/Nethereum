using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nethereum.Wallet.Hosting;
using Nethereum.Wallet.Services.Tokens;
using Nethereum.Wallet.Services.Tokens.Models;
using Nethereum.Wallet.UI.Components.Core.Localization;

namespace Nethereum.Wallet.UI.Components.Tokens
{
    public partial class AddCustomTokenViewModel : ObservableObject
    {
        private readonly ITokenManagementService _tokenManagementService;
        private readonly IComponentLocalizer<AddCustomTokenViewModel> _localizer;
        private readonly NethereumWalletHostProvider _walletHostProvider;

        [ObservableProperty] private string _contractAddress = "";
        [ObservableProperty] private string? _contractAddressError;
        [ObservableProperty] private string _symbol = "";
        [ObservableProperty] private string? _symbolError;
        [ObservableProperty] private string _name = "";
        [ObservableProperty] private string? _nameError;
        [ObservableProperty] private string _decimals = "18";
        [ObservableProperty] private string? _decimalsError;
        [ObservableProperty] private string _logoUri = "";
        [ObservableProperty] private bool _isFetchingMetadata = false;
        [ObservableProperty] private bool _isSaving = false;
        [ObservableProperty] private string? _errorMessage;
        [ObservableProperty] private string? _successMessage;

        public string CurrentChainName => _walletHostProvider.ActiveChain?.ChainName ?? $"Chain {_walletHostProvider.SelectedNetworkChainId}";
        public long CurrentChainId => _walletHostProvider.SelectedNetworkChainId;

        public bool IsFormValid =>
            string.IsNullOrEmpty(ContractAddressError) &&
            string.IsNullOrEmpty(SymbolError) &&
            string.IsNullOrEmpty(NameError) &&
            string.IsNullOrEmpty(DecimalsError) &&
            !string.IsNullOrWhiteSpace(ContractAddress) &&
            !string.IsNullOrWhiteSpace(Symbol) &&
            !string.IsNullOrWhiteSpace(Name);

        public Action? OnTokenAdded { get; set; }
        public Action? OnCancel { get; set; }

        public AddCustomTokenViewModel(
            ITokenManagementService tokenManagementService,
            IComponentLocalizer<AddCustomTokenViewModel> localizer,
            NethereumWalletHostProvider walletHostProvider)
        {
            _tokenManagementService = tokenManagementService ?? throw new ArgumentNullException(nameof(tokenManagementService));
            _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
            _walletHostProvider = walletHostProvider ?? throw new ArgumentNullException(nameof(walletHostProvider));
        }

        [RelayCommand]
        public async Task FetchTokenMetadataAsync()
        {
            if (string.IsNullOrWhiteSpace(ContractAddress))
            {
                ContractAddressError = _localizer.GetString(AddCustomTokenLocalizer.Keys.ContractAddressRequired);
                return;
            }

            if (!IsValidAddress(ContractAddress))
            {
                ContractAddressError = _localizer.GetString(AddCustomTokenLocalizer.Keys.ContractAddressInvalid);
                return;
            }

            IsFetchingMetadata = true;
            ErrorMessage = null;
            ContractAddressError = null;

            try
            {
                var web3 = await _walletHostProvider.GetWeb3Async();
                var erc20 = web3.Eth.ERC20.GetContractService(ContractAddress.Trim());

                var symbol = await erc20.SymbolQueryAsync();
                var name = await erc20.NameQueryAsync();
                var decimals = await erc20.DecimalsQueryAsync();

                Symbol = symbol ?? "";
                Name = name ?? "";
                Decimals = decimals.ToString();

                ValidateSymbol();
                ValidateName();
                ValidateDecimals();
            }
            catch (Exception ex)
            {
                ErrorMessage = string.Format(
                    _localizer.GetString(AddCustomTokenLocalizer.Keys.FetchMetadataFailed),
                    ex.Message);
            }
            finally
            {
                IsFetchingMetadata = false;
            }
        }

        [RelayCommand]
        public async Task SaveTokenAsync()
        {
            ValidateContractAddress();
            ValidateSymbol();
            ValidateName();
            ValidateDecimals();

            if (!IsFormValid) return;

            IsSaving = true;
            ErrorMessage = null;
            SuccessMessage = null;

            try
            {
                if (!int.TryParse(Decimals, out var decimalsValue))
                {
                    DecimalsError = _localizer.GetString(AddCustomTokenLocalizer.Keys.DecimalsInvalid);
                    return;
                }

                var customToken = new CustomToken
                {
                    ContractAddress = ContractAddress.Trim(),
                    Symbol = Symbol.Trim(),
                    Name = Name.Trim(),
                    Decimals = decimalsValue,
                    LogoURI = LogoUri?.Trim(),
                    ChainId = _walletHostProvider.SelectedNetworkChainId,
                    AddedAt = DateTime.UtcNow
                };

                var success = await _tokenManagementService.AddCustomTokenAsync(
                    _walletHostProvider.SelectedNetworkChainId,
                    customToken);

                if (success)
                {
                    SuccessMessage = _localizer.GetString(AddCustomTokenLocalizer.Keys.TokenAddedSuccess);
                    OnTokenAdded?.Invoke();
                    ClearForm();
                }
                else
                {
                    ErrorMessage = _localizer.GetString(AddCustomTokenLocalizer.Keys.TokenAddFailed);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsSaving = false;
            }
        }

        private void ValidateContractAddress()
        {
            if (string.IsNullOrWhiteSpace(ContractAddress))
            {
                ContractAddressError = _localizer.GetString(AddCustomTokenLocalizer.Keys.ContractAddressRequired);
            }
            else if (!IsValidAddress(ContractAddress))
            {
                ContractAddressError = _localizer.GetString(AddCustomTokenLocalizer.Keys.ContractAddressInvalid);
            }
            else
            {
                ContractAddressError = null;
            }
        }

        private void ValidateSymbol()
        {
            if (string.IsNullOrWhiteSpace(Symbol))
            {
                SymbolError = _localizer.GetString(AddCustomTokenLocalizer.Keys.SymbolRequired);
            }
            else if (Symbol.Length > 11)
            {
                SymbolError = _localizer.GetString(AddCustomTokenLocalizer.Keys.SymbolTooLong);
            }
            else
            {
                SymbolError = null;
            }
        }

        private void ValidateName()
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                NameError = _localizer.GetString(AddCustomTokenLocalizer.Keys.NameRequired);
            }
            else
            {
                NameError = null;
            }
        }

        private void ValidateDecimals()
        {
            if (string.IsNullOrWhiteSpace(Decimals))
            {
                DecimalsError = _localizer.GetString(AddCustomTokenLocalizer.Keys.DecimalsRequired);
            }
            else if (!int.TryParse(Decimals, out var value) || value < 0 || value > 18)
            {
                DecimalsError = _localizer.GetString(AddCustomTokenLocalizer.Keys.DecimalsInvalid);
            }
            else
            {
                DecimalsError = null;
            }
        }

        partial void OnContractAddressChanged(string value) => ValidateContractAddress();
        partial void OnSymbolChanged(string value) => ValidateSymbol();
        partial void OnNameChanged(string value) => ValidateName();
        partial void OnDecimalsChanged(string value) => ValidateDecimals();

        private void ClearForm()
        {
            ContractAddress = "";
            Symbol = "";
            Name = "";
            Decimals = "18";
            LogoUri = "";
            ContractAddressError = null;
            SymbolError = null;
            NameError = null;
            DecimalsError = null;
        }

        private static bool IsValidAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address)) return false;
            var trimmed = address.Trim();
            return trimmed.StartsWith("0x") && trimmed.Length == 42;
        }
    }
}
