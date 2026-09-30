using System;
using System.Collections.Generic;

namespace Nethereum.AccountAbstraction.Bundler.Mempool
{
    public static class MultipleRolesRule
    {
        public static string? Detect(
            string? sender, string? paymaster, string? factory, IEnumerable<MempoolEntry> pending)
        {
            var knownSenders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var knownEntities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in pending)
            {
                if (!string.IsNullOrEmpty(entry.UserOperation?.Sender)) knownSenders.Add(entry.UserOperation.Sender);
                if (!string.IsNullOrEmpty(entry.Paymaster)) knownEntities.Add(entry.Paymaster);
                if (!string.IsNullOrEmpty(entry.Factory)) knownEntities.Add(entry.Factory);
            }

            if (!string.IsNullOrEmpty(sender) && knownEntities.Contains(sender))
            {
                return $"The sender address \"{sender}\" is used as a different entity in another UserOperation currently in mempool";
            }

            if (!string.IsNullOrEmpty(paymaster) && knownSenders.Contains(paymaster))
            {
                return $"A Paymaster at {paymaster} in this UserOperation is used as a sender entity in another UserOperation currently in mempool.";
            }

            if (!string.IsNullOrEmpty(factory) && knownSenders.Contains(factory))
            {
                return $"A Factory at {factory} in this UserOperation is used as a sender entity in another UserOperation currently in mempool.";
            }

            return null;
        }
    }
}
