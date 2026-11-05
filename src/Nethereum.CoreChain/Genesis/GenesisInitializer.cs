using System;
using System.Threading.Tasks;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage;
using Nethereum.Util;

namespace Nethereum.CoreChain.Genesis
{
    public static class GenesisInitializer
    {
        public static async Task<byte[]> EnsureAsync(
            IChainStoreBundle bundle, StandardGenesisDocument document, ChainConfig chain)
        {
            if (bundle == null) throw new ArgumentNullException(nameof(bundle));
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            var fork = chain.ResolveHardforkAt(0, (ulong)chain.GenesisTimestamp);

            if (bundle.Metadata.IsGenesisLoaded())
                return await VerifyStoredGenesisAsync(bundle, chain, fork).ConfigureAwait(false);

            await StandardGenesisLoader.PopulateAllocAsync(bundle.State, document.Alloc).ConfigureAwait(false);
            if (!StandardGenesisLoader.DeclaresAnySystemContract(document))
                await SystemContractPredeploys.ApplyGenesisAllocationAsync(bundle.State, fork).ConfigureAwait(false);

            byte[] stateRoot;
            using (var calculator = new IncrementalStateRootCalculator(
                bundle.State, bundle.StateTrieNodes,
                emitTombstones: !ReferenceEquals(bundle.StateTrieNodes, bundle.TrieNodes)))
            {
                stateRoot = await calculator.ComputeStateRootAsync().ConfigureAwait(false);
            }
            bundle.StateTrieNodes.Flush();

            var header = GenesisHeaderBuilder.Build(chain, fork, stateRoot);
            var hash = BlockHashCalculator.ForFork(header, fork);
            await bundle.Blocks.SaveAsync(header, hash).ConfigureAwait(false);
            bundle.Metadata.Commit(0, hash);
            bundle.Metadata.CommitDurableState(0, hash);
            bundle.Metadata.MarkGenesisLoaded();
            return hash;
        }

        private static async Task<byte[]> VerifyStoredGenesisAsync(
            IChainStoreBundle bundle, ChainConfig chain, Nethereum.EVM.HardforkName fork)
        {
            var stored = await bundle.Blocks.GetByNumberAsync(0).ConfigureAwait(false);
            var storedHash = await bundle.Blocks.GetHashByNumberAsync(System.Numerics.BigInteger.Zero).ConfigureAwait(false);
            if (stored == null || storedHash == null)
                throw new InvalidOperationException("The store is marked as holding a genesis but block 0 is missing.");

            var expected = BlockHashCalculator.ForFork(GenesisHeaderBuilder.Build(chain, fork, stored.StateRoot), fork);
            if (!ByteUtil.AreEqual(expected, storedHash))
                throw new InvalidOperationException(
                    "The data directory holds a different genesis from the one configured; start with an empty data directory.");
            return storedHash;
        }
    }
}
