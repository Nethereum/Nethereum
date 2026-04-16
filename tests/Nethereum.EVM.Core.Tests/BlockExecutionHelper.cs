using Nethereum.CoreChain;
using Nethereum.EVM;
using Nethereum.EVM.Execution;
using Nethereum.EVM.Precompiles;
using Nethereum.EVM.Witness;
using Nethereum.Model;

namespace Nethereum.EVM.Core.Tests
{
    public static class BlockExecutionHelper
    {
        private static readonly HardforkRegistry MainnetRegistry = DefaultMainnetHardforkRegistry.Instance;

        public static BlockExecutionResult ExecuteBlock(BlockWitnessData block, HardforkConfig config = null)
        {
            if (block.Features == null || block.Features.Fork == HardforkName.Unspecified)
            {
                block.Features = block.Features ?? new BlockFeatureConfig();
                block.Features.Fork = HardforkName.Prague;
            }
            var encoding = RlpBlockEncodingProvider.Instance;
            return BlockExecutor.Execute(
                block,
                encoding,
                MainnetRegistry,
                new PatriciaStateRootCalculator(encoding),
                block.ProduceBlockCommitments ? new PatriciaBlockRootCalculator() : null);
        }
    }
}
