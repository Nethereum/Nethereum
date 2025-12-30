using System.Collections.Generic;

namespace Nethereum.Wallet.Services.Tokens.Models
{
    public class DiscoveryResult
    {
        public bool Success { get; set; }
        public bool Completed { get; set; }
        public bool WasCancelled { get; set; }
        public string ErrorMessage { get; set; }
        public int TokensFound { get; set; }
        public int TokensChecked { get; set; }
        public int TotalTokens { get; set; }
        public List<AccountToken> NewTokens { get; set; } = new List<AccountToken>();

        public static DiscoveryResult AlreadyComplete() => new DiscoveryResult
        {
            Success = true,
            Completed = true
        };

        public static DiscoveryResult Cancelled(int tokensChecked, int totalTokens, int tokensFound) => new DiscoveryResult
        {
            Success = true,
            Completed = false,
            WasCancelled = true,
            TokensChecked = tokensChecked,
            TotalTokens = totalTokens,
            TokensFound = tokensFound
        };

        public static DiscoveryResult Failed(string error, int tokensChecked, int totalTokens) => new DiscoveryResult
        {
            Success = false,
            Completed = false,
            ErrorMessage = error,
            TokensChecked = tokensChecked,
            TotalTokens = totalTokens
        };
    }
}
