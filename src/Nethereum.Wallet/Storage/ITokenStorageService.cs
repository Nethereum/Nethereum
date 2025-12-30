using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.Wallet.Services.Tokens.Models;

namespace Nethereum.Wallet.Storage
{
    public interface ITokenStorageService
    {
        Task<AccountTokenData> GetAccountTokenDataAsync(string accountAddress, long chainId);
        Task SaveAccountTokenDataAsync(string accountAddress, long chainId, AccountTokenData data);
        Task DeleteAccountTokenDataAsync(string accountAddress, long chainId);

        Task<List<CustomToken>> GetCustomTokensAsync(long chainId);
        Task AddCustomTokenAsync(long chainId, CustomToken token);
        Task UpdateCustomTokenAsync(long chainId, CustomToken token);
        Task DeleteCustomTokenAsync(long chainId, string contractAddress);

        Task<TokenSettings> GetTokenSettingsAsync();
        Task SaveTokenSettingsAsync(TokenSettings settings);
    }
}
