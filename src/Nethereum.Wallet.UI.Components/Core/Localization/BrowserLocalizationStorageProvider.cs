using System;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace Nethereum.Wallet.UI.Components.Core.Localization
{
    public class BrowserLocalizationStorageProvider : ILocalizationStorageProvider
    {
        private readonly IJSRuntime _jsRuntime;
        private const string StorageKey = "wallet-ui-language";
        
        public BrowserLocalizationStorageProvider(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
        }
        
        public async Task<string> GetStoredLanguageAsync()
        {
            try
            {
                return await _jsRuntime.InvokeAsync<string>("localStorage.getItem", StorageKey);
            }
            catch
            {
                return null;
            }
        }
        
        public async Task SetStoredLanguageAsync(string languageCode)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, languageCode);
            }
            catch
            {
            }
        }
        
        public async Task<string> GetSystemLanguageAsync()
        {
            try
            {
                return await _jsRuntime.InvokeAsync<string>("eval", 
                    "navigator.language || navigator.userLanguage || 'en-US'");
            }
            catch
            {
                return "en-US";
            }
        }
    }
}