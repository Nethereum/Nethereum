using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.BlockchainProcessing.BlockStorage.Entities.Mapping;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;

namespace Nethereum.BlockchainProcessing.Services
{
    public class EvmReplayInternalTransactionSource : IInternalTransactionSource
    {
        private readonly IEthApiService _ethApiService;
        private readonly HardforkConfig _hardforkConfig;
        private readonly ChainForkResolver _forkResolver;
        private readonly long _chainId;

        public EvmReplayInternalTransactionSource(IEthApiService ethApiService, HardforkConfig hardforkConfig)
        {
            _ethApiService = ethApiService ?? throw new ArgumentNullException(nameof(ethApiService));
            _hardforkConfig = hardforkConfig ?? throw new ArgumentNullException(nameof(hardforkConfig));
        }

        public EvmReplayInternalTransactionSource(
            IEthApiService ethApiService, ChainForkResolver forkResolver, long chainId)
        {
            _ethApiService = ethApiService ?? throw new ArgumentNullException(nameof(ethApiService));
            _forkResolver = forkResolver ?? throw new ArgumentNullException(nameof(forkResolver));
            _chainId = chainId;
        }

        private HardforkConfig RulesAt(long blockNumber, ulong timestamp) =>
            _hardforkConfig ?? _forkResolver.ResolveConfigAt(_chainId, blockNumber, timestamp);

        public async Task<List<InternalTransaction>> ProduceAsync(string transactionHash)
        {
            var tx = await _ethApiService.Transactions.GetTransactionByHash
                .SendRequestAsync(transactionHash).ConfigureAwait(false);
            if (tx == null || tx.BlockNumber == null)
                return new List<InternalTransaction>();

            var parentBlock = new BlockParameter(new HexBigInteger(tx.BlockNumber.Value - 1));
            var stateReader = new RpcNodeDataService(_ethApiService, parentBlock);
            var executionState = new ExecutionStateService(stateReader);

            var block = await _ethApiService.Blocks.GetBlockWithTransactionsHashesByNumber
                .SendRequestAsync(new BlockParameter(new HexBigInteger(tx.BlockNumber.Value)))
                .ConfigureAwait(false);

            var executor = new TransactionExecutor(RulesAt(
                (long)tx.BlockNumber.Value, block != null ? (ulong)block.Timestamp.Value : 0UL));

            var ctx = new TransactionExecutionContext
            {
                Sender = tx.From,
                To = tx.To,
                Data = string.IsNullOrEmpty(tx.Input) ? new byte[0] : tx.Input.HexToByteArray(),
                Value = EvmUInt256BigIntegerExtensions.FromBigInteger(tx.Value?.Value ?? BigInteger.Zero),
                GasLimit = (long)(tx.Gas?.Value ?? BigInteger.Zero),
                GasPrice = EvmUInt256BigIntegerExtensions.FromBigInteger(tx.GasPrice?.Value ?? BigInteger.Zero),
                MaxFeePerGas = EvmUInt256BigIntegerExtensions.FromBigInteger(tx.MaxFeePerGas?.Value ?? BigInteger.Zero),
                MaxPriorityFeePerGas = EvmUInt256BigIntegerExtensions.FromBigInteger(tx.MaxPriorityFeePerGas?.Value ?? BigInteger.Zero),
                Nonce = EvmUInt256BigIntegerExtensions.FromBigInteger(tx.Nonce?.Value ?? BigInteger.Zero),
                IsEip1559 = tx.MaxFeePerGas != null,
                IsContractCreation = string.IsNullOrEmpty(tx.To),
                BlockNumber = (long)tx.BlockNumber.Value,
                Timestamp = block != null ? (long)block.Timestamp.Value : 0L,
                Coinbase = block?.Miner,
                BaseFee = block != null ? (long)(block.BaseFeePerGas?.Value ?? BigInteger.Zero) : 0L,
                BlockGasLimit = block != null ? (long)block.GasLimit.Value : 30_000_000L,
                ExecutionState = executionState,
                TraceEnabled = false
            };

            var result = await executor.ExecuteAsync(ctx).ConfigureAwait(false);

            return Flatten(transactionHash, tx, result, result.GasUsed);
        }

        private static List<InternalTransaction> Flatten(
            string txHash, RPC.Eth.DTOs.Transaction tx, TransactionExecutionResult result, long topLevelGasUsed)
        {
            var list = new List<InternalTransaction>();

            var topType = string.IsNullOrEmpty(tx.To) ? "CREATE" : "CALL";
            list.Add(InternalTransactionMapping.CreateInternalTransaction(
                txHash, traceIndex: 0, depth: 0, type: topType,
                addressFrom: tx.From,
                addressTo: tx.To ?? string.Empty,
                value: (tx.Value?.Value ?? BigInteger.Zero).ToString(),
                gas: (tx.Gas?.Value ?? BigInteger.Zero).ToString(),
                gasUsed: topLevelGasUsed.ToString(),
                input: tx.Input ?? string.Empty,
                output: result.ReturnData?.ToHex(prefix: true) ?? string.Empty,
                error: result.Success ? null : (result.Error ?? "reverted"),
                revertReason: result.RevertReason));

            if (result.ProgramResult?.InnerCallResults != null)
            {
                var nextIndex = 1;
                foreach (var inner in result.ProgramResult.InnerCallResults)
                {
                    list.Add(InternalTransactionMapping.CreateInternalTransaction(
                        txHash, traceIndex: nextIndex++, depth: inner.Depth,
                        type: FrameTypeToString(inner.FrameType),
                        addressFrom: inner.CallInput?.From ?? string.Empty,
                        addressTo: inner.CallInput?.To ?? string.Empty,
                        value: inner.CallInput != null ? inner.CallInput.Value.ToBigInteger().ToString() : "0",
                        gas: inner.CallInput != null ? inner.CallInput.Gas.ToString() : "0",
                        gasUsed: inner.GasUsed.ToString(),
                        input: inner.CallInput?.Data?.ToHex(prefix: true) ?? string.Empty,
                        output: inner.Output?.ToHex(prefix: true) ?? string.Empty,
                        error: inner.Success ? null : (inner.Error ?? "reverted"),
                        revertReason: inner.RevertReason));
                }
            }

            return list;
        }

        private static string FrameTypeToString(int frameType)
        {
            switch ((CallFrameType)frameType)
            {
                case CallFrameType.Call: return "CALL";
                case CallFrameType.DelegateCall: return "DELEGATECALL";
                case CallFrameType.StaticCall: return "STATICCALL";
                case CallFrameType.CallCode: return "CALLCODE";
                case CallFrameType.Create: return "CREATE";
                case CallFrameType.Create2: return "CREATE2";
                default: return "CALL";
            }
        }
    }
}
