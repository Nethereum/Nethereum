using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace Nethereum.Wallet.UI.Components.Core.Localization
{
    public class SystemLocalizationStorageProvider : ILocalizationStorageProvider
    {
        private readonly string _settingsFilePath;
        
        public SystemLocalizationStorageProvider(string settingsDirectory = null)
        {
            var dir = settingsDirectory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "NethereumWallet"
            );
            
            Directory.CreateDirectory(dir);
            _settingsFilePath = Path.Combine(dir, "language.txt");
        }
        
        public async Task<string> GetStoredLanguageAsync()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    return await File.ReadAllTextAsync(_settingsFilePath);
                }
            }
            catch
            {
            }
            
            return null;
        }
        
        public async Task SetStoredLanguageAsync(string languageCode)
        {
            try
            {
                await File.WriteAllTextAsync(_settingsFilePath, languageCode);
            }
            catch
            {
            }
        }
        
        public Task<string> GetSystemLanguageAsync()
        {
            return Task.FromResult(CultureInfo.CurrentUICulture.Name);
        }
    }
}