using System.Collections.Generic;

namespace Nethereum.TokenServices.MultiAccount.Models
{
    public class MultiAccountPriceResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public string Currency { get; set; }

        public int TokensUpdated { get; set; }
        public int TokensFailed { get; set; }

        public Dictionary<long, int> UpdatedPerChain { get; set; } = new Dictionary<long, int>();

        public static MultiAccountPriceResult Successful(string currency, int tokensUpdated, Dictionary<long, int> perChain = null)
        {
            return new MultiAccountPriceResult
            {
                Success = true,
                Currency = currency,
                TokensUpdated = tokensUpdated,
                UpdatedPerChain = perChain ?? new Dictionary<long, int>()
            };
        }

        public static MultiAccountPriceResult Failed(string error)
        {
            return new MultiAccountPriceResult
            {
                Success = false,
                ErrorMessage = error
            };
        }
    }
}
