using System;
using System.Collections.Generic;
using Nethereum.EVM;
using Nethereum.Model;
using Nethereum.Model.Codecs;

namespace Nethereum.CoreChain.Forks
{
    public static class GenesisHeaderFields
    {
        public static void Apply(BlockHeader header, HardforkName fork)
        {
            if (header == null) throw new ArgumentNullException(nameof(header));

            var codec = BlockHeaderCodecs.ForFork(fork);

            ApplyFieldsTheCodecCarries(header, codec);
            codec.ClearFieldsNotCarried(header);
        }

        public static void EnsureShapeMatchesPinnedFork(BlockHeader storedGenesis, HardforkName pinnedFork)
        {
            if (storedGenesis == null) throw new ArgumentNullException(nameof(storedGenesis));

            var pinned = BlockHeaderCodecs.ForFork(pinnedFork);
            var stored = BlockHeaderCodecSelector.ForHeader(storedGenesis);
            if (ReferenceEquals(stored, pinned)) return;

            throw new InvalidOperationException(
                $"Stored genesis is {stored.ShapeName}-shaped, but this chain is pinned to {pinnedFork} " +
                $"({pinned.ShapeName}-shaped). Genesis is block 0 of the pinned fork, so a store built " +
                "under a different fork has a different genesis hash and is a different chain. " +
                $"Start a new data directory for {pinnedFork}, and keep this store for the chain it " +
                "already holds. Repinning the config instead changes how every block EXECUTES, not " +
                "only how its header encodes, so an existing history would be re-run under different " +
                "rules. A London shape in particular names no fork at all: chains pinned to every " +
                "fork have written one, so it cannot tell you which fork to pin back to.");
        }

        private static void ApplyFieldsTheCodecCarries(BlockHeader header, IBlockHeaderCodec codec)
        {
            if (codec.CarriesWithdrawalsRoot) ApplyNoWithdrawals(header);
            if (codec.CarriesBlobFieldsAndBeaconRoot) DefaultBlobFieldsAndBeaconRootWhenUnset(header);
            if (codec.CarriesRequestsHash) ApplyNoRequests(header);
            if (codec.CarriesBlockAccessList) ApplyEmptyBlockAccessListAndDefaultSlotWhenUnset(header);
        }

        private static void ApplyNoWithdrawals(BlockHeader header)
            => header.WithdrawalsRoot = DefaultValues.EMPTY_TRIE_HASH;

        private static void DefaultBlobFieldsAndBeaconRootWhenUnset(BlockHeader header)
        {
            header.BlobGasUsed ??= 0;
            header.ExcessBlobGas ??= 0;
            header.ParentBeaconBlockRoot ??= new byte[32];
        }

        private static void ApplyNoRequests(BlockHeader header)
            => header.RequestsHash = Eip7685Constants.EmptyRequestsHash;

        private static void ApplyEmptyBlockAccessListAndDefaultSlotWhenUnset(BlockHeader header)
        {
            header.BlockAccessListHash = BlockAccessListRLPEncoder.Current.Hash(new List<AccountChanges>());
            header.SlotNumber ??= 0;
        }
    }
}
