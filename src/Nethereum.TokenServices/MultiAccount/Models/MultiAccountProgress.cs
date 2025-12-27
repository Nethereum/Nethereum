using System.Collections.Generic;

namespace Nethereum.TokenServices.MultiAccount.Models
{
    public class MultiAccountProgress
    {
        public int TotalAccounts { get; set; }
        public int CompletedAccounts { get; set; }
        public int TotalChains { get; set; }
        public int CompletedChains { get; set; }

        public long? CurrentChainId { get; set; }
        public string CurrentChainName { get; set; }
        public string CurrentAccount { get; set; }

        public int TotalTokensToCheck { get; set; }
        public int TokensChecked { get; set; }
        public int TokensFound { get; set; }

        public Dictionary<long, ChainProgress> ChainProgress { get; set; } = new Dictionary<long, ChainProgress>();

        public double OverallPercentComplete => TotalChains > 0
            ? (double)CompletedChains / TotalChains * 100
            : 0;
    }

    public class ChainProgress
    {
        public long ChainId { get; set; }
        public string ChainName { get; set; }
        public bool IsScanning { get; set; }
        public bool IsComplete { get; set; }
        public bool HasError { get; set; }
        public string ErrorMessage { get; set; }
        public int TokensChecked { get; set; }
        public int TokensFound { get; set; }
        public int TotalTokens { get; set; }

        public double ProgressPercent => TotalTokens > 0
            ? (double)TokensChecked / TotalTokens * 100
            : 0;
    }
}
