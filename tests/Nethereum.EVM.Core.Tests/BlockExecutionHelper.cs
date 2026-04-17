using Nethereum.CoreChain;
using Nethereum.EVM;
using Nethereum.EVM.Execution;
using Nethereum.EVM.Precompiles;
using Nethereum.EVM.Witness;
using Nethereum.Merkle.Binary.Hashing;
using Nethereum.Model;
using Nethereum.Util.HashProviders;

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
            var stateRootCalc = ResolveStateRootCalculator(block.Features, encoding);

            return BlockExecutor.Execute(
                block,
                encoding,
                MainnetRegistry,
                stateRootCalc,
                block.ProduceBlockCommitments ? new PatriciaBlockRootCalculator() : null);
        }

        private static IStateRootCalculator ResolveStateRootCalculator(
            BlockFeatureConfig features, IBlockEncodingProvider encoding)
        {
            if (features.StateTree == WitnessStateTreeType.Binary)
            {
                var hashProvider = features.HashFunction switch
                {
                    WitnessHashFunction.Blake3 => (IHashProvider)new Blake3HashProvider(),
                    WitnessHashFunction.Poseidon => new PoseidonPairHashProvider(),
                    WitnessHashFunction.Sha256 => new Sha256HashProvider(),
                    _ => new Blake3HashProvider()
                };
                return new BinaryStateRootCalculator(hashProvider);
            }

            return new PatriciaStateRootCalculator(encoding);
        }
    }
}
