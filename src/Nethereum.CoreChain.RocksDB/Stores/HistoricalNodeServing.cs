using System;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Services;
using Nethereum.CoreChain.Storage;
using Nethereum.Merkle.Patricia;
using Nethereum.Util;
using Nethereum.Merkle.Patricia.Storage;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public sealed class HistoricalNodeServing : IHistoricalProofCapable, Nethereum.DevP2P.Sync.Abstractions.ISnapNodeStoreSelector
    {
        public RocksDbNodeReverseDiffStore Journal { get; }

        public RocksDbPathTrieNodeStore Latest { get; }

        public INodeHistoryFloorPolicy Floor { get; }

        public bool IndexOn { get; }

        private readonly IStateStore _state;
        private readonly IBlockStore _blocks;
        private readonly IChainMetadataStore _metadata;

        public HistoricalNodeServing(
            RocksDbNodeReverseDiffStore journal,
            RocksDbPathTrieNodeStore latest,
            INodeHistoryFloorPolicy floor,
            bool indexOn,
            IStateStore state,
            IBlockStore blocks,
            IChainMetadataStore metadata)
        {
            Journal = journal ?? throw new ArgumentNullException(nameof(journal));
            Latest = latest ?? throw new ArgumentNullException(nameof(latest));
            Floor = floor ?? throw new ArgumentNullException(nameof(floor));
            IndexOn = indexOn;
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _blocks = blocks ?? throw new ArgumentNullException(nameof(blocks));
            _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        }

        private async Task<ulong> ResolveServeHeadAsync()
        {
            var stateTip = _metadata.GetLastBlock();
            if (stateTip > 0) return stateTip;
            var latest = await _blocks.GetLatestAsync();
            if (latest != null) return (ulong)latest.BlockNumber;
            var height = await _blocks.GetHeightAsync();
            return height < 0 ? 0UL : (ulong)height;
        }

        public bool CanServeProofAsOf(ulong blockNumber, ulong head)
            => blockNumber <= head && blockNumber >= Floor.FloorFor(head);

        public IProofService ProofServiceAsOf(ulong blockNumber)
            => new ProofService(_state, new AsOfBlockNodeStore(Journal, Latest, blockNumber));

        public async Task<ITrieNodeStore> ResolveForRootAsync(byte[] stateRoot, CancellationToken ct = default)
        {
            if (stateRoot == null || stateRoot.Length != 32) return null;

            ulong head = await ResolveServeHeadAsync();
            var headHeader = await _blocks.GetByNumberAsync(head);
            if (headHeader != null && ByteUtil.AreEqual(headHeader.StateRoot, stateRoot))
                return null;

            var n = Journal.FindBlockByStateRoot(stateRoot);
            if (n == null) return null;

            if (n.Value < Floor.FloorFor(head) || n.Value > head) return null;

            return new AsOfBlockNodeStore(Journal, Latest, n.Value);
        }
    }
}
