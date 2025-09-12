using System.Threading.Tasks;

namespace Nethereum.Wallet.UI.Components.Core.Localization
{
    public interface ILocalizationStorageProvider
    {
        Task<string> GetStoredLanguageAsync();
        Task SetStoredLanguageAsync(string languageCode);
        Task<string> GetSystemLanguageAsync();
    }
}