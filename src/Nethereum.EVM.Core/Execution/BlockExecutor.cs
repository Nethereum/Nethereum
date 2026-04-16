using System.Collections.Generic;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Types;
using Nethereum.EVM.Witness;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;
#if !EVM_SYNC
using System.Threading.Tasks;
#endif

namespace Nethereum.EVM.Execution
{
    public static class BlockExecutor
    {
#if EVM_SYNC
        public static BlockExecutionResult Execute(
            BlockWitnessData block,
            IBlockEncodingProvider encodingProvider,
            HardforkRegistry hardforkRegistry,
            IStateRootCalculator stateRootCalculator = null,
            IBlockRootCalculator blockRootCalculator = null)
        {
            if (hardforkRegistry is null)
                throw new System.ArgumentNullException(nameof(hardforkRegistry));
            var accounts = WitnessStateBuilder.BuildAccountState(block.Accounts);
            var stateReader = new InMemoryStateReader(accounts);
            var config = ResolveConfig(block, hardforkRegistry);
            var executor = new TransactionExecutor(config: config);

            // EIP-4788: Beacon root system call (Cancun+)
            if (block.ParentBeaconBlockRoot != null && block.ParentBeaconBlockRoot.Length > 0)
            {
                ExecuteBeaconRootSystemCall(block, stateReader, accounts, executor);
            }

            long cumulativeGasUsed = 0;
            var txResults = new List<TransactionExecutionResult>();
            var receipts = new List<Receipt>();
            var encodedReceipts = new List<byte[]>();
            var encodedTxs = new List<byte[]>();
            var combinedBloom = new byte[256];
            var allTouchedAddresses = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < block.Transactions.Count; i++)
            {
                var wtx = block.Transactions[i];
                var executionState = new ExecutionStateService(stateReader);
                WitnessStateBuilder.LoadAllAccountsAndStorage(executionState, block.Accounts);

                var ctx = TransactionContextFactory.FromBlockWitnessTransaction(wtx, block, executionState);
                var result = executor.Execute(ctx);
                txResults.Add(result);

                cumulativeGasUsed += result.GasUsed;

                // Build receipt — failed/reverted txs have empty logs
                var txLogs = result.Success ? result.Logs : null;
                var modelLogs = EvmLogConverter.ToModelLogs(txLogs);
                var txBloom = LogBloomCalculator.CalculateBloom(modelLogs);
                LogBloomCalculator.CombineBloom(combinedBloom, txBloom);
                var receipt = Receipt.CreateStatusReceipt(result.Success, cumulativeGasUsed, txBloom, modelLogs);
                receipts.Add(receipt);

                // Typed receipt encoding: type_byte || RLP for EIP-2718 typed txs
                var txType = GetTransactionType(wtx.RlpEncoded);
                if (txType > 0)
                    encodedReceipts.Add(EncodeTypedReceipt(encodingProvider.EncodeReceipt(receipt), txType));
                else
                    encodedReceipts.Add(encodingProvider.EncodeReceipt(receipt));

                // Transaction bytes for trie — original signed RLP
                encodedTxs.Add(wtx.RlpEncoded);

                // Track all accounts touched by this tx
                foreach (var kvp in executionState.AccountsState)
                    allTouchedAddresses.Add(kvp.Key);

                stateReader.CommitChanges(executionState);
            }

            // EIP-4895: Process withdrawals (Shanghai+) — credit ETH, compute root
            byte[] withdrawalsRoot = null;
            if (block.Withdrawals != null)
            {
                var withdrawalES = new ExecutionStateService(stateReader);
                foreach (var addr in accounts.Keys)
                    withdrawalES.LoadBalanceNonceAndCodeFromStorage(addr);

                var encodedWithdrawals = new List<byte[]>();
                foreach (var w in block.Withdrawals)
                {
                    // Credit: amount is in Gwei, convert to Wei (multiply by 1e9)
                    var weiAmount = new EvmUInt256(w.AmountInGwei) * new EvmUInt256(1000000000);
                    var acct = withdrawalES.CreateOrGetAccountExecutionState(w.Address);
                    acct.Balance.CreditExecutionBalance(weiAmount);

                    var addrBytes = AddressUtil.Current.ConvertToValid20ByteAddress(w.Address)
                        .HexToByteArray();
                    encodedWithdrawals.Add(encodingProvider.EncodeWithdrawal(
                        w.Index, w.ValidatorIndex, addrBytes, w.AmountInGwei));
                }

                stateReader.CommitChanges(withdrawalES);

                if (blockRootCalculator != null && encodedWithdrawals.Count > 0)
                    withdrawalsRoot = blockRootCalculator.ComputeReceiptsRoot(encodedWithdrawals);
                else
                    withdrawalsRoot = "56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421".HexToByteArray();
            }

            // Enforce invariants
            if (block.ProduceBlockCommitments)
                block.ComputePostStateRoot = true;

            // State root
            byte[] stateRoot = null;
            ExecutionStateService finalES = null;
            if (block.ComputePostStateRoot && stateRootCalculator != null)
            {
                finalES = new ExecutionStateService(stateReader);
                // Collect all addresses: pre-state + touched during execution
                var allAddresses = new HashSet<string>(accounts.Keys, System.StringComparer.OrdinalIgnoreCase);
                foreach (var addr in allTouchedAddresses)
                    allAddresses.Add(addr);

                // Load balance, nonce, code, and storage for all accounts
                foreach (var addr in allAddresses)
                {
                    finalES.LoadBalanceNonceAndCodeFromStorage(addr);
                    var readerAcct = stateReader.GetAccountState(addr);
                    if (readerAcct != null && readerAcct.Storage != null && readerAcct.Storage.Count > 0)
                    {
                        var acctState = finalES.CreateOrGetAccountExecutionState(addr);
                        foreach (var s in readerAcct.Storage)
                            acctState.SetPreStateStorage(s.Key, s.Value);
                    }
                }
                stateRoot = stateRootCalculator.ComputeStateRoot(finalES);
            }

            // Block proof: tx root, receipts root, block hash
            byte[] transactionsRoot = null, receiptsRoot = null, blockHash = null;
            if (block.ProduceBlockCommitments && blockRootCalculator != null)
            {
                transactionsRoot = blockRootCalculator.ComputeTransactionsRoot(encodedTxs);
                receiptsRoot = blockRootCalculator.ComputeReceiptsRoot(encodedReceipts);

                var header = new BlockHeader
                {
                    ParentHash = block.ParentHash ?? new byte[32],
                    UnclesHash = "1dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347".HexToByteArray(),
                    Coinbase = block.Coinbase,
                    StateRoot = stateRoot ?? new byte[32],
                    TransactionsHash = transactionsRoot,
                    ReceiptHash = receiptsRoot,
                    LogsBloom = combinedBloom,
                    Difficulty = block.Difficulty != null ? EvmUInt256.FromBigEndian(block.Difficulty) : EvmUInt256.Zero,
                    BlockNumber = block.BlockNumber,
                    GasLimit = block.BlockGasLimit,
                    GasUsed = cumulativeGasUsed,
                    Timestamp = block.Timestamp,
                    ExtraData = block.ExtraData ?? new byte[0],
                    MixHash = block.MixHash ?? new byte[32],
                    Nonce = block.Nonce ?? new byte[8],
                    BaseFee = block.BaseFee,
                    WithdrawalsRoot = withdrawalsRoot,
                    ParentBeaconBlockRoot = block.ParentBeaconBlockRoot,
                    BlobGasUsed = block.BlobGasUsed,
                    ExcessBlobGas = block.ExcessBlobGas,
                    RequestsHash = block.RequestsHash
                };

                var encoded = encodingProvider.EncodeBlockHeader(header);
                blockHash = new Sha3Keccack().CalculateHash(encoded);
            }

            return new BlockExecutionResult
            {
                TxResults = txResults,
                Receipts = receipts,
                CombinedBloom = combinedBloom,
                CumulativeGasUsed = cumulativeGasUsed,
                StateRoot = stateRoot,
                TransactionsRoot = transactionsRoot,
                ReceiptsRoot = receiptsRoot,
                BlockHash = blockHash,
                FinalExecutionState = finalES,
                StateReader = stateReader
            };
        }

        private static void ExecuteBeaconRootSystemCall(
            BlockWitnessData block,
            InMemoryStateReader stateReader,
            Dictionary<string, AccountState> accounts,
            TransactionExecutor executor)
        {
            const string BEACON_ROOT_CONTRACT = "0x000f3df6d732807ef1319fb7b8bb8522d0beac02";
            const string SYSTEM_CALLER = "0xfffffffffffffffffffffffffffffffffffffffe";
            const long SYSTEM_CALL_GAS = 30000000;

            var executionState = new ExecutionStateService(stateReader);
            WitnessStateBuilder.LoadAllAccountsAndStorage(executionState, block.Accounts);

            // Mark addresses warm (matches Geth: AddAddressToAccessList)
            executionState.MarkAddressAsWarm(BEACON_ROOT_CONTRACT);

            // Load the contract code
            var contractCode = executionState.GetCode(BEACON_ROOT_CONTRACT);
            if (contractCode == null || contractCode.Length == 0)
            {
                stateReader.CommitChanges(executionState);
                return;
            }

            var callData = block.ParentBeaconBlockRoot.Length >= 32
                ? block.ParentBeaconBlockRoot
                : PadTo32(block.ParentBeaconBlockRoot);

            // Direct EVM call — matches Geth's evm.Call() path, not a full transaction
            var callContext = new EvmCallContext
            {
                From = SYSTEM_CALLER,
                To = BEACON_ROOT_CONTRACT,
                Data = callData,
                Gas = SYSTEM_CALL_GAS,
                Value = EvmUInt256.Zero,
                GasPrice = EvmUInt256.Zero,
                ChainId = block.ChainId
            };

            var programContext = new ProgramContext(
                callContext,
                executionState,
                null,
                blockNumber: block.BlockNumber,
                timestamp: block.Timestamp,
                coinbase: block.Coinbase,
                baseFee: block.BaseFee
            );
            programContext.Difficulty = block.Difficulty != null ? EvmUInt256.FromBigEndian(block.Difficulty) : EvmUInt256.Zero;
            programContext.GasLimit = block.BlockGasLimit;

            var program = new Program(contractCode, programContext);
            var evmSimulator = executor.GetSimulator();
            evmSimulator.ExecuteWithCallStack(program, traceEnabled: false);

            // Finalize — commit storage changes regardless of success
            stateReader.CommitChanges(executionState);
        }

#endif

        private static HardforkConfig ResolveConfig(BlockWitnessData block, HardforkRegistry registry)
        {
            var fork = block.Features?.Fork ?? HardforkName.Unspecified;
            if (fork == HardforkName.Unspecified)
                throw new System.InvalidOperationException(
                    "BlockWitnessData.Features.Fork is Unspecified. Witness producers must " +
                    "stamp the fork explicitly — block-number/timestamp activation is a " +
                    "per-chain concern done at witness build time, not in BlockExecutor.");
            return registry.Get(fork);
        }

        private static byte GetTransactionType(byte[] rlpEncoded)
        {
            if (rlpEncoded == null || rlpEncoded.Length == 0) return 0;
            var firstByte = rlpEncoded[0];
            // EIP-2718: typed transactions have first byte 0x00-0x7f
            // Legacy transactions have first byte >= 0xc0 (RLP list prefix)
            return firstByte < 0x80 ? firstByte : (byte)0;
        }

        private static byte[] EncodeTypedReceipt(byte[] encodedReceipt, byte txType)
        {
            var result = new byte[encodedReceipt.Length + 1];
            result[0] = txType;
            System.Array.Copy(encodedReceipt, 0, result, 1, encodedReceipt.Length);
            return result;
        }

        private static byte[] PadTo32(byte[] data)
        {
            if (data == null) return new byte[32];
            if (data.Length >= 32) return data;
            var padded = new byte[32];
            System.Array.Copy(data, 0, padded, 32 - data.Length, data.Length);
            return padded;
        }

#if !EVM_SYNC
        public static async Task<BlockExecutionResult> ExecuteAsync(
            BlockWitnessData block,
            IBlockEncodingProvider encodingProvider,
            HardforkRegistry hardforkRegistry,
            IStateRootCalculator stateRootCalculator = null,
            IBlockRootCalculator blockRootCalculator = null)
        {
            if (hardforkRegistry is null)
                throw new System.ArgumentNullException(nameof(hardforkRegistry));
            var accounts = WitnessStateBuilder.BuildAccountState(block.Accounts);
            var stateReader = new InMemoryStateReader(accounts);
            var config = ResolveConfig(block, hardforkRegistry);
            var executor = new TransactionExecutor(config: config);

            // EIP-4788: Beacon root system call (Cancun+)
            if (block.ParentBeaconBlockRoot != null && block.ParentBeaconBlockRoot.Length > 0)
            {
                await ExecuteBeaconRootSystemCallAsync(block, stateReader, accounts, executor);
            }

            long cumulativeGasUsed = 0;
            var txResults = new List<TransactionExecutionResult>();
            var receipts = new List<Receipt>();
            var encodedReceipts = new List<byte[]>();
            var encodedTxs = new List<byte[]>();
            var combinedBloom = new byte[256];
            var allTouchedAddresses = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < block.Transactions.Count; i++)
            {
                var wtx = block.Transactions[i];
                var executionState = new ExecutionStateService(stateReader);
                await WitnessStateBuilder.LoadAllAccountsAndStorageAsync(executionState, block.Accounts);

                var ctx = TransactionContextFactory.FromBlockWitnessTransaction(wtx, block, executionState);
                var result = await executor.ExecuteAsync(ctx);
                txResults.Add(result);

                cumulativeGasUsed += result.GasUsed;

                // Build receipt — failed/reverted txs have empty logs
                var txLogs = result.Success ? result.Logs : null;
                var modelLogs = EvmLogConverter.ToModelLogs(txLogs);
                var txBloom = LogBloomCalculator.CalculateBloom(modelLogs);
                LogBloomCalculator.CombineBloom(combinedBloom, txBloom);
                var receipt = Receipt.CreateStatusReceipt(result.Success, cumulativeGasUsed, txBloom, modelLogs);
                receipts.Add(receipt);

                // Typed receipt encoding: type_byte || RLP for EIP-2718 typed txs
                var txType = GetTransactionType(wtx.RlpEncoded);
                if (txType > 0)
                    encodedReceipts.Add(EncodeTypedReceipt(encodingProvider.EncodeReceipt(receipt), txType));
                else
                    encodedReceipts.Add(encodingProvider.EncodeReceipt(receipt));

                // Transaction bytes for trie — original signed RLP
                encodedTxs.Add(wtx.RlpEncoded);

                // Track all accounts touched by this tx
                foreach (var kvp in executionState.AccountsState)
                    allTouchedAddresses.Add(kvp.Key);

                stateReader.CommitChanges(executionState);
            }

            // EIP-4895: Process withdrawals (Shanghai+) — credit ETH, compute root
            byte[] withdrawalsRoot = null;
            if (block.Withdrawals != null)
            {
                var withdrawalES = new ExecutionStateService(stateReader);
                foreach (var addr in accounts.Keys)
                    await withdrawalES.LoadBalanceNonceAndCodeFromStorageAsync(addr);

                var encodedWithdrawals = new List<byte[]>();
                foreach (var w in block.Withdrawals)
                {
                    // Credit: amount is in Gwei, convert to Wei (multiply by 1e9)
                    var weiAmount = new EvmUInt256(w.AmountInGwei) * new EvmUInt256(1000000000);
                    var acct = withdrawalES.CreateOrGetAccountExecutionState(w.Address);
                    acct.Balance.CreditExecutionBalance(weiAmount);

                    var addrBytes = AddressUtil.Current.ConvertToValid20ByteAddress(w.Address)
                        .HexToByteArray();
                    encodedWithdrawals.Add(encodingProvider.EncodeWithdrawal(
                        w.Index, w.ValidatorIndex, addrBytes, w.AmountInGwei));
                }

                stateReader.CommitChanges(withdrawalES);

                if (blockRootCalculator != null && encodedWithdrawals.Count > 0)
                    withdrawalsRoot = blockRootCalculator.ComputeReceiptsRoot(encodedWithdrawals);
                else
                    withdrawalsRoot = "56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421".HexToByteArray();
            }

            // Enforce invariants
            if (block.ProduceBlockCommitments)
                block.ComputePostStateRoot = true;

            // State root
            byte[] stateRoot = null;
            ExecutionStateService finalES = null;
            if (block.ComputePostStateRoot && stateRootCalculator != null)
            {
                finalES = new ExecutionStateService(stateReader);
                // Collect all addresses: pre-state + touched during execution
                var allAddresses = new HashSet<string>(accounts.Keys, System.StringComparer.OrdinalIgnoreCase);
                foreach (var addr in allTouchedAddresses)
                    allAddresses.Add(addr);

                // Load balance, nonce, code, and storage for all accounts
                foreach (var addr in allAddresses)
                {
                    await finalES.LoadBalanceNonceAndCodeFromStorageAsync(addr);
                    var readerAcct = stateReader.GetAccountState(addr);
                    if (readerAcct != null && readerAcct.Storage != null && readerAcct.Storage.Count > 0)
                    {
                        var acctState = finalES.CreateOrGetAccountExecutionState(addr);
                        foreach (var s in readerAcct.Storage)
                            acctState.SetPreStateStorage(s.Key, s.Value);
                    }
                }
                stateRoot = stateRootCalculator.ComputeStateRoot(finalES);
            }

            // Block proof: tx root, receipts root, block hash
            byte[] transactionsRoot = null, receiptsRoot = null, blockHash = null;
            if (block.ProduceBlockCommitments && blockRootCalculator != null)
            {
                transactionsRoot = blockRootCalculator.ComputeTransactionsRoot(encodedTxs);
                receiptsRoot = blockRootCalculator.ComputeReceiptsRoot(encodedReceipts);

                var header = new BlockHeader
                {
                    ParentHash = block.ParentHash ?? new byte[32],
                    UnclesHash = "1dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347".HexToByteArray(),
                    Coinbase = block.Coinbase,
                    StateRoot = stateRoot ?? new byte[32],
                    TransactionsHash = transactionsRoot,
                    ReceiptHash = receiptsRoot,
                    LogsBloom = combinedBloom,
                    Difficulty = block.Difficulty != null ? EvmUInt256.FromBigEndian(block.Difficulty) : EvmUInt256.Zero,
                    BlockNumber = block.BlockNumber,
                    GasLimit = block.BlockGasLimit,
                    GasUsed = cumulativeGasUsed,
                    Timestamp = block.Timestamp,
                    ExtraData = block.ExtraData ?? new byte[0],
                    MixHash = block.MixHash ?? new byte[32],
                    Nonce = block.Nonce ?? new byte[8],
                    BaseFee = block.BaseFee,
                    WithdrawalsRoot = withdrawalsRoot,
                    ParentBeaconBlockRoot = block.ParentBeaconBlockRoot,
                    BlobGasUsed = block.BlobGasUsed,
                    ExcessBlobGas = block.ExcessBlobGas,
                    RequestsHash = block.RequestsHash
                };

                var encoded = encodingProvider.EncodeBlockHeader(header);
                blockHash = new Sha3Keccack().CalculateHash(encoded);
            }

            return new BlockExecutionResult
            {
                TxResults = txResults,
                Receipts = receipts,
                CombinedBloom = combinedBloom,
                CumulativeGasUsed = cumulativeGasUsed,
                StateRoot = stateRoot,
                TransactionsRoot = transactionsRoot,
                ReceiptsRoot = receiptsRoot,
                BlockHash = blockHash,
                FinalExecutionState = finalES,
                StateReader = stateReader
            };
        }

        private static async Task ExecuteBeaconRootSystemCallAsync(
            BlockWitnessData block,
            InMemoryStateReader stateReader,
            Dictionary<string, AccountState> accounts,
            TransactionExecutor executor)
        {
            const string BEACON_ROOT_CONTRACT = "0x000f3df6d732807ef1319fb7b8bb8522d0beac02";
            const string SYSTEM_CALLER = "0xfffffffffffffffffffffffffffffffffffffffe";
            const long SYSTEM_CALL_GAS = 30000000;

            var executionState = new ExecutionStateService(stateReader);
            await WitnessStateBuilder.LoadAllAccountsAndStorageAsync(executionState, block.Accounts);

            // Mark addresses warm (matches Geth: AddAddressToAccessList)
            executionState.MarkAddressAsWarm(BEACON_ROOT_CONTRACT);

            // Load the contract code
            var contractCode = await executionState.GetCodeAsync(BEACON_ROOT_CONTRACT);
            if (contractCode == null || contractCode.Length == 0)
            {
                stateReader.CommitChanges(executionState);
                return;
            }

            var callData = block.ParentBeaconBlockRoot.Length >= 32
                ? block.ParentBeaconBlockRoot
                : PadTo32(block.ParentBeaconBlockRoot);

            // Direct EVM call — matches Geth's evm.Call() path, not a full transaction
            var callContext = new EvmCallContext
            {
                From = SYSTEM_CALLER,
                To = BEACON_ROOT_CONTRACT,
                Data = callData,
                Gas = SYSTEM_CALL_GAS,
                Value = EvmUInt256.Zero,
                GasPrice = EvmUInt256.Zero,
                ChainId = block.ChainId
            };

            var programContext = new ProgramContext(
                callContext,
                executionState,
                null,
                blockNumber: block.BlockNumber,
                timestamp: block.Timestamp,
                coinbase: block.Coinbase,
                baseFee: block.BaseFee
            );
            programContext.Difficulty = block.Difficulty != null ? EvmUInt256.FromBigEndian(block.Difficulty) : EvmUInt256.Zero;
            programContext.GasLimit = block.BlockGasLimit;

            var program = new Program(contractCode, programContext);
            var evmSimulator = executor.GetSimulator();
            await evmSimulator.ExecuteWithCallStackAsync(program, traceEnabled: false);

            // Finalize — commit storage changes regardless of success
            stateReader.CommitChanges(executionState);
        }
#endif
    }
}
