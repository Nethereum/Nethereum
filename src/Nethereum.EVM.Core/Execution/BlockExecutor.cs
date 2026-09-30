using Nethereum.Documentation;
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
        [NethereumDocExample(DocSection.EvmSimulator, "block-execution", "Execute a whole block from a witness")]
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

            var balBuilder = HeaderCommitsToBlockAccessList(config)
                ? new BlockAccessListBuilder()
                : null;
            var balCollector = AttachCollectorToItsOwnAccountGraph(balBuilder, block);

            RunPreExecutionSystemCalls(block, stateReader, executor, balCollector);

            long cumulativeGasUsed = 0;
            var isAmsterdamPlus = config.IntrinsicGasRules.StateGasActive;
            var capacity = new Nethereum.EVM.Gas.BlockGasCapacity();
            var txResults = new List<TransactionExecutionResult>();
            var receipts = new List<Receipt>();
            var encodedReceipts = new List<byte[]>();
            var encodedTxs = new List<byte[]>();
            var combinedBloom = new byte[256];
            var allTouchedAddresses = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < block.Transactions.Count; i++)
            {
                var wtx = block.Transactions[i];
                var executionState = OpenTransactionStateWithAccessRecorder(block, stateReader, balCollector, i);

                var ctx = BuildTransactionContext(wtx, block, executionState, capacity);
                var result = executor.Execute(ctx);
                txResults.Add(result);

                cumulativeGasUsed += result.GasUsed;
                ChargeBlockGasCapacity(capacity, ctx, result, isAmsterdamPlus);

                var receipt = BuildReceiptAndFoldIntoBlockBloom(config, result, cumulativeGasUsed, combinedBloom);
                receipts.Add(receipt);

                encodedReceipts.Add(EncodeReceiptForTrie(encodingProvider, receipt, wtx.RlpEncoded));

                encodedTxs.Add(wtx.RlpEncoded);

                AccumulateTouchedAddresses(allTouchedAddresses, executionState);

                CommitStateAndRecordAccess(stateReader, executionState, balCollector, TransactionAccessIndex(i));
            }

            var requests = ExecuteRequestSystemCalls(block, stateReader, executor, balCollector, receipts);
            var requestsHash = requests.Commitment();

            byte[] withdrawalsRoot = null;
            if (block.Withdrawals != null)
            {
                var withdrawalES = OpenWithdrawalState(stateReader, accounts);

                var encodedWithdrawals = CreditWithdrawalsTouchRecipientsAndEncode(
                    block, withdrawalES, balCollector, encodingProvider);

                CommitStateAndRecordAccess(stateReader, withdrawalES, balCollector, PostExecutionAccessIndex(block));

                withdrawalsRoot = ComputeWithdrawalsRoot(blockRootCalculator, encodedWithdrawals);
            }

            ForceComputePostStateRootWhenProducingCommitments(block);

            byte[] stateRoot = null;
            ExecutionStateService finalES = null;
            if (CanComputePostStateRoot(block, stateRootCalculator))
            {
                finalES = MaterialiseFinalState(stateReader, accounts, allTouchedAddresses);
                stateRoot = stateRootCalculator.ComputeStateRoot(finalES);
            }

            var headerGasUsed = capacity.HeaderGasUsed;
            var blockAccessList = balBuilder?.Build();

            byte[] transactionsRoot = null, receiptsRoot = null, blockHash = null;
            Model.BlockHeader producedHeader = null;
            if (CanProduceBlockCommitments(block, blockRootCalculator))
            {
                transactionsRoot = blockRootCalculator.ComputeTransactionsRoot(encodedTxs);
                receiptsRoot = blockRootCalculator.ComputeReceiptsRoot(encodedReceipts);

                var header = producedHeader = BuildProducedHeader(
                    requestsHash, block, stateRoot, transactionsRoot, receiptsRoot, combinedBloom,
                    headerGasUsed, withdrawalsRoot, blockAccessList);

                blockHash = HashProducedHeader(encodingProvider, header);
            }

            return new BlockExecutionResult
            {
                TxResults = txResults,
                Receipts = receipts,
                CombinedBloom = combinedBloom,
                CumulativeGasUsed = cumulativeGasUsed,
                HeaderGasUsed = headerGasUsed,
                BlockExecutionGasUsed = capacity.ExecutionGasUsed,
                BlockStateGasUsed = capacity.StateGasUsed,
                StateRoot = stateRoot,
                TransactionsRoot = transactionsRoot,
                ReceiptsRoot = receiptsRoot,
                BlockHash = blockHash,
                ProducedHeader = producedHeader,
                BlockAccessList = blockAccessList,
                BlockAccessListGasLimitExceeded =
                    BlockAccessListSizeRule.ExceedsBlockGasLimit(blockAccessList, block.BlockGasLimit),
                DeclaredBlockAccessListCheck = BlockAccessListStructureRule
                    .FindViolation(block.DeclaredBlockAccessList, block.Transactions.Count),
                FinalExecutionState = finalES,
                StateReader = stateReader
            };
        }


        private static byte[] ExecuteSystemCall(
            BlockWitnessData block,
            InMemoryStateReader stateReader,
            TransactionExecutor executor,
            BlockAccessListCollector balCollector,
            string contractAddress,
            byte[] callData,
            ulong blockAccessIndex)
        {
            var executionState = new ExecutionStateService(stateReader);
            WitnessStateBuilder.LoadAllAccountsAndStorage(executionState, stateReader, block.Accounts);

            SystemCallExecution.Prepare(executionState, balCollector, contractAddress, blockAccessIndex);

            byte[] returnData = null;
            var contractCode = executionState.GetCode(contractAddress);
            if (SystemCallExecution.IsAbsent(contractCode))
            {
                SystemCallExecution.RefuseBlockOnAbsentRequestPredeploy(contractAddress);
            }
            else
            {
                var simulator = executor.GetSimulator();
                var program = SystemCallExecution.BuildProgram(
                    block, executionState, simulator.Config, contractAddress, callData, contractCode);
                var snapshotId = SystemCallExecution.TakeCallSnapshot(executionState);
                simulator.ExecuteWithCallStack(program, traceEnabled: false);
                SystemCallExecution.SettleCallState(executionState, program, snapshotId);
                SystemCallExecution.RefuseBlockOnFatalCallFailure(contractAddress, program);
                returnData = program.ProgramResult.Result;
            }

            SystemCallExecution.Commit(stateReader, executionState, balCollector, blockAccessIndex);

            return returnData ?? new byte[0];
        }

        private static BlockExecutionRequests ExecuteRequestSystemCalls(
            BlockWitnessData block,
            InMemoryStateReader stateReader,
            TransactionExecutor executor,
            BlockAccessListCollector balCollector,
            List<Receipt> receipts)
        {
            var fork = block.Features?.Fork ?? HardforkName.Unspecified;
            var postExecutionIndex = PostExecutionAccessIndex(block);
            var requests = BlockExecutionRequests.OpenedWithDeposits(fork, LogsOf(receipts));
            if (block.SkipsRequestSystemCalls) return requests;

            foreach (var contract in SystemCallContracts.RequestContractsFor(fork))
            {
                requests.AddFrom(contract, ExecuteSystemCall(block, stateReader, executor, balCollector,
                    contract, new byte[0], postExecutionIndex));
            }

            return requests;
        }


        private static void RunPreExecutionSystemCalls(
            BlockWitnessData block,
            InMemoryStateReader stateReader,
            TransactionExecutor executor,
            BlockAccessListCollector balCollector)
        {
            if (CarriesParentBeaconBlockRoot(block))
            {
                ExecuteSystemCall(block, stateReader, executor, balCollector,
                    SystemCallContracts.BeaconRoots,
                    block.ParentBeaconBlockRoot.Length >= 32
                        ? block.ParentBeaconBlockRoot
                        : PadTo32(block.ParentBeaconBlockRoot),
                    PreExecutionAccessIndex);
            }

            if (block.Features?.Fork >= HardforkName.Prague
                && block.ParentHash != null && block.ParentHash.Length >= 32)
            {
                ExecuteSystemCall(block, stateReader, executor, balCollector,
                    SystemCallContracts.HistoryStorage, block.ParentHash, PreExecutionAccessIndex);
            }
        }

        private static ExecutionStateService OpenTransactionStateWithAccessRecorder(
            BlockWitnessData block,
            InMemoryStateReader stateReader,
            BlockAccessListCollector balCollector,
            int transactionIndex)
        {
            var executionState = new ExecutionStateService(stateReader);
            WitnessStateBuilder.LoadAllAccountsAndStorage(executionState, stateReader, block.Accounts);
            if (balCollector != null)
            {
                balCollector.BeginUnit(TransactionAccessIndex(transactionIndex));
                executionState.AccessRecorder = balCollector;
            }
            return executionState;
        }

        private static ExecutionStateService OpenWithdrawalState(
            InMemoryStateReader stateReader,
            Dictionary<string, AccountState> accounts)
        {
            var withdrawalES = new ExecutionStateService(stateReader);
            foreach (var addr in accounts.Keys)
                withdrawalES.LoadBalanceNonceAndCodeFromStorage(addr);
            return withdrawalES;
        }

        private static ExecutionStateService MaterialiseFinalState(
            InMemoryStateReader stateReader,
            Dictionary<string, AccountState> accounts,
            HashSet<string> allTouchedAddresses)
        {
            var finalES = new ExecutionStateService(stateReader);
            var allAddresses = new HashSet<string>(accounts.Keys, System.StringComparer.OrdinalIgnoreCase);
            foreach (var addr in allTouchedAddresses)
                allAddresses.Add(addr);

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
            return finalES;
        }
#endif

        private static IEnumerable<Log> LogsOf(List<Receipt> receipts)
        {
            if (receipts == null) yield break;

            foreach (var receipt in receipts)
            {
                if (receipt?.Logs == null) continue;
                foreach (var log in receipt.Logs) yield return log;
            }
        }

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

        private static void RecordWithdrawalTouch(BlockAccessListCollector balCollector, string address)
        {
            balCollector?.RecordAccountRead(address);
        }

        private static int BlobsIncluded(TransactionExecutionContext ctx, TransactionExecutionResult result)
        {
            if (result.IsValidationError || !ctx.IsType3Transaction || ctx.BlobVersionedHashes == null)
                return 0;
            return ctx.BlobVersionedHashes.Count;
        }

        private const ulong PreExecutionAccessIndex = 0;

        private static ulong TransactionAccessIndex(int i) => (ulong)(i + 1);

        private static ulong PostExecutionAccessIndex(BlockWitnessData block) =>
            (ulong)(block.Transactions.Count + 1);

        private static bool HeaderCommitsToBlockAccessList(HardforkConfig config) =>
            config.HeaderCodec != null && config.HeaderCodec.CarriesBlockAccessList;

        private static bool CarriesParentBeaconBlockRoot(BlockWitnessData block) =>
            block.ParentBeaconBlockRoot != null && block.ParentBeaconBlockRoot.Length > 0;

        private static bool CanComputePostStateRoot(BlockWitnessData block, IStateRootCalculator stateRootCalculator) =>
            block.ComputePostStateRoot && stateRootCalculator != null;

        private static bool CanProduceBlockCommitments(BlockWitnessData block, IBlockRootCalculator blockRootCalculator) =>
            block.ProduceBlockCommitments && blockRootCalculator != null;

        private static BlockAccessListCollector AttachCollectorToItsOwnAccountGraph(
            BlockAccessListBuilder balBuilder,
            BlockWitnessData block)
        {
            return balBuilder == null
                ? null
                : new BlockAccessListCollector(balBuilder,
                    new InMemoryStateReader(WitnessStateBuilder.BuildAccountState(block.Accounts)));
        }

        private static TransactionExecutionContext BuildTransactionContext(
            BlockWitnessTransaction wtx,
            BlockWitnessData block,
            ExecutionStateService executionState,
            Gas.BlockGasCapacity capacity)
        {
            var ctx = TransactionContextFactory.FromBlockWitnessTransaction(wtx, block, executionState);
            ctx.BlockGasCapacity = capacity;
            return ctx;
        }

        private static void ChargeBlockGasCapacity(
            Gas.BlockGasCapacity capacity,
            TransactionExecutionContext ctx,
            TransactionExecutionResult result,
            bool isAmsterdamPlus)
        {
            capacity.Add(result.GasUsed, result.ExecutionGasUsed, result.StateGasUsed,
                BlobsIncluded(ctx, result), isAmsterdamPlus);
        }

        private static Receipt BuildReceiptAndFoldIntoBlockBloom(
            HardforkConfig config,
            TransactionExecutionResult result,
            long cumulativeGasUsed,
            byte[] combinedBloom)
        {
            var txLogs = result.Success ? result.Logs : null;
            var modelLogs = EvmLogConverter.ToModelLogs(txLogs);
            var txBloom = LogBloomCalculator.CalculateBloom(modelLogs);
            LogBloomCalculator.CombineBloom(combinedBloom, txBloom);
            return config.ReceiptConstruction.Construct(
                result.Success, cumulativeGasUsed, txBloom, modelLogs, intermediatePostStateRoot: null);
        }

        private static byte[] EncodeReceiptForTrie(
            IBlockEncodingProvider encodingProvider,
            Receipt receipt,
            byte[] rlpEncoded)
        {
            var txType = GetTransactionType(rlpEncoded);
            if (txType > 0)
                return EncodeTypedReceipt(encodingProvider.EncodeReceipt(receipt), txType);
            else
                return encodingProvider.EncodeReceipt(receipt);
        }

        private static void AccumulateTouchedAddresses(
            HashSet<string> allTouchedAddresses,
            ExecutionStateService executionState)
        {
            foreach (var kvp in executionState.AccountsState)
                allTouchedAddresses.Add(kvp.Key.ToHexLower());
        }

        private static void CommitStateAndRecordAccess(
            InMemoryStateReader stateReader,
            ExecutionStateService executionState,
            BlockAccessListCollector balCollector,
            ulong blockAccessIndex)
        {
            stateReader.CommitChanges(executionState);
            balCollector?.Record(blockAccessIndex, executionState, stateReader);
        }

        private static List<byte[]> CreditWithdrawalsTouchRecipientsAndEncode(
            BlockWitnessData block,
            ExecutionStateService withdrawalES,
            BlockAccessListCollector balCollector,
            IBlockEncodingProvider encodingProvider)
        {
            var encodedWithdrawals = new List<byte[]>();
            foreach (var w in block.Withdrawals)
            {
                var weiAmount = new EvmUInt256(w.AmountInGwei) * new EvmUInt256(1000000000);
                var acct = withdrawalES.CreateOrGetAccountExecutionState(w.Address);
                acct.Balance.CreditExecutionBalance(weiAmount);
                RecordWithdrawalTouch(balCollector, w.Address);

                var addrBytes = AddressUtil.Current.ConvertToValid20ByteAddress(w.Address)
                    .HexToByteArray();
                encodedWithdrawals.Add(encodingProvider.EncodeWithdrawal(
                    w.Index, w.ValidatorIndex, addrBytes, w.AmountInGwei));
            }
            return encodedWithdrawals;
        }

        private static byte[] ComputeWithdrawalsRoot(
            IBlockRootCalculator blockRootCalculator,
            List<byte[]> encodedWithdrawals)
        {
            if (blockRootCalculator != null && encodedWithdrawals.Count > 0)
                return blockRootCalculator.ComputeReceiptsRoot(encodedWithdrawals);
            else
                return "56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421".HexToByteArray();
        }

        private static void ForceComputePostStateRootWhenProducingCommitments(BlockWitnessData block)
        {
            if (block.ProduceBlockCommitments)
                block.ComputePostStateRoot = true;
        }

        private static BlockHeader BuildProducedHeader(
            byte[] requestsHash,
            BlockWitnessData block,
            byte[] stateRoot,
            byte[] transactionsRoot,
            byte[] receiptsRoot,
            byte[] combinedBloom,
            long headerGasUsed,
            byte[] withdrawalsRoot,
            List<AccountChanges> blockAccessList)
        {
            return new BlockHeader
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
                GasUsed = headerGasUsed,
                Timestamp = block.Timestamp,
                ExtraData = block.ExtraData ?? new byte[0],
                MixHash = block.MixHash ?? new byte[32],
                Nonce = block.Nonce ?? new byte[8],
                BaseFee = block.BaseFee,
                WithdrawalsRoot = withdrawalsRoot,
                ParentBeaconBlockRoot = block.ParentBeaconBlockRoot,
                BlobGasUsed = block.BlobGasUsed,
                ExcessBlobGas = block.ExcessBlobGas,
                RequestsHash = requestsHash,
                SlotNumber = block.SlotNumber,
                BlockAccessListHash = blockAccessList == null
                    ? null
                    : new BlockAccessListRLPEncoder().Hash(blockAccessList)
            };
        }

        private static byte[] HashProducedHeader(IBlockEncodingProvider encodingProvider, BlockHeader header)
        {
            var encoded = encodingProvider.EncodeBlockHeader(header);
            return new Sha3Keccack().CalculateHash(encoded);
        }

#if !EVM_SYNC
        [NethereumDocExample(DocSection.EvmSimulator, "block-execution", "Execute a whole block from a witness")]
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

            var balBuilder = HeaderCommitsToBlockAccessList(config)
                ? new BlockAccessListBuilder()
                : null;
            var balCollector = AttachCollectorToItsOwnAccountGraph(balBuilder, block);

            await RunPreExecutionSystemCallsAsync(block, stateReader, executor, balCollector);

            long cumulativeGasUsed = 0;
            var isAmsterdamPlus = config.IntrinsicGasRules.StateGasActive;
            var capacity = new Nethereum.EVM.Gas.BlockGasCapacity();
            var txResults = new List<TransactionExecutionResult>();
            var receipts = new List<Receipt>();
            var encodedReceipts = new List<byte[]>();
            var encodedTxs = new List<byte[]>();
            var combinedBloom = new byte[256];
            var allTouchedAddresses = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < block.Transactions.Count; i++)
            {
                var wtx = block.Transactions[i];
                var executionState = await OpenTransactionStateWithAccessRecorderAsync(block, stateReader, balCollector, i);

                var ctx = BuildTransactionContext(wtx, block, executionState, capacity);
                var result = await executor.ExecuteAsync(ctx);
                txResults.Add(result);

                cumulativeGasUsed += result.GasUsed;
                ChargeBlockGasCapacity(capacity, ctx, result, isAmsterdamPlus);

                var receipt = BuildReceiptAndFoldIntoBlockBloom(config, result, cumulativeGasUsed, combinedBloom);
                receipts.Add(receipt);

                encodedReceipts.Add(EncodeReceiptForTrie(encodingProvider, receipt, wtx.RlpEncoded));

                encodedTxs.Add(wtx.RlpEncoded);

                AccumulateTouchedAddresses(allTouchedAddresses, executionState);

                CommitStateAndRecordAccess(stateReader, executionState, balCollector, TransactionAccessIndex(i));
            }

            var requests = await ExecuteRequestSystemCallsAsync(block, stateReader, executor, balCollector, receipts);
            var requestsHash = requests.Commitment();

            byte[] withdrawalsRoot = null;
            if (block.Withdrawals != null)
            {
                var withdrawalES = await OpenWithdrawalStateAsync(stateReader, accounts);

                var encodedWithdrawals = CreditWithdrawalsTouchRecipientsAndEncode(
                    block, withdrawalES, balCollector, encodingProvider);

                CommitStateAndRecordAccess(stateReader, withdrawalES, balCollector, PostExecutionAccessIndex(block));

                withdrawalsRoot = ComputeWithdrawalsRoot(blockRootCalculator, encodedWithdrawals);
            }

            ForceComputePostStateRootWhenProducingCommitments(block);

            byte[] stateRoot = null;
            ExecutionStateService finalES = null;
            if (CanComputePostStateRoot(block, stateRootCalculator))
            {
                finalES = await MaterialiseFinalStateAsync(stateReader, accounts, allTouchedAddresses);
                stateRoot = stateRootCalculator.ComputeStateRoot(finalES);
            }

            var headerGasUsed = capacity.HeaderGasUsed;
            var blockAccessList = balBuilder?.Build();

            byte[] transactionsRoot = null, receiptsRoot = null, blockHash = null;
            Model.BlockHeader producedHeader = null;
            if (CanProduceBlockCommitments(block, blockRootCalculator))
            {
                transactionsRoot = blockRootCalculator.ComputeTransactionsRoot(encodedTxs);
                receiptsRoot = blockRootCalculator.ComputeReceiptsRoot(encodedReceipts);

                var header = producedHeader = BuildProducedHeader(
                    requestsHash, block, stateRoot, transactionsRoot, receiptsRoot, combinedBloom,
                    headerGasUsed, withdrawalsRoot, blockAccessList);

                blockHash = HashProducedHeader(encodingProvider, header);
            }

            return new BlockExecutionResult
            {
                TxResults = txResults,
                Receipts = receipts,
                CombinedBloom = combinedBloom,
                CumulativeGasUsed = cumulativeGasUsed,
                HeaderGasUsed = headerGasUsed,
                BlockExecutionGasUsed = capacity.ExecutionGasUsed,
                BlockStateGasUsed = capacity.StateGasUsed,
                StateRoot = stateRoot,
                TransactionsRoot = transactionsRoot,
                ReceiptsRoot = receiptsRoot,
                BlockHash = blockHash,
                ProducedHeader = producedHeader,
                BlockAccessList = blockAccessList,
                BlockAccessListGasLimitExceeded =
                    BlockAccessListSizeRule.ExceedsBlockGasLimit(blockAccessList, block.BlockGasLimit),
                FinalExecutionState = finalES,
                StateReader = stateReader
            };
        }


        private static async Task<byte[]> ExecuteSystemCallAsync(
            BlockWitnessData block,
            InMemoryStateReader stateReader,
            TransactionExecutor executor,
            BlockAccessListCollector balCollector,
            string contractAddress,
            byte[] callData,
            ulong blockAccessIndex)
        {
            var executionState = new ExecutionStateService(stateReader);
            await WitnessStateBuilder.LoadAllAccountsAndStorageAsync(executionState, stateReader, block.Accounts);

            SystemCallExecution.Prepare(executionState, balCollector, contractAddress, blockAccessIndex);

            byte[] returnData = null;
            var contractCode = await executionState.GetCodeAsync(contractAddress);
            if (SystemCallExecution.IsAbsent(contractCode))
            {
                SystemCallExecution.RefuseBlockOnAbsentRequestPredeploy(contractAddress);
            }
            else
            {
                var simulator = executor.GetSimulator();
                var program = await SystemCallExecution.BuildProgramAsync(
                    block, executionState, simulator.Config, contractAddress, callData, contractCode);
                var snapshotId = SystemCallExecution.TakeCallSnapshot(executionState);
                await simulator.ExecuteWithCallStackAsync(program, traceEnabled: false);
                SystemCallExecution.SettleCallState(executionState, program, snapshotId);
                SystemCallExecution.RefuseBlockOnFatalCallFailure(contractAddress, program);
                returnData = program.ProgramResult.Result;
            }

            SystemCallExecution.Commit(stateReader, executionState, balCollector, blockAccessIndex);

            return returnData ?? new byte[0];
        }

        private static async Task<BlockExecutionRequests> ExecuteRequestSystemCallsAsync(
            BlockWitnessData block,
            InMemoryStateReader stateReader,
            TransactionExecutor executor,
            BlockAccessListCollector balCollector,
            List<Receipt> receipts)
        {
            var fork = block.Features?.Fork ?? HardforkName.Unspecified;
            var postExecutionIndex = PostExecutionAccessIndex(block);
            var requests = BlockExecutionRequests.OpenedWithDeposits(fork, LogsOf(receipts));
            if (block.SkipsRequestSystemCalls) return requests;

            foreach (var contract in SystemCallContracts.RequestContractsFor(fork))
            {
                requests.AddFrom(contract, await ExecuteSystemCallAsync(block, stateReader, executor, balCollector,
                    contract, new byte[0], postExecutionIndex));
            }

            return requests;
        }

        private static async Task RunPreExecutionSystemCallsAsync(
            BlockWitnessData block,
            InMemoryStateReader stateReader,
            TransactionExecutor executor,
            BlockAccessListCollector balCollector)
        {
            if (CarriesParentBeaconBlockRoot(block))
            {
                await ExecuteSystemCallAsync(block, stateReader, executor, balCollector,
                    SystemCallContracts.BeaconRoots,
                    block.ParentBeaconBlockRoot.Length >= 32
                        ? block.ParentBeaconBlockRoot
                        : PadTo32(block.ParentBeaconBlockRoot),
                    PreExecutionAccessIndex);
            }

            if (block.Features?.Fork >= HardforkName.Prague
                && block.ParentHash != null && block.ParentHash.Length >= 32)
            {
                await ExecuteSystemCallAsync(block, stateReader, executor, balCollector,
                    SystemCallContracts.HistoryStorage, block.ParentHash, PreExecutionAccessIndex);
            }
        }

        private static async Task<ExecutionStateService> OpenTransactionStateWithAccessRecorderAsync(
            BlockWitnessData block,
            InMemoryStateReader stateReader,
            BlockAccessListCollector balCollector,
            int transactionIndex)
        {
            var executionState = new ExecutionStateService(stateReader);
            await WitnessStateBuilder.LoadAllAccountsAndStorageAsync(executionState, stateReader, block.Accounts);
            if (balCollector != null)
            {
                balCollector.BeginUnit(TransactionAccessIndex(transactionIndex));
                executionState.AccessRecorder = balCollector;
            }
            return executionState;
        }

        private static async Task<ExecutionStateService> OpenWithdrawalStateAsync(
            InMemoryStateReader stateReader,
            Dictionary<string, AccountState> accounts)
        {
            var withdrawalES = new ExecutionStateService(stateReader);
            foreach (var addr in accounts.Keys)
                await withdrawalES.LoadBalanceNonceAndCodeFromStorageAsync(addr);
            return withdrawalES;
        }

        private static async Task<ExecutionStateService> MaterialiseFinalStateAsync(
            InMemoryStateReader stateReader,
            Dictionary<string, AccountState> accounts,
            HashSet<string> allTouchedAddresses)
        {
            var finalES = new ExecutionStateService(stateReader);
            var allAddresses = new HashSet<string>(accounts.Keys, System.StringComparer.OrdinalIgnoreCase);
            foreach (var addr in allTouchedAddresses)
                allAddresses.Add(addr);

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
            return finalES;
        }
#endif
    }
}
