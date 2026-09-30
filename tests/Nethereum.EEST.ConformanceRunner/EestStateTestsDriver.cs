using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Gas;
using Nethereum.EVM.Gas.Intrinsic;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using EvmAccountState = Nethereum.EVM.BlockchainState.AccountState;
using IncrementalStateRootCalculator = Nethereum.CoreChain.IncrementalStateRootCalculator;

namespace Nethereum.EEST.ConformanceRunner
{
    public sealed class EestStateTestsDriver : IConformanceDriver
    {
        public static readonly EestStateTestsDriver Instance = new();

        public string SuiteId => "eest-state-tests";
        public string SuiteName => "Nethereum EEST state_tests - single-transaction state transition";

        private static readonly Sha3Keccack Keccak = Sha3Keccack.Current;

        public Task<ConformanceCaseResult> RunAsync(
            StateTestLoader.StateTest test, string fork, int dataIndex, int gasIndex, int valueIndex) =>
            RunAsyncCore(test, fork, dataIndex, gasIndex, valueIndex);

        private static async Task<ConformanceCaseResult> RunAsyncCore(
            StateTestLoader.StateTest test, string fork, int dataIndex, int gasIndex, int valueIndex)
        {
            HardforkName hardfork;
            try
            {
                hardfork = HardforkNames.Parse(fork);
            }
            catch (ArgumentException ex)
            {
                return ConformanceCaseResult.Fail("malformedFork",
                    $"post carries an unrecognised fork name '{fork}': {ex.Message}");
            }

            if (!test.Post.TryGetValue(fork, out var postList) || postList == null)
                return ConformanceCaseResult.Fail("missingPostFork", $"post has no entry for fork '{fork}'");

            StateTestLoader.PostEntry? expected = null;
            foreach (var candidate in postList)
            {
                if (candidate.Indexes.Data == dataIndex && candidate.Indexes.Gas == gasIndex && candidate.Indexes.Value == valueIndex)
                {
                    expected = candidate;
                    break;
                }
            }

            if (expected == null)
                return ConformanceCaseResult.Fail("missingPostEntry",
                    $"post['{fork}'] has no entry for indexes data={dataIndex} gas={gasIndex} value={valueIndex}");

            HardforkConfig config;
            try
            {
                config = ResolveConfig(hardfork, test);
            }
            catch (Exception ex)
            {
                return ConformanceCaseResult.Fail("malformedConfig", $"{ex.GetType().Name}: {ex.Message}");
            }

            var executionState = SetupPreState(test);

            TransactionExecutionContext ctx;
            try
            {
                ctx = BuildExecutionContext(test, expected, executionState);
            }
            catch (Exception ex)
            {
                return ClassifyPreDispatchFailure(expected, ex);
            }

            TransactionExecutionResult result;
            try
            {
                result = await new TransactionExecutor(config).ExecuteAsync(ctx).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return ClassifyPreDispatchFailure(expected, ex);
            }

            if (result.IsValidationError)
            {
                if (!string.IsNullOrEmpty(expected.ExpectException))
                {
                    return ExpectedRejectionMatcher.Satisfies(expected.ExpectException, result.ErrorCode, out var detail)
                        ? ConformanceCaseResult.Ok()
                        : ConformanceCaseResult.Fail("rejectionReasonMismatch", detail);
                }

                return ConformanceCaseResult.Fail("validationError", $"{result.ErrorCode}: {result.Error}");
            }

            if (!string.IsNullOrEmpty(expected.ExpectException))
            {
                return ConformanceCaseResult.Fail("rejectionNotEnforced",
                    $"expected exception '{expected.ExpectException}' but the transaction executed (success={result.Success})");
            }

            var (postStateStore, postAccountCount) = await BuildPostStateStoreAsync(executionState).ConfigureAwait(false);
            var computedRoot = await new IncrementalStateRootCalculator(postStateStore)
                .ComputeFullStateRootAsync().ConfigureAwait(false);

            if (string.IsNullOrEmpty(expected.Hash))
                return ConformanceCaseResult.Fail("malformedFixture", "post entry carries no expected 'hash'");

            var expectedRoot = expected.Hash.HexToByteArray();
            if (!computedRoot.SequenceEqual(expectedRoot))
            {
                return ConformanceCaseResult.Fail("stateRoot",
                    $"expected=0x{expected.Hash.Replace("0x", "")} actual=0x{computedRoot.ToHex(false)} " +
                    $"({postAccountCount} post accounts)");
            }

            if (!string.IsNullOrEmpty(expected.Logs))
            {
                var computedLogsHash = ComputeLogsHash(result.Logs);
                var expectedLogsHash = expected.Logs.HexToByteArray();
                if (!computedLogsHash.SequenceEqual(expectedLogsHash))
                {
                    return ConformanceCaseResult.Fail("logsHash",
                        $"expected=0x{expected.Logs.Replace("0x", "")} actual=0x{computedLogsHash.ToHex(false)}");
                }
            }

            return ConformanceCaseResult.Ok();
        }

        private static ConformanceCaseResult ClassifyPreDispatchFailure(StateTestLoader.PostEntry expected, Exception ex)
        {
            if (!string.IsNullOrEmpty(expected.ExpectException))
            {
                return ExpectedRejectionMatcher.Satisfies(expected.ExpectException, ex, out var detail)
                    ? ConformanceCaseResult.Ok()
                    : ConformanceCaseResult.Fail("rejectionReasonMismatch", detail);
            }

            return ConformanceCaseResult.Fail("exception", $"{ex.GetType().Name}: {ex.Message}");
        }

        private static HardforkConfig ResolveConfig(HardforkName fork, StateTestLoader.StateTest test)
        {
            var baseConfig = EestBlockchainTestsRlpDriver.FixtureRegistry.Get(fork);

            var fraction = TryReadFixtureBlobFraction(fork, test);
            if (fraction == null) return baseConfig;

            IBlobGasRule fixtureBlobRule;
            if (fork >= HardforkName.Osaka) fixtureBlobRule = new Eip7892BlobGasRule(fraction.Value);
            else if (fork >= HardforkName.Prague) fixtureBlobRule = new Eip7691BlobGasRule(fraction.Value);
            else if (fork >= HardforkName.Cancun) fixtureBlobRule = new Eip4844BlobGasRule(fraction.Value);
            else return baseConfig;

            var clone = baseConfig.Clone();
            clone.IntrinsicGasRules = baseConfig.IntrinsicGasRules.WithBlob(fixtureBlobRule);
            return clone;
        }

        private static int? TryReadFixtureBlobFraction(HardforkName fork, StateTestLoader.StateTest test)
        {
            var schedule = test.Config?.BlobSchedule;
            if (schedule == null || schedule.Count == 0) return null;

            string key;
            if (fork >= HardforkName.Amsterdam) key = "Amsterdam";
            else if (fork >= HardforkName.Osaka) key = "Osaka";
            else if (fork >= HardforkName.Prague) key = "Prague";
            else if (fork >= HardforkName.Cancun) key = "Cancun";
            else return null;

            if (!schedule.TryGetValue(key, out var entry) || string.IsNullOrEmpty(entry?.BaseFeeUpdateFraction)) return null;

            var big = entry.BaseFeeUpdateFraction.HexToBigInteger(false);
            if (big <= 0 || big > int.MaxValue) return null;
            return (int)big;
        }

        private static TransactionExecutionContext BuildExecutionContext(
            StateTestLoader.StateTest test, StateTestLoader.PostEntry expected, ExecutionStateService executionState)
        {
            var env = test.Env;
            var tx = test.Transaction;

            var dataIndex = expected.Indexes.Data;
            var gasIndex = expected.Indexes.Gas;
            var valueIndex = expected.Indexes.Value;

            var data = tx.Data != null && dataIndex < tx.Data.Count ? tx.Data[dataIndex] : "0x";
            var gasLimitStr = tx.GasLimit != null && gasIndex < tx.GasLimit.Count ? tx.GasLimit[gasIndex] : "0x0";
            var valueStr = tx.Value != null && valueIndex < tx.Value.Count ? tx.Value[valueIndex] : "0x0";

            var dataBytes = string.IsNullOrEmpty(data) || data == "0x" ? Array.Empty<byte>() : data.HexToByteArray();
            var gasLimit = ParseScalar(LegacyTransactionField.GasLimit, gasLimitStr);
            var value = ParseScalar(LegacyTransactionField.Value, valueStr);
            var baseFee = string.IsNullOrEmpty(env.CurrentBaseFee) ? BigInteger.Zero : env.CurrentBaseFee.HexToBigInteger(false);

            BigInteger gasPrice;
            BigInteger maxFeePerGas = BigInteger.Zero;
            BigInteger maxPriorityFeePerGas = BigInteger.Zero;
            bool isEip1559 = !string.IsNullOrEmpty(tx.MaxFeePerGas);

            if (isEip1559)
            {
                maxFeePerGas = tx.MaxFeePerGas!.HexToBigInteger(false);
                maxPriorityFeePerGas = string.IsNullOrEmpty(tx.MaxPriorityFeePerGas) ? BigInteger.Zero : tx.MaxPriorityFeePerGas.HexToBigInteger(false);
                gasPrice = maxFeePerGas;
            }
            else
            {
                gasPrice = string.IsNullOrEmpty(tx.GasPrice) ? BigInteger.Zero : tx.GasPrice.HexToBigInteger(false);
            }

            var sender = tx.Sender ?? SenderFromSecretKey(tx.SecretKey);
            var toAddress = string.IsNullOrEmpty(tx.To) ? null : tx.To;
            var isContractCreation = toAddress == null;

            var blobVersionedHashes = tx.BlobVersionedHashes;
            var isType3Transaction = !string.IsNullOrEmpty(tx.MaxFeePerBlobGas);

            var accessList = tx.AccessLists != null && dataIndex < tx.AccessLists.Count ? tx.AccessLists[dataIndex] : null;
            List<AccessListEntry>? accessListEntries = accessList?
                .Select(a => new AccessListEntry { Address = a.Address, StorageKeys = a.StorageKeys ?? new List<string>() })
                .ToList();

            var blockNumber = string.IsNullOrEmpty(env.CurrentNumber) ? 1L : (long)env.CurrentNumber.HexToBigInteger(false);
            var timestamp = string.IsNullOrEmpty(env.CurrentTimestamp) ? 0L : (long)env.CurrentTimestamp.HexToBigInteger(false);
            var blockGasLimit = string.IsNullOrEmpty(env.CurrentGasLimit) ? 0L : (long)env.CurrentGasLimit.HexToBigInteger(false);

            var difficulty = EvmUInt256.Zero;
            if (!string.IsNullOrEmpty(env.CurrentRandom))
                difficulty = EvmUInt256BigIntegerExtensions.FromBigInteger(env.CurrentRandom.HexToBigInteger(false));
            else if (!string.IsNullOrEmpty(env.CurrentDifficulty))
                difficulty = EvmUInt256BigIntegerExtensions.FromBigInteger(env.CurrentDifficulty.HexToBigInteger(false));

            var excessBlobGas = string.IsNullOrEmpty(env.CurrentExcessBlobGas) ? 0UL : (ulong)env.CurrentExcessBlobGas.HexToBigInteger(false);

            var slotNumber = EvmUInt256.Zero;
            if (!string.IsNullOrEmpty(env.SlotNumber))
                slotNumber = EvmUInt256BigIntegerExtensions.FromBigInteger(env.SlotNumber.HexToBigInteger(false));

            var nonce = string.IsNullOrEmpty(tx.Nonce) ? 0UL : (ulong)tx.Nonce.HexToBigInteger(false);

            // AMS-STATE-02: EvmUInt256 declares an implicit conversion FROM HexBigInteger (a
            // reference type) that maps null to Zero - so a bare `cond ? null : EvmUInt256expr`
            // ternary infers EvmUInt256 (non-nullable, via that HexBigInteger path) for BOTH
            // branches instead of EvmUInt256?, silently turning "no declared chain id" into
            // "declared chain id 0" and firing ChainIdValidationRule on every pre-EIP-155 fixture.
            // The explicit (EvmUInt256?) cast on the null branch is REQUIRED, not decorative -
            // GeneralStateTestRunner.BuildExecutionContext carries the same cast for the same reason.
            EvmUInt256? declaredChainId = string.IsNullOrEmpty(tx.ChainId)
                ? (EvmUInt256?)null
                : EvmUInt256BigIntegerExtensions.FromBigInteger(tx.ChainId.HexToBigInteger(false));

            List<Authorisation7702Signed>? authList = null;
            if (tx.AuthorizationList != null)
            {
                authList = tx.AuthorizationList.Select(a => new Authorisation7702Signed(
                    EvmUInt256BigIntegerExtensions.FromBigInteger(a.ChainId.HexToBigInteger(false)),
                    a.Address,
                    EvmUInt256BigIntegerExtensions.FromBigInteger(a.Nonce.HexToBigInteger(false)),
                    a.R.HexToByteArray(),
                    a.S.HexToByteArray(),
                    a.V.HexToByteArray()
                )).ToList();
            }

            var chainId = test.Config?.ChainId is { Length: > 0 } configChainId
                ? EvmUInt256BigIntegerExtensions.FromBigInteger(configChainId.HexToBigInteger(false))
                : EvmUInt256.One;

            return new TransactionExecutionContext
            {
                Sender = sender,
                To = toAddress,
                Data = dataBytes,
                GasLimit = (long)gasLimit,
                Value = EvmUInt256BigIntegerExtensions.FromBigInteger(value),
                GasPrice = EvmUInt256BigIntegerExtensions.FromBigInteger(gasPrice),
                MaxFeePerGas = EvmUInt256BigIntegerExtensions.FromBigInteger(maxFeePerGas),
                MaxPriorityFeePerGas = EvmUInt256BigIntegerExtensions.FromBigInteger(maxPriorityFeePerGas),
                Nonce = nonce,
                IsEip1559 = isEip1559,
                IsContractCreation = isContractCreation,
                IsType3Transaction = isType3Transaction,
                TransactionType =
                    authList != null ? Nethereum.Model.TransactionType.EIP7702 :
                    isType3Transaction ? Nethereum.Model.TransactionType.Blob :
                    isEip1559 ? Nethereum.Model.TransactionType.EIP1559 :
                    accessListEntries != null ? Nethereum.Model.TransactionType.LegacyEIP2930 :
                    Nethereum.Model.TransactionType.LegacyTransaction,
                BlobVersionedHashes = blobVersionedHashes,
                MaxFeePerBlobGas = isType3Transaction ? EvmUInt256BigIntegerExtensions.FromBigInteger(tx.MaxFeePerBlobGas!.HexToBigInteger(false)) : EvmUInt256.Zero,
                AccessList = accessListEntries,
                AuthorisationList = authList,

                BlockNumber = blockNumber,
                Timestamp = timestamp,
                Coinbase = env.CurrentCoinbase,
                BaseFee = EvmUInt256BigIntegerExtensions.FromBigInteger(baseFee),
                Difficulty = difficulty,
                BlockGasLimit = blockGasLimit,
                ExcessBlobGas = excessBlobGas,
                SlotNumber = slotNumber,

                ChainId = chainId,
                DeclaredChainId = declaredChainId,

                ExecutionState = executionState,
                TraceEnabled = false
            };
        }

        private static BigInteger ParseScalar(LegacyTransactionField field, string text)
        {
            if (string.IsNullOrEmpty(text)) return BigInteger.Zero;

            const string bigIntMarker = "0x:bigint ";
            var digits = text.StartsWith(bigIntMarker) ? text.Substring(bigIntMarker.Length) : text;
            if (string.IsNullOrEmpty(digits)) return BigInteger.Zero;

            var bytes = digits.HexToByteArray();
            if (bytes.Length > 32)
                throw new ScalarWiderThanItsFieldException(field, bytes.Length);

            return digits.HexToBigInteger(false);
        }

        private static string SenderFromSecretKey(string? secretKey)
        {
            if (string.IsNullOrEmpty(secretKey))
                return "0x0000000000000000000000000000000000000000";

            var key = new Nethereum.Signer.EthECKey(secretKey);
            return key.GetPublicAddress();
        }

        private static ExecutionStateService SetupPreState(StateTestLoader.StateTest test)
        {
            var executionState = new ExecutionStateService(new StateTestNodeDataReader(BuildPreStateReader(test)));

            foreach (var preAccount in test.Pre)
            {
                var address = preAccount.Key;
                var account = preAccount.Value;

                var accountState = executionState.CreateOrGetAccountExecutionState(address);
                accountState.WasInPreState = true;

                accountState.Code = string.IsNullOrEmpty(account.Code) || account.Code == "0x"
                    ? Array.Empty<byte>()
                    : account.Code.HexToByteArray();

                accountState.Balance.SetInitialChainBalance(
                    EvmUInt256BigIntegerExtensions.FromBigInteger(string.IsNullOrEmpty(account.Balance) ? BigInteger.Zero : account.Balance.HexToBigInteger(false)));

                accountState.Nonce = string.IsNullOrEmpty(account.Nonce)
                    ? (ulong?)0
                    : (ulong)account.Nonce.HexToBigInteger(false);

                if (account.Storage != null)
                {
                    foreach (var storage in account.Storage)
                    {
                        var key = EvmUInt256BigIntegerExtensions.FromBigInteger(storage.Key.HexToBigInteger(false));
                        accountState.SetPreStateStorage(key, storage.Value.HexToByteArray());
                    }
                }
            }

            return executionState;
        }

        private static InMemoryStateReader BuildPreStateReader(StateTestLoader.StateTest test)
        {
            var accounts = new Dictionary<string, EvmAccountState>();

            foreach (var preAccount in test.Pre)
            {
                var account = preAccount.Value;
                accounts[preAccount.Key.ToLowerInvariant()] = new EvmAccountState
                {
                    Balance = EvmUInt256BigIntegerExtensions.FromBigInteger(string.IsNullOrEmpty(account.Balance) ? BigInteger.Zero : account.Balance.HexToBigInteger(false)),
                    Nonce = EvmUInt256BigIntegerExtensions.FromBigInteger(string.IsNullOrEmpty(account.Nonce) ? BigInteger.Zero : account.Nonce.HexToBigInteger(false)),
                    Code = string.IsNullOrEmpty(account.Code) || account.Code == "0x" ? Array.Empty<byte>() : account.Code.HexToByteArray(),
                };
            }

            return new InMemoryStateReader(accounts);
        }

        /// <summary>
        /// EIP-161/state-test convention shared with GeneralStateTestRunner.ExtractPostState: an
        /// account materialised only because something READ it (never in Pre, never touched, never a
        /// new contract, all-zero fields) is a phantom the interpreter never actually wrote to state
        /// and must not appear in the post-state trie the root is computed over. Survivors are
        /// written into a real <see cref="InMemoryStateStore"/> so the root is computed by the exact
        /// same <see cref="IncrementalStateRootCalculator"/> production block execution uses
        /// (<c>Nethereum.CoreChain.BlockExecutor</c>), not a second, test-only trie builder - state_tests and
        /// consume-rlp end up sharing one root-computation path.
        /// </summary>
        private static async Task<(InMemoryStateStore Store, int AccountCount)> BuildPostStateStoreAsync(
            ExecutionStateService executionState)
        {
            var store = new InMemoryStateStore();
            var accountCount = 0;

            foreach (var kvp in executionState.AccountsState)
            {
                var address = kvp.Key;
                var acc = kvp.Value;

                if (acc.IsRemoved) continue;

                var isPhantom =
                    !acc.WasInPreState &&
                    !acc.IsTouched &&
                    !acc.IsNewContract &&
                    acc.Storage.Count == 0 &&
                    acc.Balance.GetTotalBalance().IsZero &&
                    (acc.Code == null || acc.Code.Length == 0) &&
                    (!acc.Nonce.HasValue || acc.Nonce.Value.IsZero);
                if (isPhantom) continue;

                var code = acc.Code ?? Array.Empty<byte>();
                await store.SaveAccountAsync(address, new Account
                {
                    Nonce = acc.Nonce ?? (EvmUInt256)0L,
                    Balance = acc.Balance.GetTotalBalance(),
                    CodeHash = code.Length > 0 ? Keccak.CalculateHash(code) : DefaultValues.EMPTY_DATA_HASH
                }).ConfigureAwait(false);

                foreach (var storageKvp in acc.Storage)
                    if (storageKvp.Value != null)
                        await store.SaveStorageAsync(address, storageKvp.Key.ToBigInteger(), storageKvp.Value)
                            .ConfigureAwait(false);

                accountCount++;
            }

            return (store, accountCount);
        }

        private static byte[] ComputeLogsHash(List<FilterLog> logs)
        {
            var encoded = new byte[logs.Count][];
            for (int i = 0; i < logs.Count; i++)
            {
                var log = logs[i];
                var topics = new List<byte[]>();
                if (log.Topics != null)
                    foreach (var topic in log.Topics)
                        topics.Add(((string)topic).HexToByteArray());

                var modelLog = new Log
                {
                    Address = log.Address,
                    Data = string.IsNullOrEmpty(log.Data) ? Array.Empty<byte>() : log.Data.HexToByteArray(),
                    Topics = topics
                };
                encoded[i] = LogEncoder.Current.Encode(modelLog);
            }

            return Keccak.CalculateHash(RLP.RLP.EncodeList(encoded));
        }

        private sealed class StateTestNodeDataReader : IStateReader
        {
            private readonly InMemoryStateReader _reader;

            public StateTestNodeDataReader(InMemoryStateReader reader) => _reader = reader;

            public Task<EvmUInt256> GetBalanceAsync(byte[] address) => _reader.GetBalanceAsync(address);
            public Task<EvmUInt256> GetBalanceAsync(string address) => _reader.GetBalanceAsync(address);
            public Task<byte[]> GetCodeAsync(byte[] address) => _reader.GetCodeAsync(address);
            public Task<byte[]> GetCodeAsync(string address) => _reader.GetCodeAsync(address);
            public Task<byte[]> GetBlockHashAsync(long blockNumber) => Task.FromResult(Keccak.CalculateHashAsBytes(blockNumber.ToString()));
            public Task<byte[]> GetStorageAtAsync(byte[] address, EvmUInt256 position) => _reader.GetStorageAtAsync(address, position);
            public Task<byte[]> GetStorageAtAsync(string address, EvmUInt256 position) => _reader.GetStorageAtAsync(address, position);
            public Task<EvmUInt256> GetTransactionCountAsync(byte[] address) => _reader.GetTransactionCountAsync(address);
            public Task<EvmUInt256> GetTransactionCountAsync(string address) => _reader.GetTransactionCountAsync(address);
            public Task<bool> AccountExistsAsync(string address) => _reader.AccountExistsAsync(address);
        }
    }
}
