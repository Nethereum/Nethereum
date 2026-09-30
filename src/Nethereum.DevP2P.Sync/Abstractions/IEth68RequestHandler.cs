using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Model;
using Nethereum.Model.P2P;

namespace Nethereum.DevP2P.Sync.Abstractions
{
    public interface IEth68RequestHandler
    {
        Task<IList<BlockHeader>> GetHeadersAsync(GetBlockHeadersMessage request, CancellationToken cancellationToken = default);
        Task<IList<BlockBody>> GetBodiesAsync(byte[][] blockHashes, CancellationToken cancellationToken = default);
        Task<List<List<Receipt>>> GetReceiptsAsync(byte[][] blockHashes, CancellationToken cancellationToken = default);

        Task<Receipts70Result> GetReceipts70Async(byte[][] blockHashes, ulong firstBlockReceiptIndex, ulong sizeCap, CancellationToken cancellationToken = default);

        Task<IList<ISignedTransaction>> GetPooledTransactionsAsync(byte[][] txHashes, CancellationToken cancellationToken = default);

        Task<List<byte[]>> GetBlockAccessListsAsync(byte[][] blockHashes, CancellationToken cancellationToken = default);
    }

    public readonly struct Receipts70Result
    {
        public Receipts70Result(List<List<Receipt>> receiptsByBlock, bool lastBlockIncomplete)
        {
            ReceiptsByBlock = receiptsByBlock;
            LastBlockIncomplete = lastBlockIncomplete;
        }

        public List<List<Receipt>> ReceiptsByBlock { get; }
        public bool LastBlockIncomplete { get; }
    }
}
