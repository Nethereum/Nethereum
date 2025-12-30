using System.Collections.Generic;

namespace Nethereum.Wallet.Services.Tokens.Models
{
    public class EventScanResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public int TokensUpdated { get; set; }
        public int NewTokensFound { get; set; }
        public ulong FromBlock { get; set; }
        public ulong ToBlock { get; set; }
        public int EventsProcessed { get; set; }
        public List<string> UpdatedAddresses { get; set; } = new List<string>();

        public static EventScanResult NoChanges(ulong fromBlock, ulong toBlock) => new EventScanResult
        {
            Success = true,
            FromBlock = fromBlock,
            ToBlock = toBlock
        };

        public static EventScanResult Failed(string error) => new EventScanResult
        {
            Success = false,
            ErrorMessage = error
        };
    }
}
