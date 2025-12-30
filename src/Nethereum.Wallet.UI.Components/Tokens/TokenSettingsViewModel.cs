using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nethereum.Wallet.Services.Tokens.Models;
using Nethereum.Wallet.Storage;
using Nethereum.Wallet.UI.Components.Core.Localization;

namespace Nethereum.Wallet.UI.Components.Tokens
{
    public partial class TokenSettingsViewModel : ObservableObject
    {
        private readonly ITokenStorageService _tokenStorageService;
        private readonly IComponentLocalizer<TokenSettingsViewModel> _localizer;

        [ObservableProperty] private string _selectedCurrency = "usd";
        [ObservableProperty] private int _refreshIntervalSeconds = 300;
        [ObservableProperty] private bool _autoRefreshPrices = true;
        [ObservableProperty] private bool _isLoading = false;
        [ObservableProperty] private bool _isSaving = false;
        [ObservableProperty] private string? _errorMessage;
        [ObservableProperty] private string? _successMessage;

        public List<CurrencyOption> AvailableCurrencies { get; } = SupportedCurrencies.Currencies
            .Select(c => new CurrencyOption
            {
                Code = c.Key,
                Symbol = c.Value,
                DisplayName = $"{c.Value} ({c.Key.ToUpperInvariant()})"
            })
            .ToList();

        public Action? OnSettingsSaved { get; set; }

        public TokenSettingsViewModel(
            ITokenStorageService tokenStorageService,
            IComponentLocalizer<TokenSettingsViewModel> localizer)
        {
            _tokenStorageService = tokenStorageService ?? throw new ArgumentNullException(nameof(tokenStorageService));
            _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
        }

        [RelayCommand]
        public async Task LoadSettingsAsync()
        {
            IsLoading = true;
            ErrorMessage = null;

            try
            {
                var settings = await _tokenStorageService.GetTokenSettingsAsync();
                SelectedCurrency = settings.Currency;
                RefreshIntervalSeconds = settings.RefreshIntervalSeconds;
                AutoRefreshPrices = settings.AutoRefreshPrices;
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        public async Task SaveSettingsAsync()
        {
            IsSaving = true;
            ErrorMessage = null;
            SuccessMessage = null;

            try
            {
                var currencyOption = AvailableCurrencies.FirstOrDefault(c => c.Code == SelectedCurrency);
                var settings = new TokenSettings
                {
                    Currency = SelectedCurrency,
                    CurrencySymbol = currencyOption?.Symbol ?? "$",
                    RefreshIntervalSeconds = RefreshIntervalSeconds,
                    AutoRefreshPrices = AutoRefreshPrices
                };

                await _tokenStorageService.SaveTokenSettingsAsync(settings);
                SuccessMessage = _localizer.GetString(TokenSettingsLocalizer.Keys.SettingsSaved);
                OnSettingsSaved?.Invoke();
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
    }

    public class CurrencyOption
    {
        public string Code { get; set; }
        public string Symbol { get; set; }
        public string DisplayName { get; set; }
    }
}
