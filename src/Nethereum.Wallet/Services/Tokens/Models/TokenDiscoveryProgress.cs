namespace Nethereum.Wallet.Services.Tokens.Models
{
    public class TokenDiscoveryProgress
    {
        public int CheckedTokens { get; set; }
        public int TotalTokens { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int TokensFoundSoFar { get; set; }
        public string LastCheckedAddress { get; set; }

        public double PercentComplete => TotalTokens > 0
            ? (double)CheckedTokens / TotalTokens * 100
            : 0;

        public bool IsComplete => TotalTokens > 0 && CheckedTokens >= TotalTokens;
    }
}
