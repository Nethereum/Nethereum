using System;
using System.Collections.Generic;

namespace Nethereum.BlockchainProcessing.BlockProcessing
{
    public enum LogStorageMode
    {
        All,
        Selective
    }

    public class LogStorageOptions
    {
        public LogStorageMode Mode { get; set; } = LogStorageMode.All;

        public List<string> EventSignatures { get; set; } = new List<string>
        {
            "0xddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef",
            "0x8c5be1e5ebec7d5bd14f71427d1e84f3dd0314c0f7b2291e5b200ac8c7c3b925",
            "0xc3d58168c5ae7397731d063d5bbf3d657854427343f4c083240f7aacaa2d0f62",
            "0x4a39dc06d4c0dbc64b70af90fd698a233a518aa5d07e595d983b8c0526c8f7fb"
        };

        public Dictionary<string, List<string>> ContractOverrides { get; set; } = new Dictionary<string, List<string>>();

        public bool ShouldStoreLog(string eventSignature, string contractAddress)
        {
            if (Mode == LogStorageMode.All) return true;
            if (string.IsNullOrEmpty(eventSignature)) return false;

            var sigLower = eventSignature.ToLowerInvariant();

            if (EventSignatures != null)
            {
                foreach (var sig in EventSignatures)
                {
                    if (string.Equals(sig, sigLower, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            if (ContractOverrides != null && !string.IsNullOrEmpty(contractAddress))
            {
                var contractLower = contractAddress.ToLowerInvariant();
                if (ContractOverrides.TryGetValue(contractLower, out var overrideSigs))
                {
                    foreach (var sig in overrideSigs)
                    {
                        if (string.Equals(sig, sigLower, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                }
            }

            return false;
        }
    }
}
