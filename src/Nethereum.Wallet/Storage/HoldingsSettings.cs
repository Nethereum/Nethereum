using System;
using System.Collections.Generic;

namespace Nethereum.Wallet.Storage
{
    public class HoldingsSettings
    {
        public List<string> SelectedAccountAddresses { get; set; } = new List<string>();
        public List<long> SelectedChainIds { get; set; } = new List<long>();
        public List<string> ForceRescanAccountAddresses { get; set; } = new List<string>();
        public DateTime? LastScanned { get; set; }
    }
}
