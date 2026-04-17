using System;
using System.Collections.Generic;
using System.Numerics;

namespace Nethereum.AppChain.Sync
{
    public class BatchInfo
    {
        public BigInteger ChainId { get; set; }
        public BigInteger FromBlock { get; set; }
        public BigInteger ToBlock { get; set; }
        public int BlockCount => (int)(ToBlock - FromBlock + 1);

        public byte[] BatchHash { get; set; } = Array.Empty<byte>();
        public byte[] ToBlockHash { get; set; } = Array.Empty<byte>();
        public byte[] ToBlockStateRoot { get; set; } = Array.Empty<byte>();
        public byte[] ToBlockTxRoot { get; set; } = Array.Empty<byte>();
        public byte[] ToBlockReceiptRoot { get; set; } = Array.Empty<byte>();

        public byte[]? PrevBatchHash { get; set; }
        public byte[]? FromBlockStateRoot { get; set; }
        public int PolicyVersion { get; set; }

        public BatchContentType ContentType { get; set; } = BatchContentType.FullBlocks;
        public List<byte[]>? DiffHashes { get; set; }
        public long TotalDiffBytes { get; set; }

        public string? Uri { get; set; }
        public long CreatedAt { get; set; }
        public long? AnchoredAt { get; set; }
        public BatchStatus Status { get; set; } = BatchStatus.Created;

        public string BatchId => $"{FromBlock}-{ToBlock}";

        public static BatchInfo Create(BigInteger chainId, BigInteger fromBlock, BigInteger toBlock)
        {
            return new BatchInfo
            {
                ChainId = chainId,
                FromBlock = fromBlock,
                ToBlock = toBlock,
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
        }
    }

    public enum BatchStatus
    {
        Pending,
        Created,
        Written,
        AnchorPending,
        Anchored,
        Verified,
        Imported,
        Failed
    }

    public enum BatchContentType
    {
        FullBlocks,
        StateDiffs
    }
}
