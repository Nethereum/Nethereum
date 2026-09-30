using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;

namespace Nethereum.DevP2P.Sync.FullSync.Stages
{
    public sealed class BlockExecutionStage
    {
        private readonly BlockImporter _importer;
        private readonly IChainMetadataStore _metadataStore;

        public BlockExecutionStage(
            BlockImporter importer,
            IChainMetadataStore metadataStore)
        {
            _importer = importer ?? throw new ArgumentNullException(nameof(importer));
            _metadataStore = metadataStore ?? throw new ArgumentNullException(nameof(metadataStore));
        }

        public async Task<BlockExecutionOutcome> ExecuteOneAsync(
            BlockHeader header,
            IList<ISignedTransaction> transactions,
            IList<BlockHeader> uncles,
            IList<Withdrawal> withdrawals,
            byte[] blockHash,
            CancellationToken ct)
        {
            if (header == null) throw new ArgumentNullException(nameof(header));
            if (blockHash == null || blockHash.Length != 32)
                throw new ArgumentException("blockHash must be exactly 32 bytes (Keccak-256 digest).", nameof(blockHash));

            var result = await _importer.ImportAsync(
                header,
                transactions ?? new List<ISignedTransaction>(),
                uncles ?? new List<BlockHeader>(),
                withdrawals,
                ct).ConfigureAwait(false);

            if (result.RootMatches)
            {
                _metadataStore.Commit((ulong)header.BlockNumber, blockHash);
                return new BlockExecutionOutcome(
                    Result: result,
                    BlockNumber: (ulong)header.BlockNumber,
                    BlockHash: blockHash,
                    Committed: true);
            }

            return new BlockExecutionOutcome(
                Result: result,
                BlockNumber: (ulong)header.BlockNumber,
                BlockHash: blockHash,
                Committed: false);
        }
    }

    public sealed record BlockExecutionOutcome(
        BlockImporterResult Result,
        ulong BlockNumber,
        byte[] BlockHash,
        bool Committed);
}
