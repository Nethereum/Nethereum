using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;

namespace Nethereum.AppChain.Sync
{
    public class BlockReExecutor : IBlockReExecutor
    {
        private readonly TransactionProcessor _transactionProcessor;
        private readonly IncrementalStateRootCalculator _stateRootCalculator;
        private readonly IStateStore _stateStore;
        private readonly ChainConfig _chainConfig;
        private readonly ILogger<BlockReExecutor>? _logger;

        public BlockReExecutor(
            TransactionProcessor transactionProcessor,
            IStateStore stateStore,
            ChainConfig chainConfig,
            ILogger<BlockReExecutor>? logger = null,
            IncrementalStateRootCalculator? stateRootCalculator = null)
        {
            _transactionProcessor = transactionProcessor ?? throw new ArgumentNullException(nameof(transactionProcessor));
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _chainConfig = chainConfig ?? throw new ArgumentNullException(nameof(chainConfig));
            _stateRootCalculator = stateRootCalculator ?? new IncrementalStateRootCalculator(stateStore);
            _logger = logger;
        }

        public async Task<BlockReExecutionResult> ReExecuteBlockAsync(
            BlockHeader header,
            IList<ISignedTransaction> transactions,
            CancellationToken cancellationToken = default)
        {
            var result = new BlockReExecutionResult
            {
                ExpectedStateRoot = header.StateRoot
            };

            try
            {
                var blockContext = BuildBlockContext(header);
                BigInteger cumulativeGasUsed = 0;

                _logger?.LogDebug("Re-executing block {BlockNumber} with {TxCount} transactions",
                    header.BlockNumber, transactions.Count);

                for (int i = 0; i < transactions.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var tx = transactions[i];
                    var txResult = await _transactionProcessor.ExecuteTransactionAsync(
                        tx,
                        blockContext,
                        i,
                        (long)cumulativeGasUsed);

                    cumulativeGasUsed = txResult.CumulativeGasUsed;

                    result.TransactionResults.Add(new TransactionReExecutionResult
                    {
                        TransactionHash = tx.Hash,
                        Success = txResult.Success,
                        GasUsed = (long)txResult.GasUsed,
                        Error = txResult.RevertReason
                    });

                    if (!txResult.Success)
                    {
                        _logger?.LogWarning("Transaction {TxHash} reverted: {Reason}",
                            tx.Hash?.ToHex(true), txResult.RevertReason);
                    }
                }

                result.TransactionsExecuted = transactions.Count;

                // Compute state root after execution (uses dirty tracking for performance)
                result.ComputedStateRoot = await _stateRootCalculator.ComputeStateRootAsync();

                // Validate state root matches expected
                result.StateRootMatches = ByteArrayEquals(result.ComputedStateRoot, result.ExpectedStateRoot);

                if (!result.StateRootMatches)
                {
                    _logger?.LogError(
                        "State root mismatch for block {BlockNumber}! Expected: {Expected}, Computed: {Computed}",
                        header.BlockNumber,
                        result.ExpectedStateRoot?.ToHex(true) ?? "null",
                        result.ComputedStateRoot?.ToHex(true) ?? "null");

                    result.Success = false;
                    result.ErrorMessage = $"State root mismatch: expected {result.ExpectedStateRoot?.ToHex(true)}, got {result.ComputedStateRoot?.ToHex(true)}";
                }
                else
                {
                    _logger?.LogDebug("Block {BlockNumber} re-executed successfully. State root validated.",
                        header.BlockNumber);
                    result.Success = true;
                }
            }
            catch (OperationCanceledException)
            {
                result.Success = false;
                result.ErrorMessage = "Re-execution cancelled";
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to re-execute block {BlockNumber}", header.BlockNumber);
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        private BlockContext BuildBlockContext(BlockHeader header)
        {
            return new BlockContext
            {
                BlockNumber = header.BlockNumber,
                Timestamp = (long)header.Timestamp,
                Coinbase = header.Coinbase ?? _chainConfig.Coinbase,
                GasLimit = header.GasLimit,
                BaseFee = header.BaseFee ?? _chainConfig.BaseFee,
                Difficulty = header.Difficulty,
                PrevRandao = header.MixHash,
                ChainId = _chainConfig.ChainId
            };
        }

        private static bool ByteArrayEquals(byte[]? a, byte[]? b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }
    }
}
