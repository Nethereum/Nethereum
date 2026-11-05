using System;
using Nethereum.EVM;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.CoreChain.Freezer
{
    public interface IBlobBaseFeeFractionResolver
    {
        EvmUInt256 FractionForBlock(BlockHeader header);
    }

    public sealed class CancunBlobBaseFeeFractionResolver : IBlobBaseFeeFractionResolver
    {
        public EvmUInt256 FractionForBlock(BlockHeader header) => BlobGasCalculator.BLOB_BASE_FEE_UPDATE_FRACTION;
    }

    public sealed class ChainBlobBaseFeeFractionResolver : IBlobBaseFeeFractionResolver
    {
        public static readonly ChainBlobBaseFeeFractionResolver Mainnet = new ChainBlobBaseFeeFractionResolver(
            MainnetChainActivations.Instance, Nethereum.EVM.Precompiles.DefaultMainnetHardforkRegistry.Instance.Get);

        private readonly IChainActivations _activations;
        private readonly Func<HardforkName, HardforkConfig> _configForFork;

        public ChainBlobBaseFeeFractionResolver(IChainActivations activations, Func<HardforkName, HardforkConfig> configForFork)
        {
            _activations = activations ?? throw new ArgumentNullException(nameof(activations));
            _configForFork = configForFork ?? throw new ArgumentNullException(nameof(configForFork));
        }

        public EvmUInt256 FractionForBlock(BlockHeader header)
        {
            var blockNumber = (long)header.BlockNumber;
            var timestamp = (ulong)header.Timestamp;
            var blob = _configForFork(_activations.ResolveAt(blockNumber, timestamp))
                .AtBlock(_activations, blockNumber, timestamp)
                .IntrinsicGasRules?.Blob;

            return blob?.BaseFeeUpdateFraction ?? BlobGasCalculator.BLOB_BASE_FEE_UPDATE_FRACTION;
        }
    }
}
