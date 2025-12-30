namespace Nethereum.Wallet.Services.Tokens.Models
{
    public class RefreshResult
    {
        public bool BalancesUpdated { get; set; }
        public bool PricesUpdated { get; set; }
        public string BalanceError { get; set; }
        public string PriceError { get; set; }
        public int TokensUpdated { get; set; }
        public int NewTokensFound { get; set; }

        public bool HasBalanceError => !string.IsNullOrEmpty(BalanceError);
        public bool HasPriceError => !string.IsNullOrEmpty(PriceError);
        public bool FullySuccessful => BalancesUpdated && PricesUpdated && !HasBalanceError && !HasPriceError;

        public static RefreshResult Success(int tokensUpdated, int newTokensFound) => new RefreshResult
        {
            BalancesUpdated = true,
            PricesUpdated = true,
            TokensUpdated = tokensUpdated,
            NewTokensFound = newTokensFound
        };

        public static RefreshResult BalancesOnly(int tokensUpdated, int newTokensFound, string priceError) => new RefreshResult
        {
            BalancesUpdated = true,
            PricesUpdated = false,
            TokensUpdated = tokensUpdated,
            NewTokensFound = newTokensFound,
            PriceError = priceError
        };

        public static RefreshResult Failed(string balanceError) => new RefreshResult
        {
            BalancesUpdated = false,
            PricesUpdated = false,
            BalanceError = balanceError
        };
    }
}
