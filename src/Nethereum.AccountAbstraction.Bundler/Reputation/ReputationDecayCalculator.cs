namespace Nethereum.AccountAbstraction.Bundler.Reputation
{
    /// <summary>
    /// ERC-4337/7562 hourly reputation decay - the SINGLE shared formula every
    /// IReputationService backend (InMemoryReputationService, RocksDbReputationStore)
    /// applies, so opsSeen/opsIncluded decay identically regardless of storage backend
    /// (the same single-source-of-truth rule as ReputationStatusCalculator).
    ///
    /// Every decay interval opsSeen and opsIncluded are each multiplied by
    /// DecayNumerator/DecayDenominator (23/24) using integer division, so a reformed
    /// entity's throttle/ban clears over time and an entity that has fully decayed to
    /// all-zero opsSeen/opsIncluded is forgotten. This matches the ERC-4337 reputation
    /// spec, which decays each counter independently.
    ///
    /// Note: the reference bundler's ReputationManager.hourlyCron
    /// (packages/bundler/src/modules/ReputationManager.ts:75-85) instead sets
    /// opsIncluded = floor(opsSeen * 23 / 24) - deriving opsIncluded from the just-decayed
    /// opsSeen rather than from opsIncluded. That is a known reference deviation from the
    /// EIP text; this port follows the spec's independent decay of each counter, which is
    /// also the "exponential backoff of opsSeen and opsIncluded" the reference documents.
    /// Decay only ever REDUCES counters, so it can never manufacture a throttle or ban.
    /// </summary>
    public static class ReputationDecayCalculator
    {
        public static bool Decay(ReputationEntry entry, ReputationConfig config)
        {
            entry.OpsSeen = entry.OpsSeen * config.DecayNumerator / config.DecayDenominator;
            entry.OpsIncluded = entry.OpsIncluded * config.DecayNumerator / config.DecayDenominator;
            entry.OpsFailed = entry.OpsFailed * config.DecayNumerator / config.DecayDenominator;
            entry.OpsDropped = entry.OpsDropped * config.DecayNumerator / config.DecayDenominator;

            return entry.OpsSeen == 0 && entry.OpsIncluded == 0;
        }
    }
}
