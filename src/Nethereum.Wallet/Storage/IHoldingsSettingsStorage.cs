using System.Threading.Tasks;

namespace Nethereum.Wallet.Storage
{
    public interface IHoldingsSettingsStorage
    {
        Task<HoldingsSettings> GetSettingsAsync();
        Task SaveSettingsAsync(HoldingsSettings settings);
        Task ClearAsync();
    }
}
