using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Nethereum.TokenServices.Caching;

namespace Nethereum.Wallet.Storage
{
    public class FileHoldingsSettingsStorage : FileStorageBase, IHoldingsSettingsStorage
    {
        private const string SettingsFileName = "holdings-settings.json";

        public FileHoldingsSettingsStorage(
            string baseDirectory = null,
            JsonSerializerOptions jsonOptions = null,
            Action<string, Exception> onError = null)
            : base(
                baseDirectory ?? GetDefaultDirectory(),
                jsonOptions ?? CreateDefaultJsonOptions(),
                onError)
        {
        }

        private static string GetDefaultDirectory()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Nethereum", "Wallet", "holdings");
        }

        public async Task<HoldingsSettings> GetSettingsAsync()
        {
            return await ReadAsync<HoldingsSettings>(SettingsFileName).ConfigureAwait(false)
                ?? new HoldingsSettings();
        }

        public Task SaveSettingsAsync(HoldingsSettings settings)
        {
            return WriteAsync(SettingsFileName, settings);
        }

        public Task ClearAsync()
        {
            return DeleteAsync(SettingsFileName);
        }
    }
}
