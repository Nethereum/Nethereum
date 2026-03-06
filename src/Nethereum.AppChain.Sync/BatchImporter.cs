using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.AppChain.Sync
{
    public class BatchImporter : IBatchImporter
    {
        private readonly IBlockStore _blockStore;
        private readonly ITransactionStore _transactionStore;
        private readonly IReceiptStore _receiptStore;
        private readonly ILogStore _logStore;
        private readonly IBatchStore _batchStore;
        private readonly IBatchReader _batchReader;
        private readonly Sha3Keccack _keccak = new Sha3Keccack();
        private readonly RootCalculator _rootCalculator = new RootCalculator();

        public BatchImporter(
            IBlockStore blockStore,
            ITransactionStore transactionStore,
            IReceiptStore receiptStore,
            ILogStore logStore,
            IBatchStore batchStore,
            IBatchReader batchReader = null)
        {
            _blockStore = blockStore;
            _transactionStore = transactionStore;
            _receiptStore = receiptStore;
            _logStore = logStore;
            _batchStore = batchStore;
            _batchReader = batchReader ?? new BatchFileReader();
        }

        public async Task<BatchImportResult> ImportBatchAsync(
            Stream batchStream,
            byte[] expectedBatchHash = null,
            BatchVerificationMode verificationMode = BatchVerificationMode.Quick,
            CancellationToken cancellationToken = default)
        {
            var result = new BatchImportResult();
            var verificationResult = new BatchVerificationResult
            {
                HeaderChainValid = true,
                TxRootsValid = true,
                ReceiptRootsValid = true,
                StateRootValid = true
            };

            try
            {
                var header = await _batchReader.ReadHeaderAsync(batchStream, cancellationToken);
                batchStream.Position = 0;

                byte[] previousBlockHash = null;
                var blocksImported = 0;
                var transactionsImported = 0;
                var logsImported = 0;

                await foreach (var block in _batchReader.ReadBlocksAsync(batchStream, cancellationToken))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var blockHash = CalculateBlockHash(block.Header);

                    if (verificationMode >= BatchVerificationMode.Quick)
                    {
                        if (previousBlockHash != null && !ByteUtil.AreEqual(block.Header.ParentHash, previousBlockHash))
                        {
                            verificationResult.HeaderChainValid = false;
                            verificationResult.FailureReason = $"Invalid parent hash at block {block.Header.BlockNumber}";
                            result.VerificationResult = verificationResult;
                            result.ErrorMessage = verificationResult.FailureReason;
                            return result;
                        }
                        previousBlockHash = blockHash;
                    }

                    var transactions = new List<ISignedTransaction>();
                    foreach (var txBytes in block.TransactionBytes)
                    {
                        var tx = TransactionFactory.CreateTransaction(txBytes);
                        transactions.Add(tx);
                    }

                    if (verificationMode >= BatchVerificationMode.Quick)
                    {
                        var computedTxRoot = ComputeTransactionsRoot(transactions);
                        if (!ByteUtil.AreEqual(computedTxRoot, block.Header.TransactionsHash))
                        {
                            verificationResult.TxRootsValid = false;
                            verificationResult.FailureReason = $"Invalid transactions root at block {block.Header.BlockNumber}";
                            result.VerificationResult = verificationResult;
                            result.ErrorMessage = verificationResult.FailureReason;
                            return result;
                        }
                    }

                    await _blockStore.SaveAsync(block.Header, blockHash);

                    for (int i = 0; i < transactions.Count; i++)
                    {
                        var tx = transactions[i];
                        await _transactionStore.SaveAsync(tx, blockHash, i, block.Header.BlockNumber);
                        transactionsImported++;

                        if (i < block.Receipts.Count)
                        {
                            var receipt = block.Receipts[i];
                            var txHash = tx.Hash;
                            var gasUsed = receipt.CumulativeGasUsed;
                            if (i > 0)
                            {
                                gasUsed -= block.Receipts[i - 1].CumulativeGasUsed;
                            }

                            await _receiptStore.SaveAsync(
                                receipt,
                                txHash,
                                blockHash,
                                block.Header.BlockNumber,
                                i,
                                gasUsed,
                                null,
                                0);

                            if (receipt.Logs != null && receipt.Logs.Count > 0)
                            {
                                await _logStore.SaveLogsAsync(receipt.Logs, txHash, blockHash, block.Header.BlockNumber, i);
                                logsImported += receipt.Logs.Count;
                            }
                        }
                    }

                    blocksImported++;
                }

                batchStream.Position = 0;
                var batchInfo = await _batchReader.ReadAndVerifyAsync(batchStream, expectedBatchHash, cancellationToken);
                batchInfo.Status = BatchStatus.Imported;
                await _batchStore.SaveBatchAsync(batchInfo);

                result.Success = true;
                result.BatchInfo = batchInfo;
                result.BlocksImported = blocksImported;
                result.TransactionsImported = transactionsImported;
                result.LogsImported = logsImported;
                result.VerificationResult = verificationResult;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        public async Task<BatchImportResult> ImportBatchFromFileAsync(
            string filePath,
            byte[] expectedBatchHash = null,
            BatchVerificationMode verificationMode = BatchVerificationMode.Quick,
            bool compressed = true,
            CancellationToken cancellationToken = default)
        {
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536);

            if (compressed)
            {
                using var gzipStream = new System.IO.Compression.GZipStream(fileStream, System.IO.Compression.CompressionMode.Decompress);
                using var memoryStream = new MemoryStream();
                await gzipStream.CopyToAsync(memoryStream, cancellationToken);
                memoryStream.Position = 0;
                return await ImportBatchAsync(memoryStream, expectedBatchHash, verificationMode, cancellationToken);
            }
            else
            {
                return await ImportBatchAsync(fileStream, expectedBatchHash, verificationMode, cancellationToken);
            }
        }

        private byte[] CalculateBlockHash(BlockHeader header)
        {
            var encoded = BlockHeaderEncoder.Current.Encode(header);
            return _keccak.CalculateHash(encoded);
        }

        private byte[] ComputeTransactionsRoot(List<ISignedTransaction> transactions)
        {
            var encodedTxs = transactions.Select(tx => tx.GetRLPEncoded()).ToList();
            return _rootCalculator.CalculateTransactionsRoot(encodedTxs);
        }

    }
}
