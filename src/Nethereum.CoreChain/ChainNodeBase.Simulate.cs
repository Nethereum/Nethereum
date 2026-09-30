using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Composition;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.State;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Tracing;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;

namespace Nethereum.CoreChain
{
    public abstract partial class ChainNodeBase
    {
        private const int MaxSimulateBlockCount = 256;

        public virtual async Task<List<EthSimulateBlockResult>> SimulateAsync(EthSimulateInput input, BigInteger? baseBlockNumber)
        {
            var resolvedBaseBlockNumber = baseBlockNumber ?? await _blockStore.GetHeightAsync();
            var baseBlockHeader = await _blockStore.GetByNumberAsync(resolvedBaseBlockNumber);
            if (baseBlockHeader == null)
                throw new Rpc.RpcException(-32000, "header not found");
            var baseBlockHash = await _blockStore.GetHashByNumberAsync(resolvedBaseBlockNumber) ?? new byte[32];
            var baseBlockContext = await GetBlockContextAtBlockAsync(resolvedBaseBlockNumber);

            var stateForSimulate = await BuildIsolatedSimulateStateStoreAsync(baseBlockNumber);
            var trieNodeStoreForSimulate = _trieNodeStore != null
                ? (ITrieNodeStore)new ReadOnlyTrieNodeStoreWrapper(_trieNodeStore)
                : new InMemoryContentNodeStore();

            var activations = Config.ResolveActivations();
            var calculator = new IncrementalStateRootCalculator(stateForSimulate, trieNodeStoreForSimulate);
            var engine = new BlockExecutor(
                stateForSimulate,
                _blockStore,
                activations,
                chainConfigFactory: fork => new ChainConfig
                {
                    ChainId = Config.ChainId,
                    BaseFee = Config.BaseFee,
                    Registry = Config.Registry,
                    RpcGasCap = Config.RpcGasCap,
                    Hardfork = fork.ToString().ToLowerInvariant()
                },
                hardforkConfigFactory: Config.ConfigForFork,
                stateRootCalculator: calculator,
                rewardPolicy: NoRewardPolicy.Instance,
                trieNodeStore: trieNodeStoreForSimulate);

            var producer = new BlockProducer(
                engine, _blockStore, _transactionStore, _receiptStore, _logStore, stateForSimulate,
                trieNodeStoreForSimulate, calculator, hardforkConfigFactory: Config.ConfigForFork);

            var simulateCallOptions = new SimulateCallOptions
            {
                Validation = input.Validation ?? false,
                TraceTransfers = input.TraceTransfers ?? false,
                ExpectedNonces = new Dictionary<string, EvmUInt256>()
            };

            if (input.BlockStateCalls.Count > MaxSimulateBlockCount)
                throw new Rpc.RpcException(-38026, "too many blocks");

            var baseFork = activations.ResolveAt((long)resolvedBaseBlockNumber, (ulong)baseBlockContext.Timestamp);
            var simulatePrecompiles = Config.ConfigForFork(baseFork).Precompiles;

            var results = new List<EthSimulateBlockResult>();
            var previousNumber = baseBlockContext.BlockNumber;
            var previousTimestamp = baseBlockContext.Timestamp;
            var previousGasLimit = baseBlockContext.GasLimit;
            var previousCoinbase = baseBlockContext.Coinbase;
            var parentHash = baseBlockHash;
            var parentHeader = baseBlockHeader;
            var totalBlocks = 0;
            var globalGasUsed = BigInteger.Zero;

            async Task ProduceSimulatedBlockResultAsync(
                BlockOverrides overrides, List<TransactionInput> calls, Dictionary<string, AccountOverride> stateOverrides)
            {
                if (++totalBlocks > MaxSimulateBlockCount)
                    throw new Rpc.RpcException(-38026, "too many blocks");

                var workingBlockContext = BuildWorkingBlockContext(
                    baseBlockContext, overrides, previousNumber, previousTimestamp,
                    previousGasLimit, previousCoinbase);

                previousNumber = workingBlockContext.BlockNumber;
                previousTimestamp = workingBlockContext.Timestamp;
                previousGasLimit = workingBlockContext.GasLimit;
                previousCoinbase = workingBlockContext.Coinbase;

                var productionOptions = new BlockProductionOptions
                {
                    Timestamp = workingBlockContext.Timestamp,
                    Coinbase = workingBlockContext.Coinbase,
                    BaseFee = workingBlockContext.BaseFee,
                    BlockGasLimit = workingBlockContext.GasLimit,
                    Difficulty = workingBlockContext.Difficulty,
                    PrevRandao = workingBlockContext.PrevRandao,
                    ExtraData = Array.Empty<byte>(),
                    ChainId = workingBlockContext.ChainId,
                    ParentBeaconBlockRoot = null,
                    Nonce = null,
                    SlotNumber = workingBlockContext.SlotNumber,
                    Withdrawals = null
                };

                var blockCalls = calls ?? new List<TransactionInput>();
                var txEntries = new List<TxEntry>(blockCalls.Count);
                foreach (var call in blockCalls)
                {
                    var resolvedCall = ResolveCallForSimulate(call);
                    var syntheticTx = BlockExecutor.BuildSyntheticSimulateTransaction(resolvedCall, workingBlockContext.ChainId);
                    txEntries.Add(new TxEntry(syntheticTx, resolvedCall.From ?? AddressUtil.ZERO_ADDRESS, resolvedCall));
                }

                Dictionary<string, int> blockRelocations = null;
                if (stateOverrides != null)
                {
                    blockRelocations = BuildBlockPrecompileMoves(stateOverrides, simulatePrecompiles);
                    await ApplyStateOverridesToStoreAsync(
                        stateForSimulate, AccountOverrideMapper.ToStateOverrideSet(stateOverrides));
                }
                simulateCallOptions.PrecompileRelocations = blockRelocations != null && blockRelocations.Count > 0
                    ? blockRelocations : null;

                var baseFeeOverride = ResolveSimulateBaseFeeOverride(overrides, simulateCallOptions.Validation);
                var excessBlobGasOverride = ResolveSimulateExcessBlobGasOverride(
                    activations, workingBlockContext, parentHeader);

                var production = await producer.ProduceSimulatedBlockAsync(
                    txEntries, productionOptions, parentHash, parentHeader, workingBlockContext.BlockNumber,
                    simulateCallOptions, baseFeeOverride, excessBlobGasOverride, globalGasUsed);

                parentHash = production.BlockHash;
                parentHeader = production.Header;
                globalGasUsed += production.Header.GasUsed;

                var blockSize = BlockHeaderExtensions.CalculateFullBlockSize(
                    production.Header, production.IncludedTransactions, uncles: null, withdrawals: null);

                var blockResult = new EthSimulateBlockResult();
                BlockHeaderExtensions.PopulateCommonFields(blockResult, production.Header, production.BlockHash, blockSize, withdrawals: null, uncles: null);
                blockResult.Transactions = BuildSimulateTransactions(
                    production, input.ReturnFullTransactions ?? false);

                var callOutputs = new List<EthSimulateCallResult>();
                var blockLogIndex = 0;
                for (var callIndex = 0; callIndex < production.TransactionResults.Count; callIndex++)
                {
                    var callResult = production.TransactionResults[callIndex];
                    var callOutput = ToSimulateCallResult(callResult);
                    DecorateSimulateLogs(
                        callOutput.Logs, production.BlockHash, workingBlockContext.BlockNumber,
                        production.Header.Timestamp, callResult.TxHash, callIndex, ref blockLogIndex);
                    callOutputs.Add(callOutput);
                }
                blockResult.Calls = callOutputs;

                results.Add(blockResult);
            }

            foreach (var blockStateCall in input.BlockStateCalls)
            {
                var overrides = blockStateCall.BlockOverrides;
                var targetNumber = overrides?.Number?.Value ?? (previousNumber + 1);

                if (overrides?.Number != null && targetNumber <= previousNumber)
                    throw new Rpc.RpcException(-38020,
                        $"block numbers must be in order: {targetNumber} <= {previousNumber}");

                while (previousNumber + 1 < targetNumber)
                    await ProduceSimulatedBlockResultAsync(null, null, null);

                if (overrides?.Time != null && (long)overrides.Time.Value <= previousTimestamp)
                    throw new Rpc.RpcException(-38021,
                        $"block timestamps must be in order: {(long)overrides.Time.Value} <= {previousTimestamp}");

                await ProduceSimulatedBlockResultAsync(
                    overrides, blockStateCall.Calls, blockStateCall.StateOverrides);
            }

            return results;
        }

        private static Dictionary<string, int> BuildBlockPrecompileMoves(
            Dictionary<string, AccountOverride> stateOverrides,
            Nethereum.EVM.Execution.Precompiles.PrecompileRegistry precompiles)
        {
            var relocations = new Dictionary<string, int>();
            foreach (var kvp in stateOverrides)
            {
                if (string.IsNullOrEmpty(kvp.Value?.MovePrecompileToAddress)) continue;

                var originalKey = Nethereum.EVM.Execution.Precompiles.PrecompileRelocationResolver.Normalize(kvp.Key);
                var targetKey = Nethereum.EVM.Execution.Precompiles.PrecompileRelocationResolver.Normalize(kvp.Value.MovePrecompileToAddress);

                int currentIndex;
                if (relocations.TryGetValue(originalKey, out var mapped))
                    currentIndex = mapped;
                else
                    currentIndex = Nethereum.EVM.Execution.Precompiles.PrecompileRelocationResolver.TryResolve(
                        null, precompiles, originalKey, out var natural) ? natural : 0;

                if (currentIndex <= 0)
                    throw new Rpc.RpcException(-32000, $"account {kvp.Key} is not a precompile");

                relocations[targetKey] = currentIndex;
                relocations[originalKey] = 0;
            }
            return relocations;
        }

        private static EvmUInt256? ResolveSimulateBaseFeeOverride(BlockOverrides overrides, bool validation)
        {
            if (overrides?.BaseFeePerGas != null)
                return (EvmUInt256)overrides.BaseFeePerGas.Value;

            return validation ? (EvmUInt256?)null : EvmUInt256.Zero;
        }

        private long? ResolveSimulateExcessBlobGasOverride(
            IChainActivations activations, BlockContext workingBlockContext, BlockHeader? parentHeader)
        {
            var fork = activations.ResolveAt((long)workingBlockContext.BlockNumber, (ulong)workingBlockContext.Timestamp);
            var targetBlobsPerBlock = Config.ConfigForFork(fork).TargetBlobsPerBlock;
            if (targetBlobsPerBlock <= 0) return null;

            var parentExcessBlobGas = parentHeader?.ExcessBlobGas is long parentExcess ? unchecked((ulong)parentExcess) : 0UL;
            var parentBlobGasUsed = parentHeader?.BlobGasUsed is long parentUsed ? unchecked((ulong)parentUsed) : 0UL;
            var targetBlobGasPerBlock = (ulong)targetBlobsPerBlock * BlobGasCalculator.GAS_PER_BLOB;

            return (long)BlobGasCalculator.CalculateExcessBlobGas(parentExcessBlobGas, parentBlobGasUsed, targetBlobGasPerBlock);
        }

        private static object[] BuildSimulateTransactions(BlockProductionResult production, bool returnFullTransactions)
        {
            var included = production.IncludedTransactions;
            var transactions = new object[included.Count];

            for (var i = 0; i < included.Count; i++)
            {
                transactions[i] = returnFullTransactions
                    ? TransactionRpcBuilder.Build(included[i], production.BlockHash, production.Header.BlockNumber, i, production.Header, production.TransactionResults[i].Sender)
                    : included[i].Hash.ToHex(true);
            }

            return transactions;
        }

        private async Task<IStateStore> BuildIsolatedSimulateStateStoreAsync(BigInteger? baseBlockNumber)
        {
            if (!baseBlockNumber.HasValue)
                return new StateLayer().Stores.WitnessCapture(_stateStore);

            if (_stateStore is IHistoricalStateProvider historyProvider)
            {
                return new StateLayer().Stores.WitnessCapture(
                    _stateStore, new HistoricalStateStoreReadAdapter(historyProvider, _stateStore, baseBlockNumber.Value));
            }

            var head = await _blockStore.GetHeightAsync().ConfigureAwait(false);
            if (baseBlockNumber.Value < head)
                throw new HistoricalStateNotAvailableException(baseBlockNumber.Value, head);

            return new StateLayer().Stores.WitnessCapture(_stateStore);
        }

        private static async Task ApplyStateOverridesToStoreAsync(
            IStateStore stateStore, Dictionary<string, StateOverride> overrides)
        {
            if (overrides == null) return;

            foreach (var kvp in overrides)
            {
                var address = kvp.Key;
                var stateOverride = kvp.Value;

                var setsBalance = stateOverride.Balance != null;
                var setsNonce = !string.IsNullOrEmpty(stateOverride.Nonce);
                var setsCode = !string.IsNullOrEmpty(stateOverride.Code);
                var setsStorage = stateOverride.State != null || stateOverride.StateDiff != null;

                if (!setsBalance && !setsNonce && !setsCode && !setsStorage)
                    continue;

                var account = await stateStore.GetAccountAsync(address).ConfigureAwait(false) ?? new Account
                {
                    Nonce = EvmUInt256.Zero,
                    Balance = EvmUInt256.Zero,
                    CodeHash = DefaultValues.EMPTY_DATA_HASH,
                    StateRoot = DefaultValues.EMPTY_TRIE_HASH
                };

                if (setsBalance)
                    account.Balance = (EvmUInt256)stateOverride.Balance.Value;

                if (setsNonce)
                    account.Nonce = EvmUInt256.FromHex(stateOverride.Nonce);

                if (setsCode)
                {
                    var code = stateOverride.Code.HexToByteArray();
                    var codeHash = new Sha3Keccack().CalculateHash(code);
                    await stateStore.SaveCodeAsync(codeHash, code).ConfigureAwait(false);
                    account.CodeHash = codeHash;
                }

                await stateStore.SaveAccountAsync(address, account).ConfigureAwait(false);

                if (stateOverride.State != null)
                {
                    await stateStore.ClearStorageAsync(address).ConfigureAwait(false);
                    foreach (var storageKvp in stateOverride.State)
                    {
                        await stateStore.SaveStorageAsync(
                            address, storageKvp.Key.HexToBigInteger(false), storageKvp.Value.HexToByteArray())
                            .ConfigureAwait(false);
                    }
                }

                if (stateOverride.StateDiff != null)
                {
                    foreach (var storageKvp in stateOverride.StateDiff)
                    {
                        await stateStore.SaveStorageAsync(
                            address, storageKvp.Key.HexToBigInteger(false), storageKvp.Value.HexToByteArray())
                            .ConfigureAwait(false);
                    }
                }
            }
        }

        private TransactionInput ResolveCallForSimulate(TransactionInput call)
        {
            return new TransactionInput
            {
                From = call.From,
                To = call.To,
                Data = call.Data,
                Value = call.Value,
                Gas = call.Gas == null ? null : new HexBigInteger(ResolveCallGas(call.Gas.Value)),
                GasPrice = call.GasPrice,
                MaxFeePerGas = call.MaxFeePerGas,
                MaxPriorityFeePerGas = call.MaxPriorityFeePerGas,
                Nonce = call.Nonce
            };
        }

        private const long DefaultBlockTimestampIncrementSeconds = 12;

        private static BlockContext BuildWorkingBlockContext(
            BlockContext baseBlockContext, BlockOverrides overrides, BigInteger previousNumber, long previousTimestamp,
            BigInteger previousGasLimit, string previousCoinbase)
        {
            return new BlockContext
            {
                BlockNumber = overrides?.Number?.Value ?? previousNumber + 1,
                Timestamp = overrides?.Time != null ? (long)overrides.Time.Value : previousTimestamp + DefaultBlockTimestampIncrementSeconds,
                Coinbase = !string.IsNullOrEmpty(overrides?.FeeRecipient) ? overrides.FeeRecipient : previousCoinbase,
                GasLimit = overrides?.GasLimit?.Value ?? previousGasLimit,
                BaseFee = overrides?.BaseFeePerGas?.Value ?? baseBlockContext.BaseFee,
                Difficulty = baseBlockContext.Difficulty,
                PrevRandao = !string.IsNullOrEmpty(overrides?.PrevRandao) ? overrides.PrevRandao.HexToByteArray() : baseBlockContext.PrevRandao,
                ChainId = baseBlockContext.ChainId,
                ExcessBlobGas = baseBlockContext.ExcessBlobGas,
                SlotNumber = baseBlockContext.SlotNumber
            };
        }

        private static EthSimulateCallResult ToSimulateCallResult(TransactionResult result)
        {
            if (!result.Success && result.IsOutOfGas)
            {
                return new EthSimulateCallResult
                {
                    Status = "0x0",
                    ReturnData = "0x",
                    GasUsed = new HexBigInteger(result.GasUsed),
                    MaxUsedGas = new HexBigInteger(result.GasUsed + result.GasRefund),
                    Logs = ToFilterLogs(result.Logs ?? result.Receipt?.Logs),
                    Error = new EthSimulateCallError { Message = "out of gas", Code = -32015 }
                };
            }

            if (!result.Success && result.IsRevert)
            {
                return new EthSimulateCallResult
                {
                    Status = "0x0",
                    ReturnData = "0x",
                    GasUsed = new HexBigInteger(result.GasUsed),
                    MaxUsedGas = new HexBigInteger(result.GasUsed + result.GasRefund),
                    Logs = ToFilterLogs(result.Logs ?? result.Receipt?.Logs),
                    Error = BuildRevertError(result)
                };
            }

            return new EthSimulateCallResult
            {
                Status = result.Success ? "0x1" : "0x0",
                ReturnData = result.ReturnData?.ToHex(true) ?? "0x",
                GasUsed = new HexBigInteger(result.GasUsed),
                MaxUsedGas = new HexBigInteger(result.GasUsed + result.GasRefund),
                Logs = ToFilterLogs(result.Logs ?? result.Receipt?.Logs),
                Error = result.Success ? null : (result.ErrorMessage ?? "revert")
            };
        }

        private static EthSimulateCallError BuildRevertError(TransactionResult result)
        {
            var reason = result.ErrorMessage;
            return new EthSimulateCallError
            {
                Message = string.IsNullOrEmpty(reason) ? "execution reverted" : "execution reverted: " + reason,
                Code = 3,
                Data = result.ReturnData != null && result.ReturnData.Length > 0 ? result.ReturnData.ToHex(true) : "0x"
            };
        }

        private static List<FilterLog> ToFilterLogs(List<Log> logs)
        {
            var filterLogs = new List<FilterLog>(logs?.Count ?? 0);
            if (logs == null) return filterLogs;

            foreach (var log in logs)
            {
                filterLogs.Add(new FilterLog
                {
                    Address = log.Address,
                    Data = log.Data?.ToHex(true) ?? "0x",
                    Topics = log.Topics?.Select(t => (object)t.ToHex(true)).ToArray() ?? Array.Empty<object>()
                });
            }
            return filterLogs;
        }

        private static void DecorateSimulateLogs(
            List<FilterLog> logs, byte[] blockHash, BigInteger blockNumber, long blockTimestamp,
            byte[] transactionHash, int callIndex, ref int blockLogIndex)
        {
            if (logs == null) return;

            var blockHashHex = blockHash?.ToHex(true);
            var transactionHashHex = transactionHash?.ToHex(true);

            foreach (var log in logs)
            {
                log.BlockHash = blockHashHex;
                log.TransactionHash = transactionHashHex;
                log.BlockTimestamp = new HexBigInteger(blockTimestamp);
                log.BlockNumber = new HexBigInteger(blockNumber);
                log.TransactionIndex = new HexBigInteger((BigInteger)callIndex);
                log.LogIndex = new HexBigInteger((BigInteger)blockLogIndex);
                log.Removed = false;
                blockLogIndex++;
            }
        }
    }
}
