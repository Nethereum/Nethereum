using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.CoreChain.Tracing;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Execution;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Model;

namespace Nethereum.CoreChain
{
    public abstract partial class ChainNodeBase
    {
        protected virtual async Task<TraceExecutionResult> PrepareAndExecuteTraceAsync(string txHash, bool traceEnabled = true)
        {
            var txHashBytes = txHash.HexToByteArray();
            var tx = await _transactionStore.GetByHashAsync(txHashBytes);
            if (tx == null)
                throw new InvalidOperationException($"Transaction {txHash} not found");

            var receiptInfo = await _receiptStore.GetInfoByTxHashAsync(txHashBytes);
            if (receiptInfo == null)
                throw new InvalidOperationException($"Receipt for transaction {txHash} not found");

            var block = await _blockStore.GetByHashAsync(receiptInfo.BlockHash);
            if (block == null)
                throw new InvalidOperationException($"Block for transaction {txHash} not found");

            var blockContext = BlockExecutor.BuildBlockContext(block, ForkStampedConfigFor(block));

            var targetBlock = block.BlockNumber > 0 ? block.BlockNumber - 1 : 0;
            var traceNodeDataService = await GetNodeDataServiceAtBlockAsync(targetBlock);
            var executionStateService = new ExecutionStateService(traceNodeDataService);

            if (receiptInfo.TransactionIndex > 0)
            {
                await ReplayPrecedingTransactionsAsync(
                    receiptInfo.BlockHash, receiptInfo.TransactionIndex, blockContext, executionStateService);
            }

            var execResult = await ExecuteBlockTxAsync(tx, blockContext, executionStateService, traceEnabled);

            return new TraceExecutionResult
            {
                Program = execResult?.Program,
                CallInput = BuildTraceCallInput(tx),
                StateService = executionStateService,
                IsContractCreation = tx.IsContractCreation(),
                IsSimpleTransfer = execResult?.ProgramResult == null,
                TotalGasUsed = execResult?.GasUsed ?? 0
            };
        }


        private async Task ReplayPrecedingTransactionsAsync(
            byte[] blockHash,
            int targetTxIndex,
            BlockContext blockContext,
            ExecutionStateService executionStateService)
        {
            var blockTxs = await _transactionStore.GetByBlockHashAsync(blockHash);
            if (blockTxs == null || blockTxs.Count == 0)
                return;

            for (int i = 0; i < targetTxIndex && i < blockTxs.Count; i++)
                await ReplayTransactionAsync(blockTxs[i], blockContext, executionStateService);
        }

        private async Task ReplayTransactionAsync(
            ISignedTransaction precedingTx,
            BlockContext blockContext,
            ExecutionStateService executionStateService)
        {
            var senderAddress = _txVerifier.GetSenderAddress(precedingTx);
            if (string.IsNullOrEmpty(senderAddress))
                return;

            if (!executionStateService.ContainsInitialChainBalanceForAddress(senderAddress))
            {
                var senderBalance = await executionStateService.StateReader.GetBalanceAsync(senderAddress);
                executionStateService.SetInitialChainBalance(senderAddress, senderBalance);
            }

            var ctx = TransactionContextFactory.From(
                precedingTx, senderAddress, blockContext, executionStateService);

            await ExecutorAt(blockContext).ExecuteAsync(ctx);
        }

        public virtual async Task<OpcodeTraceResult> TraceTransactionAsync(
            string txHash,
            OpcodeTraceConfig config = null)
        {
            var result = await PrepareAndExecuteTraceAsync(txHash);

            if (result.IsSimpleTransfer)
            {
                return new OpcodeTraceResult
                {
                    Gas = (ulong)result.TotalGasUsed,
                    Failed = false,
                    ReturnValue = "0x",
                    StructLogs = new List<OpcodeTraceStep>()
                };
            }

            return TraceConverter.ConvertToOpcodeResult(result.Program, config, result.TotalGasUsed);
        }

        public virtual async Task<CallTraceResult> TraceTransactionCallTracerAsync(string txHash)
        {
            var result = await PrepareAndExecuteTraceAsync(txHash);

            if (result.IsSimpleTransfer)
            {
                return new CallTraceResult
                {
                    Type = result.IsContractCreation ? "CREATE" : "CALL",
                    From = result.CallInput.From,
                    To = result.IsContractCreation ? null : result.CallInput.To,
                    Value = result.CallInput.Value ?? new HexBigInteger(0),
                    Gas = result.CallInput.Gas ?? new HexBigInteger(0),
                    GasUsed = new HexBigInteger(result.TotalGasUsed),
                    Input = "0x",
                    Output = "0x"
                };
            }

            return TraceConverter.ConvertToCallTraceResult(
                result.Program, result.CallInput, result.IsContractCreation, result.TotalGasUsed);
        }

        public virtual async Task<PrestateTraceResult> TraceTransactionPrestateAsync(string txHash)
        {
            var result = await PrepareAndExecuteTraceAsync(txHash, traceEnabled: false);

            if (result.IsSimpleTransfer)
            {
                return new PrestateTraceResult
                {
                    Pre = new Dictionary<string, PrestateAccountInfo>(),
                    Post = new Dictionary<string, PrestateAccountInfo>()
                };
            }

            return TraceConverter.ConvertToPrestateResult(result.StateService);
        }
    }
}
