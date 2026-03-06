using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Model;

namespace Nethereum.AppChain.Sync
{
    public interface IBatchReader
    {
        Task<BatchHeader> ReadHeaderAsync(Stream inputStream, CancellationToken cancellationToken = default);

        IAsyncEnumerable<BatchBlock> ReadBlocksAsync(Stream inputStream, CancellationToken cancellationToken = default);

        Task<BatchInfo> ReadAndVerifyAsync(
            Stream inputStream,
            byte[] expectedBatchHash,
            CancellationToken cancellationToken = default);

        Task<BatchInfo> ReadFromFileAsync(
            string filePath,
            byte[] expectedBatchHash = null,
            bool compressed = true,
            CancellationToken cancellationToken = default);
    }

    public class BatchHeader
    {
        public ushort Version { get; set; }
        public ulong ChainId { get; set; }
        public ulong FromBlock { get; set; }
        public ulong ToBlock { get; set; }
        public ushort BlockCount { get; set; }
    }

    public class BatchBlock
    {
        public BlockHeader Header { get; set; } = null!;
        public List<byte[]> TransactionBytes { get; set; } = new();
        public List<Receipt> Receipts { get; set; } = new();
    }
}
