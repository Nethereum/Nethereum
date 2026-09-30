using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Tracing;
using Nethereum.EVM.BlockchainState;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Model;
using Nethereum.RPC.DebugNode.Dtos.Tracing;

namespace Nethereum.CoreChain
{
    public abstract partial class ChainNodeBase
    {
        public virtual async Task<List<BlockResponseItemDto<OpcodeTraceResult>>> TraceBlockByNumberAsync(
            BigInteger blockNumber, OpcodeTraceConfig config = null)
        {
            var block = await _blockStore.GetByNumberAsync(blockNumber);
            if (block == null)
                throw new InvalidOperationException($"Block {blockNumber} not found");
            var blockHash = await _blockStore.GetHashByNumberAsync(blockNumber);
            return await TraceBlockOpcodeAsync(block, blockHash, config);
        }

        public virtual async Task<List<BlockResponseItemDto<OpcodeTraceResult>>> TraceBlockByHashAsync(
            byte[] blockHash, OpcodeTraceConfig config = null)
        {
            var block = await _blockStore.GetByHashAsync(blockHash);
            if (block == null)
                throw new InvalidOperationException($"Block {blockHash.ToHex(true)} not found");
            return await TraceBlockOpcodeAsync(block, blockHash, config);
        }

        public virtual async Task<List<BlockResponseItemDto<CallTraceResult>>> TraceBlockCallTracerByNumberAsync(
            BigInteger blockNumber)
        {
            var block = await _blockStore.GetByNumberAsync(blockNumber);
            if (block == null)
                throw new InvalidOperationException($"Block {blockNumber} not found");
            var blockHash = await _blockStore.GetHashByNumberAsync(blockNumber);
            return await TraceBlockCallTracerAsync(block, blockHash);
        }

        public virtual async Task<List<BlockResponseItemDto<CallTraceResult>>> TraceBlockCallTracerByHashAsync(
            byte[] blockHash)
        {
            var block = await _blockStore.GetByHashAsync(blockHash);
            if (block == null)
                throw new InvalidOperationException($"Block {blockHash.ToHex(true)} not found");
            return await TraceBlockCallTracerAsync(block, blockHash);
        }

        private Task<List<BlockResponseItemDto<OpcodeTraceResult>>> TraceBlockOpcodeAsync(
            BlockHeader block, byte[] blockHash, OpcodeTraceConfig config)
            => TraceBlockAsync(block, blockHash, (exec, _) => BuildBlockOpcodeResult(exec, config));

        private Task<List<BlockResponseItemDto<CallTraceResult>>> TraceBlockCallTracerAsync(
            BlockHeader block, byte[] blockHash)
            => TraceBlockAsync(block, blockHash, BuildBlockCallTraceResult);

        private async Task<List<BlockResponseItemDto<T>>> TraceBlockAsync<T>(
            BlockHeader block,
            byte[] blockHash,
            Func<Nethereum.EVM.TransactionExecutionResult, ISignedTransaction, T> convert)
        {
            var canonicalHash = await _blockStore.GetHashByNumberAsync(block.BlockNumber);
            if (canonicalHash == null ||
                !canonicalHash.ToHex().Equals(blockHash.ToHex(), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"Block {blockHash.ToHex(true)} is not canonical; tracing non-canonical blocks is not supported");

            var blockTxs = await _transactionStore.GetByBlockHashAsync(blockHash);
            var results = new List<BlockResponseItemDto<T>>(blockTxs?.Count ?? 0);
            if (blockTxs == null || blockTxs.Count == 0)
                return results;

            var blockContext = BlockExecutor.BuildBlockContext(block, ForkStampedConfigFor(block));
            var targetBlock = block.BlockNumber > 0 ? block.BlockNumber - 1 : 0;
            var traceNodeDataService = await GetNodeDataServiceAtBlockAsync(targetBlock);
            var executionStateService = new ExecutionStateService(traceNodeDataService);

            foreach (var tx in blockTxs)
            {
                var exec = await ExecuteBlockTxAsync(tx, blockContext, executionStateService, traceEnabled: true);
                results.Add(new BlockResponseItemDto<T>
                {
                    TxHash = tx.Hash.ToHex(true),
                    Result = convert(exec, tx)
                });
            }
            return results;
        }

        private static OpcodeTraceResult BuildBlockOpcodeResult(
            Nethereum.EVM.TransactionExecutionResult exec, OpcodeTraceConfig config)
        {
            if (exec?.ProgramResult == null)
                return new OpcodeTraceResult
                {
                    Gas = (ulong)(exec?.GasUsed ?? 0),
                    Failed = false,
                    ReturnValue = "0x",
                    StructLogs = new List<OpcodeTraceStep>()
                };

            return TraceConverter.ConvertToOpcodeResult(exec.Program, config, exec.GasUsed);
        }

        private CallTraceResult BuildBlockCallTraceResult(
            Nethereum.EVM.TransactionExecutionResult exec, ISignedTransaction tx)
        {
            var callInput = BuildTraceCallInput(tx);
            var isContractCreation = tx.IsContractCreation();

            if (exec?.ProgramResult == null)
                return new CallTraceResult
                {
                    Type = isContractCreation ? "CREATE" : "CALL",
                    From = callInput.From,
                    To = isContractCreation ? null : callInput.To,
                    Value = callInput.Value ?? new HexBigInteger(0),
                    Gas = callInput.Gas ?? new HexBigInteger(0),
                    GasUsed = new HexBigInteger(exec?.GasUsed ?? 0),
                    Input = "0x",
                    Output = "0x"
                };

            return TraceConverter.ConvertToCallTraceResult(
                exec.Program, callInput, isContractCreation, exec.GasUsed);
        }
    }
}
