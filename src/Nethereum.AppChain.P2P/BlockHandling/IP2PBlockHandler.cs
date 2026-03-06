using System;
using System.Threading.Tasks;
using Nethereum.Model;

namespace Nethereum.AppChain.P2P.BlockHandling
{
    public interface IP2PBlockHandler
    {
        event EventHandler<BlockImportedEventArgs>? BlockImported;
        event EventHandler<BlockRejectedEventArgs>? BlockRejected;

        Task<BlockImportResult> HandleNewBlockMessageAsync(byte[] messagePayload, string fromPeerId);
        Task<BlockImportResult> HandleNewBlockAsync(byte[] encodedBlockHeader, string fromPeerId);
        Task<BlockImportResult> HandleNewBlockAsync(BlockHeader header, byte[] blockHash, string fromPeerId);
    }

    public class BlockImportResult
    {
        public bool Success { get; }
        public string? Error { get; }
        public BlockHeader? Header { get; }
        public byte[]? BlockHash { get; }
        public BlockImportReason Reason { get; }

        private BlockImportResult(bool success, string? error, BlockHeader? header, byte[]? blockHash, BlockImportReason reason)
        {
            Success = success;
            Error = error;
            Header = header;
            BlockHash = blockHash;
            Reason = reason;
        }

        public static BlockImportResult Imported(BlockHeader header, byte[] blockHash) =>
            new(true, null, header, blockHash, BlockImportReason.Imported);

        public static BlockImportResult AlreadyKnown(long blockNumber) =>
            new(true, null, null, null, BlockImportReason.AlreadyKnown);

        public static BlockImportResult Rejected(string error, BlockImportReason reason) =>
            new(false, error, null, null, reason);
    }

    public enum BlockImportReason
    {
        Imported,
        AlreadyKnown,
        InvalidParentHash,
        InvalidBlockNumber,
        ConsensusRejected,
        DecodingFailed,
        StorageFailed
    }

    public class BlockImportedEventArgs : EventArgs
    {
        public BlockHeader Header { get; }
        public byte[] BlockHash { get; }
        public string FromPeerId { get; }

        public BlockImportedEventArgs(BlockHeader header, byte[] blockHash, string fromPeerId)
        {
            Header = header;
            BlockHash = blockHash;
            FromPeerId = fromPeerId;
        }
    }

    public class BlockRejectedEventArgs : EventArgs
    {
        public BlockHeader? Header { get; }
        public string Error { get; }
        public BlockImportReason Reason { get; }
        public string FromPeerId { get; }

        public BlockRejectedEventArgs(BlockHeader? header, string error, BlockImportReason reason, string fromPeerId)
        {
            Header = header;
            Error = error;
            Reason = reason;
            FromPeerId = fromPeerId;
        }
    }
}
