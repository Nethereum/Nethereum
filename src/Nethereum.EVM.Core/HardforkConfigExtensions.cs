using Nethereum.Documentation;
using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Nethereum.EVM.Execution.Precompiles;
using Nethereum.EVM.Gas.Intrinsic;

namespace Nethereum.EVM
{
    public static class HardforkConfigExtensions
    {
        public static HardforkConfig WithPrecompiles(
            this HardforkConfig config,
            PrecompileRegistry registry)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (registry == null) throw new ArgumentNullException(nameof(registry));

            var clone = config.Clone();
            clone.Precompiles = registry;
            return clone;
        }

        private static readonly ConditionalWeakTable<HardforkConfig, ConcurrentDictionary<BlobParameters, HardforkConfig>>
            ConfigsByBlobParameters = new ConditionalWeakTable<HardforkConfig, ConcurrentDictionary<BlobParameters, HardforkConfig>>();

        [NethereumDocExample(DocSection.EvmSimulator, "blob-schedule", "Clone a HardforkConfig with scheduled blob parameters")]
        public static HardforkConfig WithBlobParameters(this HardforkConfig config, BlobParameters? parameters)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            var blob = config.IntrinsicGasRules?.Blob;
            if (parameters is not BlobParameters stated || blob == null) return config;
            if (config.TargetBlobsPerBlock == stated.Target && config.MaxBlobsPerBlock == stated.Max
                && blob.BaseFeeUpdateFraction == new Util.EvmUInt256((ulong)stated.BaseFeeUpdateFraction))
                return config;

            return ConfigsByBlobParameters.GetOrCreateValue(config).GetOrAdd(stated, p =>
            {
                var clone = config.Clone();
                clone.TargetBlobsPerBlock = p.Target;
                clone.MaxBlobsPerBlock = p.Max;
                clone.IntrinsicGasRules = config.IntrinsicGasRules.WithBlob(
                    new ScheduledBlobGasRule(p.BaseFeeUpdateFraction, blob.AppliesReservePrice));
                return clone;
            });
        }

        [NethereumDocExample(DocSection.EvmSimulator, "blob-schedule", "Resolve a block's HardforkConfig from the chain activations")]
        public static HardforkConfig AtBlock(
            this HardforkConfig config, IChainActivations activations, long blockNumber, ulong timestamp) =>
            activations == null ? config : config.WithBlobParameters(activations.BlobParametersAt(blockNumber, timestamp));
    }
}
