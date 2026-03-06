using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.AppChain.Sync
{
    public interface IBatchImporter
    {
        Task<BatchImportResult> ImportBatchAsync(
            Stream batchStream,
            byte[]? expectedBatchHash = null,
            BatchVerificationMode verificationMode = BatchVerificationMode.Quick,
            CancellationToken cancellationToken = default);

        Task<BatchImportResult> ImportBatchFromFileAsync(
            string filePath,
            byte[]? expectedBatchHash = null,
            BatchVerificationMode verificationMode = BatchVerificationMode.Quick,
            bool compressed = true,
            CancellationToken cancellationToken = default);
    }

    public enum BatchVerificationMode
    {
        None,
        Quick,
        Full
    }

    public class BatchImportResult
    {
        public bool Success { get; set; }
        public BatchInfo? BatchInfo { get; set; }
        public int BlocksImported { get; set; }
        public int TransactionsImported { get; set; }
        public int LogsImported { get; set; }
        public string? ErrorMessage { get; set; }
        public BatchVerificationResult? VerificationResult { get; set; }
    }

    public class BatchVerificationResult
    {
        public bool HeaderChainValid { get; set; }
        public bool TxRootsValid { get; set; }
        public bool ReceiptRootsValid { get; set; }
        public bool StateRootValid { get; set; }
        public string? FailureReason { get; set; }
    }
}
