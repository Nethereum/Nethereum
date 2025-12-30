using System.Collections.Generic;

namespace Nethereum.Wallet.Services.Tokens.Models
{
    public class TokenScanResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public List<AccountToken> TokensWithBalance { get; set; } = new List<AccountToken>();
        public int TotalTokensScanned { get; set; }
        public int TokensWithBalanceCount { get; set; }
    }
}
