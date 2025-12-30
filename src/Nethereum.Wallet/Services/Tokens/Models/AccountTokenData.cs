using System;
using System.Collections.Generic;

namespace Nethereum.Wallet.Services.Tokens.Models
{
    public class AccountTokenData
    {
        public List<AccountToken> Tokens { get; set; } = new List<AccountToken>();

        public bool DiscoveryComplete { get; set; }
        public TokenDiscoveryProgress DiscoveryProgress { get; set; }
        public ulong? DiscoveryCompletedAtBlock { get; set; }

        public ulong LastScannedBlock { get; set; }
        public DateTime? LastEventScan { get; set; }

        public DateTime? LastPriceUpdate { get; set; }

        public ChainScanStatus ScanStatus { get; set; } = new ChainScanStatus();
    }
}
