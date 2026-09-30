using System.Collections.Generic;
using Nethereum.EVM;
using Nethereum.EVM.Gas.Intrinsic;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.EEST.ConformanceRunner
{
    /// <summary>
    /// AMS-BLOB-01 for blockchain_tests: a fixture near/at a blob-schedule fork boundary
    /// (fusaka-devnet-5 osaka/eip7918_blob_reserve_price) states the target/max/baseFeeUpdateFraction
    /// its filler used in config.blobSchedule per fork, exactly like geth's consume-rlp/consume-engine
    /// harness reads it, rather than the runner silently substituting its own compiled-in mainnet
    /// constants. Same fraction-only pattern <see cref="EestStateTestsDriver"/> already applies to
    /// state_tests, extended with target/max since a blockchain_tests fixture is validated at the
    /// block-header level (<see cref="Nethereum.CoreChain.BlockExecutor.ExpectedExcessBlobGas"/> reads
    /// HardforkConfig.MaxBlobsPerBlock/TargetBlobsPerBlock as well as the blob gas rule's
    /// BaseFeeUpdateFraction), not just the leaf blob-fee calculation state_tests exercises.
    ///
    /// Builds a registry through the existing configurable-registry seam
    /// (HardforkConfig.Clone + IntrinsicGasRules.WithBlob) rather than any production change -
    /// FullNodeHarness.ComposeAsync already takes a HardforkRegistry per fixture.
    /// </summary>
    public static class FixtureBlobScheduleOverride
    {
        public static HardforkRegistry BuildRegistry(
            HardforkRegistry baseRegistry,
            Dictionary<string, BlockchainTestLoader.BlobScheduleEntry>? schedule)
        {
            if (schedule == null || schedule.Count == 0) return baseRegistry;

            var registry = new HardforkRegistry();
            foreach (var fork in baseRegistry.RegisteredNames)
                registry.Register(fork, Apply(fork, baseRegistry.Get(fork), schedule));
            return registry;
        }

        private static HardforkConfig Apply(
            HardforkName fork,
            HardforkConfig baseConfig,
            Dictionary<string, BlockchainTestLoader.BlobScheduleEntry> schedule)
        {
            var key = ScheduleKeyFor(fork);
            if (key == null || !schedule.TryGetValue(key, out var entry) || entry == null) return baseConfig;
            if (baseConfig.IntrinsicGasRules?.Blob == null) return baseConfig;

            HardforkConfig clone = null;

            if (TryParseInt(entry.Target, out var target))
            {
                clone ??= baseConfig.Clone();
                clone.TargetBlobsPerBlock = target;
            }

            if (TryParseInt(entry.Max, out var max))
            {
                clone ??= baseConfig.Clone();
                clone.MaxBlobsPerBlock = max;
            }

            if (TryParseInt(entry.BaseFeeUpdateFraction, out var fraction))
            {
                IBlobGasRule rule =
                    fork >= HardforkName.Osaka ? new Eip7892BlobGasRule(fraction) :
                    fork >= HardforkName.Prague ? new Eip7691BlobGasRule(fraction) :
                    fork >= HardforkName.Cancun ? new Eip4844BlobGasRule(fraction) :
                    null;

                if (rule != null)
                {
                    clone ??= baseConfig.Clone();
                    clone.IntrinsicGasRules = baseConfig.IntrinsicGasRules.WithBlob(rule);
                }
            }

            return clone ?? baseConfig;
        }

        private static string? ScheduleKeyFor(HardforkName fork) => fork switch
        {
            HardforkName.OsakaBpo2 => "BPO2",
            HardforkName.OsakaBpo1 => "BPO1",
            HardforkName.Osaka => "Osaka",
            HardforkName.Prague => "Prague",
            HardforkName.Cancun => "Cancun",
            _ => null,
        };

        private static bool TryParseInt(string? hex, out int value)
        {
            value = 0;
            if (string.IsNullOrEmpty(hex)) return false;

            var big = hex.HexToBigInteger(false);
            if (big < 0 || big > int.MaxValue) return false;

            value = (int)big;
            return true;
        }
    }
}
