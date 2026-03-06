using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Consensus;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.AppChain.P2P.BlockHandling
{
    public class P2PBlockHandler : IP2PBlockHandler
    {
        private readonly IBlockStore _blockStore;
        private readonly IConsensusEngine? _consensusEngine;
        private readonly ILogger<P2PBlockHandler>? _logger;
        private readonly SemaphoreSlim _importLock = new(1, 1);
        private readonly Sha3Keccack _sha3 = new();

        public event EventHandler<BlockImportedEventArgs>? BlockImported;
        public event EventHandler<BlockRejectedEventArgs>? BlockRejected;

        public P2PBlockHandler(
            IBlockStore blockStore,
            IConsensusEngine? consensusEngine = null,
            ILogger<P2PBlockHandler>? logger = null)
        {
            _blockStore = blockStore ?? throw new ArgumentNullException(nameof(blockStore));
            _consensusEngine = consensusEngine;
            _logger = logger;
        }

        public async Task<BlockImportResult> HandleNewBlockMessageAsync(byte[] messagePayload, string fromPeerId)
        {
            try
            {
                using var ms = new System.IO.MemoryStream(messagePayload);
                using var reader = new System.IO.BinaryReader(ms);

                var headerLength = reader.ReadInt32();
                var headerBytes = reader.ReadBytes(headerLength);

                _logger?.LogDebug("[P2P] Decoding header of {Length} bytes", headerBytes.Length);

                var header = BlockHeaderEncoder.Current.Decode(headerBytes);
                var blockHash = _sha3.CalculateHash(headerBytes);

                _logger?.LogDebug("[P2P] Decoded block {Number}, ParentHash: {ParentHash}",
                    header.BlockNumber,
                    header.ParentHash?.ToHex(true) ?? "null");

                return await HandleNewBlockAsync(header, blockHash, fromPeerId);
            }
            catch (Exception ex)
            {
                var error = $"Failed to decode block message: {ex.Message}";
                _logger?.LogWarning(error);
                BlockRejected?.Invoke(this, new BlockRejectedEventArgs(null, error, BlockImportReason.DecodingFailed, fromPeerId));
                return BlockImportResult.Rejected(error, BlockImportReason.DecodingFailed);
            }
        }

        public async Task<BlockImportResult> HandleNewBlockAsync(byte[] encodedBlockHeader, string fromPeerId)
        {
            BlockHeader header;
            try
            {
                header = BlockHeaderEncoder.Current.Decode(encodedBlockHeader);
            }
            catch (Exception ex)
            {
                var error = $"Failed to decode block header: {ex.Message}";
                _logger?.LogWarning(error);
                BlockRejected?.Invoke(this, new BlockRejectedEventArgs(null, error, BlockImportReason.DecodingFailed, fromPeerId));
                return BlockImportResult.Rejected(error, BlockImportReason.DecodingFailed);
            }

            var blockHash = _sha3.CalculateHash(encodedBlockHeader);
            return await HandleNewBlockAsync(header, blockHash, fromPeerId);
        }

        public async Task<BlockImportResult> HandleNewBlockAsync(BlockHeader header, byte[] blockHash, string fromPeerId)
        {
            await _importLock.WaitAsync();
            try
            {
                return await ImportBlockInternalAsync(header, blockHash, fromPeerId);
            }
            finally
            {
                _importLock.Release();
            }
        }

        private async Task<BlockImportResult> ImportBlockInternalAsync(BlockHeader header, byte[] blockHash, string fromPeerId)
        {
            var blockNumber = header.BlockNumber;
            var blockHashHex = blockHash.ToHex(true);
            var shortHash = blockHashHex.Length > 10 ? blockHashHex.Substring(0, 10) + "..." : blockHashHex;

            _logger?.LogDebug("[P2P] Received block {BlockNumber} ({BlockHash}) from peer {PeerId}",
                blockNumber, shortHash, fromPeerId);

            if (await _blockStore.ExistsAsync(blockHash))
            {
                _logger?.LogDebug("[P2P] Block {BlockNumber} already known, skipping", blockNumber);
                return BlockImportResult.AlreadyKnown((long)blockNumber);
            }

            var latestBlock = await _blockStore.GetLatestAsync();
            if (latestBlock == null)
            {
                if (blockNumber != 0)
                {
                    var error = $"No genesis block, cannot import block {blockNumber}";
                    _logger?.LogWarning("[P2P] {Error}", error);
                    BlockRejected?.Invoke(this, new BlockRejectedEventArgs(header, error, BlockImportReason.InvalidBlockNumber, fromPeerId));
                    return BlockImportResult.Rejected(error, BlockImportReason.InvalidBlockNumber);
                }
            }
            else
            {
                var expectedBlockNumber = latestBlock.BlockNumber + 1;
                if (blockNumber != expectedBlockNumber)
                {
                    var error = $"Invalid block number: expected {expectedBlockNumber}, got {blockNumber}";
                    _logger?.LogWarning("[P2P] {Error}", error);
                    BlockRejected?.Invoke(this, new BlockRejectedEventArgs(header, error, BlockImportReason.InvalidBlockNumber, fromPeerId));
                    return BlockImportResult.Rejected(error, BlockImportReason.InvalidBlockNumber);
                }

                var latestBlockHash = await _blockStore.GetHashByNumberAsync(latestBlock.BlockNumber);
                if (header.ParentHash == null || !header.ParentHash.SequenceEqual(latestBlockHash))
                {
                    var error = $"Invalid parent hash for block {blockNumber}: expected {latestBlockHash.ToHex(true)}, got {header.ParentHash?.ToHex(true) ?? "null"}";
                    _logger?.LogWarning("[P2P] {Error}", error);
                    BlockRejected?.Invoke(this, new BlockRejectedEventArgs(header, error, BlockImportReason.InvalidParentHash, fromPeerId));
                    return BlockImportResult.Rejected(error, BlockImportReason.InvalidParentHash);
                }
            }

            string? signer = null;
            if (_consensusEngine != null)
            {
                signer = _consensusEngine.RecoverSigner(header);
                if (string.IsNullOrEmpty(signer))
                {
                    var error = $"Failed to recover signer for block {blockNumber}";
                    _logger?.LogWarning("[P2P] {Error}", error);
                    BlockRejected?.Invoke(this, new BlockRejectedEventArgs(header, error, BlockImportReason.ConsensusRejected, fromPeerId));
                    return BlockImportResult.Rejected(error, BlockImportReason.ConsensusRejected);
                }

                var isValid = _consensusEngine.ValidateBlock(header, latestBlock);
                if (!isValid)
                {
                    var error = $"Consensus validation failed for block {blockNumber}";
                    _logger?.LogWarning("[P2P] {Error}", error);
                    BlockRejected?.Invoke(this, new BlockRejectedEventArgs(header, error, BlockImportReason.ConsensusRejected, fromPeerId));
                    return BlockImportResult.Rejected(error, BlockImportReason.ConsensusRejected);
                }

                _logger?.LogDebug("[P2P] Block {BlockNumber} validated, signer: {Signer}",
                    blockNumber, signer);
            }

            try
            {
                await _blockStore.SaveAsync(header, blockHash);

                if (_consensusEngine != null && !string.IsNullOrEmpty(signer))
                {
                    _consensusEngine.ApplyBlock(header, signer, blockHash);
                }

                _logger?.LogInformation("[P2P] Imported block {BlockNumber} ({BlockHash}) from peer {PeerId}",
                    blockNumber, shortHash, fromPeerId);

                BlockImported?.Invoke(this, new BlockImportedEventArgs(header, blockHash, fromPeerId));
                return BlockImportResult.Imported(header, blockHash);
            }
            catch (Exception ex)
            {
                var error = $"Failed to save block {blockNumber}: {ex.Message}";
                _logger?.LogError(ex, "[P2P] {Error}", error);
                BlockRejected?.Invoke(this, new BlockRejectedEventArgs(header, error, BlockImportReason.StorageFailed, fromPeerId));
                return BlockImportResult.Rejected(error, BlockImportReason.StorageFailed);
            }
        }
    }
}
