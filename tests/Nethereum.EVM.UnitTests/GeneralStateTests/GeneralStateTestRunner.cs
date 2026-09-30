using Nethereum.CoreChain.IntegrationTests.BlockchainTests;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Execution;
using Nethereum.EVM.Gas;
using Nethereum.EVM.Gas.Intrinsic;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.EVM.UnitTests.GeneralStateTests
{
    public class GeneralStateTestRunner
    {
        private readonly ITestOutputHelper _output;
        private readonly string _targetHardfork;
        private readonly HardforkName _targetFork;
        private readonly TimeSpan _testTimeout;
        private HardforkConfig _cachedConfig;




        private const int G_CODEDEPOSIT = 200;

        private const int MAX_BLOBS_PER_BLOCK_CANCUN = 6;
        private const int MAX_BLOBS_PER_BLOCK_PRAGUE = 9;
        private const byte VERSIONED_HASH_VERSION_KZG = 0x01;

        public GeneralStateTestRunner(ITestOutputHelper output = null, string targetHardfork = "Prague", TimeSpan? testTimeout = null)
        {
            _output = output;
            _targetHardfork = targetHardfork;
            _targetFork = HardforkNames.Parse(targetHardfork);
            _testTimeout = testTimeout ?? TimeSpan.FromSeconds(30);
        }

        private void EnsureCachedConfig()
        {
            if (_cachedConfig != null) return;

            var config = Nethereum.EVM.Precompiles.DefaultMainnetHardforkRegistry.Instance.Get(_targetFork);

            if (_targetFork >= HardforkName.Cancun)
            {
                config = config.WithKzgBackend();
            }

            if (_targetFork >= HardforkName.Prague)
            {
                config = config.WithBlsBackend(new Nethereum.Signer.Bls.Herumi.Bls12381Operations());
            }

            _cachedConfig = config;
        }

        private HardforkConfig ResolveConfigForFixture(GeneralStateTest test)
        {
            EnsureCachedConfig();

            var fraction = TryReadFixtureBlobFraction(test);
            if (fraction == null) return _cachedConfig;

            IBlobGasRule fixtureBlobRule;
            if (_targetFork >= HardforkName.Osaka)
                fixtureBlobRule = new Eip7892BlobGasRule(fraction.Value);
            else if (_targetFork >= HardforkName.Prague)
                fixtureBlobRule = new Eip7691BlobGasRule(fraction.Value);
            else if (_targetFork >= HardforkName.Cancun)
                fixtureBlobRule = new Eip4844BlobGasRule(fraction.Value);
            else
                return _cachedConfig;

            var clone = _cachedConfig.Clone();
            clone.IntrinsicGasRules = _cachedConfig.IntrinsicGasRules.WithBlob(fixtureBlobRule);
            return clone;
        }

        private int? TryReadFixtureBlobFraction(GeneralStateTest test)
        {
            var schedule = test?.Config?.BlobSchedule;
            if (schedule == null || schedule.Count == 0) return null;

            string key;
            if (_targetFork >= HardforkName.Amsterdam) key = "Amsterdam";
            else if (_targetFork >= HardforkName.Osaka) key = "Osaka";
            else if (_targetFork >= HardforkName.Prague) key = "Prague";
            else if (_targetFork >= HardforkName.Cancun) key = "Cancun";
            else return null;

            if (!schedule.TryGetValue(key, out var entry) || entry == null) return null;
            if (string.IsNullOrEmpty(entry.BaseFeeUpdateFraction)) return null;

            var big = entry.BaseFeeUpdateFraction.HexToBigInteger(false);
            if (big <= 0 || big > int.MaxValue) return null;
            return (int)big;
        }

        public async Task<TestResult> RunTestAsync(string testFilePath, int? specificDataIndex = null)
        {
            return await RunTestInternalAsync(testFilePath, captureTraces: false, specificDataIndex: specificDataIndex);
        }

        public async Task<TestResult> RunTestWithTraceAsync(string testFilePath, int? specificDataIndex = null)
        {
            return await RunTestInternalAsync(testFilePath, captureTraces: true, specificDataIndex: specificDataIndex);
        }

        public async Task<TestResult> RunTestWithExecutorAsync(string testFilePath, int? specificDataIndex = null, bool captureTraces = false)
        {
            return await RunTestInternalWithExecutorAsync(testFilePath, captureTraces: captureTraces, specificDataIndex: specificDataIndex);
        }

        private async Task<TestResult> RunTestInternalWithExecutorAsync(string testFilePath, bool captureTraces, int? specificDataIndex = null)
        {
            var json = File.ReadAllText(testFilePath);
            var tests = JsonConvert.DeserializeObject<Dictionary<string, GeneralStateTest>>(json);
            var results = new List<SingleTestResult>();

            foreach (var testEntry in tests)
            {
                var testName = testEntry.Key;
                var test = testEntry.Value;

                if (!test.Post.ContainsKey(_targetHardfork))
                {
                    results.Add(new SingleTestResult
                    {
                        TestName = testName,
                        Skipped = true,
                        SkipReason = $"Hardfork {_targetHardfork} not found in post"
                    });
                    continue;
                }

                foreach (var postResult in test.Post[_targetHardfork])
                {
                    if (specificDataIndex.HasValue && postResult.Indexes.Data != specificDataIndex.Value)
                        continue;
                    using var cts = new CancellationTokenSource(_testTimeout);
                    var testTask = RunSingleTestWithExecutorAsync(testName, test, postResult, captureTraces);
                    var completedTask = await Task.WhenAny(testTask, Task.Delay(_testTimeout, cts.Token));

                    if (completedTask != testTask)
                    {
                        results.Add(new SingleTestResult
                        {
                            TestName = testName,
                            DataIndex = postResult.Indexes.Data,
                            GasIndex = postResult.Indexes.Gas,
                            ValueIndex = postResult.Indexes.Value,
                            Passed = false,
                            Message = $"Test timed out after {_testTimeout.TotalSeconds} seconds"
                        });
                    }
                    else
                    {
                        cts.Cancel();
                        var result = await testTask;
                        results.Add(result);
                    }
                }
            }

            return new TestResult
            {
                FilePath = testFilePath,
                Results = results
            };
        }

        public async Task<TraceValidationResult> RunAndValidateAsync(string testFilePath, TraceValidationOptions options = null)
        {
            var gethRunner = new GethEvmRunner();
            var gethResult = await gethRunner.RunStateTestAsync(testFilePath);

            if (!gethResult.Success || gethResult.Steps == null || gethResult.Steps.Count == 0)
            {
                return new TraceValidationResult
                {
                    IsValid = false,
                    FirstMismatch = new StepMismatch
                    {
                        StepIndex = 0,
                        Field = "GETH_RUN_FAILED",
                        GethValue = gethResult.Error ?? "No steps captured",
                        NethValue = "N/A"
                    }
                };
            }

            var nethResult = await RunTestWithTraceAsync(testFilePath);
            var singleResult = nethResult.Results.FirstOrDefault(r => !r.Skipped);

            if (singleResult == null || singleResult.Traces == null || singleResult.Traces.Count == 0)
            {
                return new TraceValidationResult
                {
                    IsValid = false,
                    TotalGethSteps = gethResult.Steps.Count,
                    FirstMismatch = new StepMismatch
                    {
                        StepIndex = 0,
                        Field = "NETH_RUN_FAILED",
                        GethValue = $"{gethResult.Steps.Count} steps",
                        NethValue = singleResult?.Message ?? "No traces captured"
                    }
                };
            }

            var comparer = new TraceComparer();
            var nethSteps = comparer.NormalizeNethTrace(singleResult.Traces);

            var validator = new TraceValidator();
            return validator.Validate(gethResult.Steps, nethSteps, options);
        }

        public async Task<(GethEvmResult geth, TestResult neth, TraceValidationResult validation)> RunAndValidateFullAsync(
            string testFilePath,
            TraceValidationOptions options = null)
        {
            var gethRunner = new GethEvmRunner();
            var gethResult = await gethRunner.RunStateTestAsync(testFilePath);

            var nethResult = await RunTestWithTraceAsync(testFilePath);
            var singleResult = nethResult.Results.FirstOrDefault(r => !r.Skipped);

            TraceValidationResult validation;

            if (!gethResult.Success || gethResult.Steps == null || gethResult.Steps.Count == 0)
            {
                validation = new TraceValidationResult
                {
                    IsValid = false,
                    FirstMismatch = new StepMismatch
                    {
                        StepIndex = 0,
                        Field = "GETH_RUN_FAILED",
                        GethValue = gethResult.Error ?? "No steps captured",
                        NethValue = "N/A"
                    }
                };
            }
            else if (singleResult == null || singleResult.Traces == null || singleResult.Traces.Count == 0)
            {
                validation = new TraceValidationResult
                {
                    IsValid = false,
                    TotalGethSteps = gethResult.Steps.Count,
                    FirstMismatch = new StepMismatch
                    {
                        StepIndex = 0,
                        Field = "NETH_RUN_FAILED",
                        GethValue = $"{gethResult.Steps.Count} steps",
                        NethValue = singleResult?.Message ?? "No traces captured"
                    }
                };
            }
            else
            {
                var comparer = new TraceComparer();
                var nethSteps = comparer.NormalizeNethTrace(singleResult.Traces);
                var validator = new TraceValidator();
                validation = validator.Validate(gethResult.Steps, nethSteps, options);
            }

            return (gethResult, nethResult, validation);
        }

        private async Task<TestResult> RunTestInternalAsync(string testFilePath, bool captureTraces, int? specificDataIndex = null)
        {
            var json = File.ReadAllText(testFilePath);
            var tests = JsonConvert.DeserializeObject<Dictionary<string, GeneralStateTest>>(json);
            var results = new List<SingleTestResult>();

            foreach (var testEntry in tests)
            {
                var testName = testEntry.Key;
                var test = testEntry.Value;

                if (!test.Post.ContainsKey(_targetHardfork))
                {
                    results.Add(new SingleTestResult
                    {
                        TestName = testName,
                        Skipped = true,
                        SkipReason = $"Hardfork {_targetHardfork} not found in post"
                    });
                    continue;
                }

                foreach (var postResult in test.Post[_targetHardfork])
                {
                    if (specificDataIndex.HasValue && postResult.Indexes.Data != specificDataIndex.Value)
                        continue;
                    using var cts = new CancellationTokenSource(_testTimeout);
                    var testTask = RunSingleTestAsync(testName, test, postResult, captureTraces);
                    var completedTask = await Task.WhenAny(testTask, Task.Delay(_testTimeout, cts.Token));

                    if (completedTask != testTask)
                    {
                        results.Add(new SingleTestResult
                        {
                            TestName = testName,
                            DataIndex = postResult.Indexes.Data,
                            GasIndex = postResult.Indexes.Gas,
                            ValueIndex = postResult.Indexes.Value,
                            Passed = false,
                            Message = $"Test timed out after {_testTimeout.TotalSeconds} seconds"
                        });
                    }
                    else
                    {
                        cts.Cancel();
                        var result = await testTask;
                        results.Add(result);
                    }
                }
            }

            return new TestResult
            {
                FilePath = testFilePath,
                Results = results
            };
        }

        private async Task<SingleTestResult> RunSingleTestAsync(string testName, GeneralStateTest test, PostResult expected, bool captureTraces = false)
        {
            var result = new SingleTestResult
            {
                TestName = testName,
                DataIndex = expected.Indexes.Data,
                GasIndex = expected.Indexes.Gas,
                ValueIndex = expected.Indexes.Value
            };
            Program programForTraces = null;

            EnsureCachedConfig();

            try
            {
                var env = test.Env;
                var tx = test.Transaction;

                var dataIndex = expected.Indexes.Data;
                var gasIndex = expected.Indexes.Gas;
                var valueIndex = expected.Indexes.Value;

                var data = tx.Data != null && dataIndex < tx.Data.Count ? tx.Data[dataIndex] : "0x";
                var gasLimitStr = tx.GasLimit != null && gasIndex < tx.GasLimit.Count ? tx.GasLimit[gasIndex] : "0x0";
                var valueStr = tx.Value != null && valueIndex < tx.Value.Count ? tx.Value[valueIndex] : "0x0";

                var dataBytes = string.IsNullOrEmpty(data) || data == "0x" ? new byte[0] : data.HexToByteArray();
                var gasLimit = StateTestScalar.Parse(LegacyTransactionField.GasLimit, gasLimitStr);
                var value = StateTestScalar.Parse(LegacyTransactionField.Value, valueStr);
                var baseFee = string.IsNullOrEmpty(env.CurrentBaseFee) ? BigInteger.Zero : env.CurrentBaseFee.HexToBigInteger(false);

                const long MAX_TX_GAS_LIMIT = 16_777_216;
                if (_targetFork >= HardforkName.Osaka && gasLimit > MAX_TX_GAS_LIMIT)
                {
                    throw new TransactionValidationException(TransactionError.GasLimitExceedsMaximum, "TransactionException.GAS_LIMIT_EXCEEDS_MAXIMUM");
                }

                BigInteger gasPrice;
                BigInteger effectiveGasPrice;
                BigInteger maxFeePerGas = BigInteger.Zero;
                BigInteger maxPriorityFeePerGas = BigInteger.Zero;
                bool isEip1559 = !string.IsNullOrEmpty(tx.MaxFeePerGas);

                if (isEip1559)
                {
                    maxFeePerGas = tx.MaxFeePerGas.HexToBigInteger(false);
                    maxPriorityFeePerGas = string.IsNullOrEmpty(tx.MaxPriorityFeePerGas) ? BigInteger.Zero : tx.MaxPriorityFeePerGas.HexToBigInteger(false);

                    if (maxFeePerGas < baseFee)
                    {
                        throw new TransactionValidationException(TransactionError.InsufficientMaxFeePerGas, "TransactionException.INSUFFICIENT_MAX_FEE_PER_GAS");
                    }

                    if (maxPriorityFeePerGas > maxFeePerGas)
                    {
                        throw new TransactionValidationException(TransactionError.PriorityGreaterThanMaxFee, "TransactionException.PRIORITY_GREATER_THAN_MAX_FEE_PER_GAS");
                    }

                    gasPrice = maxFeePerGas;
                    var priorityFee = BigInteger.Min(maxPriorityFeePerGas, maxFeePerGas - baseFee);
                    effectiveGasPrice = baseFee + priorityFee;
                }
                else
                {
                    gasPrice = string.IsNullOrEmpty(tx.GasPrice) ? BigInteger.Zero : tx.GasPrice.HexToBigInteger(false);
                    effectiveGasPrice = gasPrice;

                    if (baseFee > 0 && gasPrice < baseFee)
                    {
                        throw new TransactionValidationException(TransactionError.InsufficientMaxFeePerGas, "TransactionException.INSUFFICIENT_MAX_FEE_PER_GAS");
                    }
                }

                var sender = tx.Sender ?? GetSenderFromSecretKey(tx.SecretKey);
                var toAddress = string.IsNullOrEmpty(tx.To) ? null : tx.To;
                var isContractCreation = toAddress == null;

                const int MAX_INITCODE_SIZE = 49152;
                if (isContractCreation && _targetFork >= HardforkName.Shanghai && dataBytes != null && dataBytes.Length > MAX_INITCODE_SIZE)
                {
                    throw new TransactionValidationException(TransactionError.InitcodeSizeExceeded, "TransactionException.INITCODE_SIZE_EXCEEDED");
                }

                var blobVersionedHashes = tx.BlobVersionedHashes;
                var isType3Transaction = !string.IsNullOrEmpty(tx.MaxFeePerBlobGas);

                if (isType3Transaction && _targetFork >= HardforkName.Cancun)
                {
                    if (isContractCreation)
                    {
                        throw new TransactionValidationException(TransactionError.Type3TxContractCreation, "TransactionException.TYPE_3_TX_CONTRACT_CREATION");
                    }

                    if (blobVersionedHashes == null || blobVersionedHashes.Count == 0)
                    {
                        throw new TransactionValidationException(TransactionError.Type3TxZeroBlobs, "TransactionException.TYPE_3_TX_ZERO_BLOBS");
                    }

                    int maxBlobs = _targetFork >= HardforkName.Prague ? MAX_BLOBS_PER_BLOCK_PRAGUE : MAX_BLOBS_PER_BLOCK_CANCUN;
                    if (blobVersionedHashes.Count > maxBlobs)
                    {
                        throw new TransactionValidationException(TransactionError.Type3TxBlobCountExceeded, "TransactionException.TYPE_3_TX_BLOB_COUNT_EXCEEDED");
                    }

                    foreach (var hash in blobVersionedHashes)
                    {
                        var hashBytes = hash.HexToByteArray();
                        if (hashBytes.Length < 1 || hashBytes[0] != VERSIONED_HASH_VERSION_KZG)
                        {
                            throw new TransactionValidationException(TransactionError.Type3TxInvalidBlobVersionedHash, "TransactionException.TYPE_3_TX_INVALID_BLOB_VERSIONED_HASH");
                        }
                    }
                }

                var accessList = tx.AccessLists != null && dataIndex < tx.AccessLists.Count
                    ? tx.AccessLists[dataIndex]
                    : null;

                var executionState = SetupPreState(test);

                var blockGasLimit = string.IsNullOrEmpty(env.CurrentGasLimit) ? BigInteger.Zero : env.CurrentGasLimit.HexToBigInteger(false);
                if (blockGasLimit > 0 && gasLimit > blockGasLimit)
                {
                    throw new TransactionValidationException(TransactionError.GasAllowanceExceeded, "TransactionException.GAS_ALLOWANCE_EXCEEDED");
                }

                var accessListEntries = accessList?.Select(a => new AccessListEntry
                {
                    Address = a.Address,
                    StorageKeys = a.StorageKeys
                }).ToList();

                var isSelfTransfer = !isContractCreation && sender.IsTheSameAddress(toAddress);
                var hasValue = value > 0;

                BigInteger intrinsicGas = _cachedConfig.IntrinsicGasRules.CalculateIntrinsicGas(dataBytes, isContractCreation, accessListEntries, isSelfTransfer, hasValue);

                BigInteger minGasRequired = intrinsicGas;
                BigInteger floorGas = _cachedConfig.IntrinsicGasRules.CalculateFloorGasLimit(dataBytes, isContractCreation, isSelfTransfer, hasValue, accessListEntries);
                if (floorGas > minGasRequired)
                    minGasRequired = floorGas;

                if (gasLimit < minGasRequired)
                {
                    if (!string.IsNullOrEmpty(expected.ExpectException))
                    {
                        result.Passed = ExpectedRejectionMatcher.Satisfies(
                            expected.ExpectException,
                            TransactionError.IntrinsicGasTooLow,
                            out var rejectionDetail);
                        result.Message = result.Passed
                            ? $"Expected exception {expected.ExpectException} - got {rejectionDetail}"
                            : $"Expected exception {expected.ExpectException} but got {rejectionDetail}";
                        return result;
                    }

                    result.Passed = false;
                    result.Message = $"Intrinsic gas too low: {gasLimit} < {minGasRequired}";
                    return result;
                }

                EnsureCachedConfig();
                executionState.MarkAddressAsWarm(sender);
                executionState.MarkPrecompilesAsWarm(_cachedConfig.Precompiles);

                if (!string.IsNullOrEmpty(env.CurrentCoinbase))
                {
                    executionState.MarkAddressAsWarm(env.CurrentCoinbase);
                }

                var senderAccount = executionState.CreateOrGetAccountExecutionState(sender);

                if (senderAccount.Code != null && senderAccount.Code.Length > 0)
                {
                    throw new TransactionValidationException(TransactionError.SenderNotEOA, "TransactionException.SENDER_NOT_EOA");
                }

                var senderBalance = senderAccount.Balance.GetTotalBalance();

                BigInteger blobGasCost = BigInteger.Zero;
                BigInteger blobBaseFee = BigInteger.One;
                BigInteger maxBlobReservation = BigInteger.Zero;
                if (isType3Transaction && _targetFork >= HardforkName.Cancun)
                {
                    if (!string.IsNullOrEmpty(env.CurrentExcessBlobGas))
                    {
                        var excessBlobGas = env.CurrentExcessBlobGas.HexToBigInteger(false);
                        blobBaseFee = (BigInteger)_cachedConfig.IntrinsicGasRules.Blob.CalculateBlobBaseFee(
                            EvmUInt256BigIntegerExtensions.FromBigInteger(excessBlobGas));
                    }
                    var blobCount = blobVersionedHashes?.Count ?? 0;
                    BigInteger blobGasUsed = BlobGasCalculator.CalculateTotalBlobGas(blobCount);
                    blobGasCost = blobGasUsed * blobBaseFee;

                    var maxFeePerBlobGas = tx.MaxFeePerBlobGas.HexToBigInteger(false);
                    if (maxFeePerBlobGas < blobBaseFee)
                    {
                        throw new TransactionValidationException(TransactionError.InsufficientMaxFeePerBlobGas, "TransactionException.INSUFFICIENT_MAX_FEE_PER_BLOB_GAS");
                    }
                    maxBlobReservation = blobGasUsed * maxFeePerBlobGas;
                }

                var maxCost = gasLimit * gasPrice + value + maxBlobReservation;

                if (senderBalance < EvmUInt256BigIntegerExtensions.FromBigInteger(maxCost))
                {
                    result.Skipped = true;
                    result.SkipReason = $"Insufficient balance: {senderBalance} < {maxCost}";
                    return result;
                }

                var senderNonceBeforeIncrement = senderAccount.Nonce ?? 0L;

                if (senderNonceBeforeIncrement.ToBigInteger() >= BigInteger.Parse("18446744073709551615"))
                {
                    throw new TransactionValidationException(TransactionError.NonceIsMax, "TransactionException.NONCE_IS_MAX");
                }

                senderAccount.Nonce = senderNonceBeforeIncrement + 1;

                senderAccount.Balance.DebitExecutionBalance(EvmUInt256BigIntegerExtensions.FromBigInteger(gasLimit * effectiveGasPrice));

                if (blobGasCost > 0)
                {
                    senderAccount.Balance.DebitExecutionBalance(EvmUInt256BigIntegerExtensions.FromBigInteger(blobGasCost));
                }

                var transactionSnapshotId = executionState.TakeSnapshot();

                byte[] code;
                string contractAddress = null;

                bool hasCollision = false;
                if (isContractCreation)
                {
                    contractAddress = ContractUtils.CalculateContractAddress(sender, senderNonceBeforeIncrement.ToLong());
                    executionState.MarkAddressAsWarm(contractAddress);
                    var contractAccount = executionState.CreateOrGetAccountExecutionState(contractAddress);

                    var targetHasCode = contractAccount.Code != null && contractAccount.Code.Length > 0;
                    var targetHasNonce = contractAccount.Nonce.HasValue && contractAccount.Nonce.Value > 0;
                    var targetHasStorage = contractAccount.Storage != null && contractAccount.Storage.Count > 0;
                    hasCollision = targetHasCode || targetHasNonce || targetHasStorage;

                    if (!hasCollision)
                    {
                        executionState.PrepareNewContractAccount(contractAddress);
                    }

                    code = hasCollision ? null : dataBytes;
                }
                else
                {
                    executionState.MarkAddressAsWarm(toAddress);
                    var receiverAccount = executionState.CreateOrGetAccountExecutionState(toAddress);
                    code = receiverAccount.Code;
                }

                if (accessList != null)
                {
                    foreach (var entry in accessList)
                    {
                        executionState.MarkAddressAsWarm(entry.Address);
                        var warmAccount = executionState.CreateOrGetAccountExecutionState(entry.Address);
                        if (entry.StorageKeys != null)
                        {
                            foreach (var storageKey in entry.StorageKeys)
                            {
                                var slot = storageKey.HexToBigInteger(false);
                                warmAccount.MarkStorageKeyAsWarm(EvmUInt256BigIntegerExtensions.FromBigInteger(slot));
                            }
                        }
                    }
                }

                for (int i = 1; i <= 9; i++)
                {
                    var precompileAddress = "0x" + i.ToString("x").PadLeft(40, '0');
                    executionState.MarkAddressAsWarm(precompileAddress);
                }

                BigInteger gasUsed = intrinsicGas;
                bool executionSuccess = !hasCollision;
                string revertReason = null;
                BigInteger gasRefund = BigInteger.Zero;

                if (hasCollision)
                {
                    gasUsed = gasLimit;
                    executionState.RevertToSnapshot(transactionSnapshotId);
                }

                if (code != null && code.Length > 0)
                {
                    var targetAddress = isContractCreation ? contractAddress : toAddress;
                    var transaction = new TransactionInput
                    {
                        From = sender,
                        To = targetAddress,
                        Data = isContractCreation ? "" : data,
                        Gas = new Hex.HexTypes.HexBigInteger(gasLimit - intrinsicGas),
                        Value = new Hex.HexTypes.HexBigInteger(value),
                        Nonce = new Hex.HexTypes.HexBigInteger(tx.Nonce ?? "0x0"),
                        GasPrice = new Hex.HexTypes.HexBigInteger(effectiveGasPrice),
                        ChainId = new Hex.HexTypes.HexBigInteger(1)
                    };

                    var blockNumber = string.IsNullOrEmpty(env.CurrentNumber)
                        ? 1
                        : (long)env.CurrentNumber.HexToBigInteger(false);

                    var timestamp = string.IsNullOrEmpty(env.CurrentTimestamp)
                        ? 0
                        : (long)env.CurrentTimestamp.HexToBigInteger(false);

                    var programContext = new ProgramContext(
                        transaction,
                        executionState,
                        null,
                        blockNumber: blockNumber,
                        timestamp: timestamp,
                        coinbase: env.CurrentCoinbase,
                        baseFee: (long)baseFee,
                        codeAddress: isContractCreation ? contractAddress : null
                    );

                    if (!string.IsNullOrEmpty(env.CurrentRandom))
                        programContext.Difficulty = EvmUInt256BigIntegerExtensions.FromBigInteger(env.CurrentRandom.HexToBigInteger(false));
                    else if (!string.IsNullOrEmpty(env.CurrentDifficulty))
                        programContext.Difficulty = EvmUInt256BigIntegerExtensions.FromBigInteger(env.CurrentDifficulty.HexToBigInteger(false));

                    if (!string.IsNullOrEmpty(env.CurrentGasLimit))
                        programContext.GasLimit = (long)env.CurrentGasLimit.HexToBigInteger(false);

                    if (!string.IsNullOrEmpty(env.CurrentExcessBlobGas))
                    {
                        var excessBlobGas = (ulong)env.CurrentExcessBlobGas.HexToBigInteger(false);
                        programContext.BlobBaseFee = _cachedConfig.IntrinsicGasRules.Blob.CalculateBlobBaseFee(excessBlobGas);
                    }

                    if (isType3Transaction && blobVersionedHashes != null && blobVersionedHashes.Count > 0)
                    {
                        programContext.BlobHashes = blobVersionedHashes.Select(h => h.HexToByteArray()).ToArray();
                    }

                    programContext.EnforceSstoreGasStipend = _cachedConfig.EnforceSstoreGasStipend;

                    var program = new Program(code, programContext);
                    var evmSimulator = new EVMSimulator(_cachedConfig);

                    if (value > 0)
                    {
                        senderAccount.Balance.DebitExecutionBalance(EvmUInt256BigIntegerExtensions.FromBigInteger(value));
                    }

                    try
                    {
                        program = await evmSimulator.ExecuteWithCallStackAsync(program, traceEnabled: captureTraces);

                        if (captureTraces)
                        {
                            programForTraces = program;
                        }

                        gasUsed = intrinsicGas + program.TotalGasUsed;
                        executionSuccess = !program.ProgramResult.IsRevert;


                        if (executionSuccess)
                        {
                            var maxRefund = gasUsed / _cachedConfig.RefundQuotient;
                            gasRefund = BigInteger.Min(program.RefundCounter, maxRefund);

                            var effectiveGasUsed = gasUsed - gasRefund;
                            if (effectiveGasUsed < intrinsicGas)
                                effectiveGasUsed = intrinsicGas;

                            gasUsed = effectiveGasUsed;
                        }
                        revertReason = program.ProgramResult.GetRevertMessage();


                        if (executionSuccess)
                        {
                            if (isContractCreation)
                            {
                                var deployedCode = program.ProgramResult.Result ?? new byte[0];

                                if (deployedCode.Length > 0 && deployedCode[0] == 0xEF)
                                {
                                    executionSuccess = false;
                                    executionState.RevertToSnapshot(transactionSnapshotId);
                                    gasUsed = gasLimit;
                                }
                                else
                                {
                                    var codeDepositGas = deployedCode.Length * G_CODEDEPOSIT;

                                    if (gasUsed + codeDepositGas > gasLimit || deployedCode.Length > 24576)
                                    {
                                        executionSuccess = false;
                                        executionState.RevertToSnapshot(transactionSnapshotId);
                                        gasUsed = gasLimit;
                                    }
                                    else
                                    {
                                        gasUsed += codeDepositGas;
                                        var newContractAccount = executionState.CreateOrGetAccountExecutionState(contractAddress);
                                        newContractAccount.Code = deployedCode;
                                        executionState.CommitSnapshot(transactionSnapshotId);
                                    }
                                }
                            }
                            else
                            {
                                executionState.CommitSnapshot(transactionSnapshotId);
                            }

                            var createdSet = new HashSet<string>(program.ProgramResult.CreatedContractAccounts, StringComparer.OrdinalIgnoreCase);
                            if (isContractCreation)
                            {
                                createdSet.Add(contractAddress);
                            }
                            foreach (var deletedAddr in program.ProgramResult.DeletedContractAccounts)
                            {
                                if (createdSet.Contains(deletedAddr))
                                {
                                    executionState.DeleteAccount(deletedAddr);
                                }
                            }
                        }
                        else
                        {
                            executionState.RevertToSnapshot(transactionSnapshotId);
                            if (program.GasRemaining == 0)
                            {
                                gasUsed = gasLimit;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        if (!string.IsNullOrEmpty(expected.ExpectException))
                        {
                            result.Passed = ExpectedRejectionMatcher.Satisfies(expected.ExpectException, ex, out var detail);
                            result.Message = result.Passed
                                ? $"Expected exception {expected.ExpectException} - got {detail}"
                                : $"Expected exception {expected.ExpectException} but got {detail}";
                            if (!result.Passed) result.StackTrace = ex.StackTrace;
                            return result;
                        }
                        result.Passed = false;
                        result.Message = $"Exception: {ex.Message}";
                        return result;
                    }
                }
                else if (!isContractCreation && !string.IsNullOrEmpty(toAddress))
                {
                    var precompiledContracts = new EvmPreCompiledContractsExecution();
                    if (precompiledContracts.IsPrecompiledAddress(toAddress))
                    {
                        var precompileInput = dataBytes ?? new byte[0];
                        var precompileGasCost = precompiledContracts.GetPrecompileGasCost(toAddress, precompileInput);
                        var availableGas = gasLimit - intrinsicGas;

                        if (precompileGasCost > availableGas)
                        {
                            gasUsed = gasLimit;
                            executionSuccess = false;
                            executionState.RevertToSnapshot(transactionSnapshotId);
                        }
                        else
                        {
                            gasUsed = intrinsicGas + (long)precompileGasCost;
                            try
                            {
                                precompiledContracts.ExecutePreCompile(toAddress, precompileInput);
                                executionSuccess = true;
                                if (value > 0)
                                {
                                    senderAccount.Balance.DebitExecutionBalance(EvmUInt256BigIntegerExtensions.FromBigInteger(value));
                                    var receiverAccount = executionState.CreateOrGetAccountExecutionState(toAddress);
                                    receiverAccount.Balance.CreditExecutionBalance(EvmUInt256BigIntegerExtensions.FromBigInteger(value));
                                }
                                executionState.CommitSnapshot(transactionSnapshotId);
                            }
                            catch
                            {
                                gasUsed = gasLimit;
                                executionSuccess = false;
                                executionState.RevertToSnapshot(transactionSnapshotId);
                            }
                        }
                    }
                    else if (value > 0)
                    {
                        senderAccount.Balance.DebitExecutionBalance(EvmUInt256BigIntegerExtensions.FromBigInteger(value));
                        var receiverAccount = executionState.CreateOrGetAccountExecutionState(toAddress);
                        receiverAccount.Balance.CreditExecutionBalance(EvmUInt256BigIntegerExtensions.FromBigInteger(value));
                        executionState.CommitSnapshot(transactionSnapshotId);
                    }
                }
                else if (executionSuccess && value > 0)
                {
                    senderAccount.Balance.DebitExecutionBalance(EvmUInt256BigIntegerExtensions.FromBigInteger(value));
                    if (isContractCreation)
                    {
                        var newContractAccount = executionState.CreateOrGetAccountExecutionState(contractAddress);
                        newContractAccount.Balance.CreditExecutionBalance(EvmUInt256BigIntegerExtensions.FromBigInteger(value));
                    }
                    executionState.CommitSnapshot(transactionSnapshotId);
                }

                if (!string.IsNullOrEmpty(expected.ExpectException) && executionSuccess)
                {
                    result.Passed = false;
                    result.Message = $"Expected exception {expected.ExpectException} but execution succeeded";
                    return result;
                }

                if (gasUsed > gasLimit)
                    gasUsed = gasLimit;

                bool floorIsContractCreation = _cachedConfig.IntrinsicGasRules.StateGasActive && isContractCreation;
                BigInteger floorDataGas = _cachedConfig.IntrinsicGasRules.CalculateFloorGasLimit(dataBytes, floorIsContractCreation, isSelfTransfer, hasValue, accessListEntries);
                if (gasUsed < floorDataGas)
                {
                    gasUsed = floorDataGas;
                    if (gasUsed > gasLimit)
                        gasUsed = gasLimit;
                }

                var gasRefundAmount = (gasLimit - gasUsed) * effectiveGasPrice;
                senderAccount.Balance.CreditExecutionBalance(EvmUInt256BigIntegerExtensions.FromBigInteger(gasRefundAmount));

                var coinbase = env.CurrentCoinbase;
                var minerReward = gasUsed * (effectiveGasPrice - baseFee);
                if (minerReward < 0) minerReward = 0;
                var coinbaseAccount = executionState.CreateOrGetAccountExecutionState(coinbase);
                if (minerReward > 0)
                    coinbaseAccount.Balance.CreditExecutionBalance(EvmUInt256BigIntegerExtensions.FromBigInteger(minerReward));
                else
                    coinbaseAccount.IsTouched = true;

                EnsureCachedConfig();
                _cachedConfig.TouchedEmptyCleanupRule.Apply(executionState);
                var accountStates = ExtractPostState(executionState);
                var computedStateRoot = StateRootCalculator.CalculateStateRoot(accountStates, skipEmptyAccounts: false);
                var expectedStateRoot = expected.Hash.HexToByteArray();

                result.ExpectedStateRoot = expected.Hash;
                result.ActualStateRoot = computedStateRoot.ToHex(true);

                if (computedStateRoot.SequenceEqual(expectedStateRoot))
                {
                    result.Passed = true;
                }
                else
                {
                    result.Passed = false;
                    result.Message = $"State root mismatch. Expected: {expected.Hash}, Actual: {computedStateRoot.ToHex(true)}";

                    if (expected.State != null && expected.State.Count > 0)
                    {
                        result.AccountDiffs = CompareWithExpectedState(expected.State, accountStates);
                    }
                    else
                    {
                        result.AccountDiffs = CompareStates(test.Pre, accountStates);
                        result.AccountDiffs.Insert(0, "(No expected post-state in test file, comparing with pre-state)");
                    }
                    if (result.AccountDiffs == null) result.AccountDiffs = new List<string>();
                    result.AccountDiffs.Add($"--- OUR COMPUTED ACCOUNTS ({accountStates.Count}) ---");
                    foreach (var kv in accountStates)
                    {
                        var a = kv.Value;
                        result.AccountDiffs.Add($"  {kv.Key}: bal={a.Balance} nonce={a.Nonce} codeLen={(a.Code?.Length ?? 0)} storage[{a.Storage?.Count ?? 0}]");
                        if (a.Storage != null)
                            foreach (var sk in a.Storage)
                                result.AccountDiffs.Add($"    slot[{sk.Key}] = {sk.Value}");
                    }
                }
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrEmpty(expected.ExpectException))
                {
                    result.Passed = ExpectedRejectionMatcher.Satisfies(expected.ExpectException, ex, out var detail);
                    result.Message = result.Passed
                        ? $"Expected exception {expected.ExpectException} - got {detail}"
                        : $"Expected exception {expected.ExpectException} but got {detail}";
                    if (!result.Passed) result.StackTrace = ex.StackTrace;
                }
                else
                {
                    result.Passed = false;
                    result.Message = $"Exception: {ex.Message}";
                    result.StackTrace = ex.StackTrace;
                }
            }

            if (captureTraces && programForTraces != null)
            {
                result.Traces = programForTraces.Trace;
            }

            return result;
        }

        public async Task<(ExecutionStateService State, string Coinbase, string Sender)> RunAndCaptureExecutionStateAsync(
            string testFilePath, int dataIndex, int gasIndex = 0, int valueIndex = 0)
        {
            var json = File.ReadAllText(testFilePath);
            var tests = JsonConvert.DeserializeObject<Dictionary<string, GeneralStateTest>>(json);
            foreach (var testEntry in tests)
            {
                var test = testEntry.Value;
                if (!test.Post.ContainsKey(_targetHardfork)) continue;
                foreach (var postResult in test.Post[_targetHardfork])
                {
                    if (postResult.Indexes.Data != dataIndex) continue;
                    if (postResult.Indexes.Gas != gasIndex) continue;
                    if (postResult.Indexes.Value != valueIndex) continue;
                    var ctx = BuildExecutionContext(test, postResult, captureTraces: false);
                    if (ctx == null) return (null, null, null);
                    EnsureCachedConfig();
                    var executor = new TransactionExecutor(_cachedConfig);
                    try { await executor.ExecuteAsync(ctx); }
                    catch { return (null, null, null); }
                    return (ctx.ExecutionState, ctx.Coinbase, ctx.Sender);
                }
            }
            return (null, null, null);
        }

        private async Task<SingleTestResult> RunSingleTestWithExecutorAsync(string testName, GeneralStateTest test, PostResult expected, bool captureTraces = false)
        {
            var result = new SingleTestResult
            {
                TestName = testName,
                DataIndex = expected.Indexes.Data,
                GasIndex = expected.Indexes.Gas,
                ValueIndex = expected.Indexes.Value
            };

            try
            {
                var ctx = BuildExecutionContext(test, expected, captureTraces);
                if (ctx == null)
                {
                    result.Skipped = true;
                    result.SkipReason = "Failed to build execution context";
                    return result;
                }

                var fixtureConfig = ResolveConfigForFixture(test);
                var executor = new TransactionExecutor(fixtureConfig);

                var execResult = await executor.ExecuteAsync(ctx);

                if (execResult.IsValidationError)
                {
                    if (!string.IsNullOrEmpty(expected.ExpectException))
                    {
                        result.Passed = ExpectedRejectionMatcher.Satisfies(expected.ExpectException, execResult.ErrorCode, out var detail);
                        result.Message = result.Passed
                            ? $"Expected exception {expected.ExpectException} - got validation error: {detail}"
                            : $"Expected exception {expected.ExpectException} but got validation error: {detail}";
                        return result;
                    }

                    if (execResult.Error?.Contains("Insufficient balance") == true)
                    {
                        result.Skipped = true;
                        result.SkipReason = execResult.Error;
                        return result;
                    }

                    result.Passed = false;
                    result.Message = $"Validation error: {execResult.Error}";
                    return result;
                }

                if (!string.IsNullOrEmpty(expected.ExpectException) && execResult.Success)
                {
                    result.Passed = false;
                    result.Message = $"Expected exception {expected.ExpectException} but execution succeeded";
                    return result;
                }

                if (captureTraces && execResult.Traces != null)
                {
                    result.Traces = execResult.Traces;
                }

                var accountStates = ExtractPostState(ctx.ExecutionState);
                var computedStateRoot = StateRootCalculator.CalculateStateRoot(accountStates, skipEmptyAccounts: false);
                var expectedStateRoot = expected.Hash.HexToByteArray();

                result.ExpectedStateRoot = expected.Hash;
                result.ActualStateRoot = computedStateRoot.ToHex(true);

                if (computedStateRoot.SequenceEqual(expectedStateRoot))
                {
                    result.Passed = true;
                }
                else
                {
                    result.Passed = false;
                    result.Message = $"State root mismatch. Expected: {expected.Hash}, Actual: {computedStateRoot.ToHex(true)}";

                    if (expected.State != null && expected.State.Count > 0)
                    {
                        result.AccountDiffs = CompareWithExpectedState(expected.State, accountStates);
                    }
                    else
                    {
                        result.AccountDiffs = CompareStates(test.Pre, accountStates);
                        result.AccountDiffs.Insert(0, "(No expected post-state in test file, comparing with pre-state)");
                    }
                    result.FullPostState = DumpFullPostState(accountStates);
                }
            }
            catch (TransactionValidationException ex)
            {
                if (!string.IsNullOrEmpty(expected.ExpectException))
                {
                    result.Passed = ExpectedRejectionMatcher.Satisfies(expected.ExpectException, ex, out var detail);
                    result.Message = result.Passed
                        ? $"Expected exception {expected.ExpectException} - got {detail}"
                        : $"Expected exception {expected.ExpectException} but got {detail}";
                }
                else
                {
                    result.Passed = false;
                    result.Message = $"Validation exception: {ex.Message}";
                }
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrEmpty(expected.ExpectException))
                {
                    result.Passed = ExpectedRejectionMatcher.Satisfies(expected.ExpectException, ex, out var detail);
                    result.Message = result.Passed
                        ? $"Expected exception {expected.ExpectException} - got {detail}"
                        : $"Expected exception {expected.ExpectException} but got {detail}";
                    if (!result.Passed) result.StackTrace = ex.StackTrace;
                }
                else
                {
                    result.Passed = false;
                    result.Message = $"Exception: {ex.Message}";
                    result.StackTrace = ex.StackTrace;
                }
            }

            return result;
        }

        private TransactionExecutionContext BuildExecutionContext(GeneralStateTest test, PostResult expected, bool captureTraces)
        {
            var env = test.Env;
            var tx = test.Transaction;

            var dataIndex = expected.Indexes.Data;
            var gasIndex = expected.Indexes.Gas;
            var valueIndex = expected.Indexes.Value;

            var data = tx.Data != null && dataIndex < tx.Data.Count ? tx.Data[dataIndex] : "0x";
            var gasLimitStr = tx.GasLimit != null && gasIndex < tx.GasLimit.Count ? tx.GasLimit[gasIndex] : "0x0";
            var valueStr = tx.Value != null && valueIndex < tx.Value.Count ? tx.Value[valueIndex] : "0x0";

            var dataBytes = string.IsNullOrEmpty(data) || data == "0x" ? new byte[0] : data.HexToByteArray();
            var gasLimit = StateTestScalar.Parse(LegacyTransactionField.GasLimit, gasLimitStr);
            var value = StateTestScalar.Parse(LegacyTransactionField.Value, valueStr);
            var baseFee = string.IsNullOrEmpty(env.CurrentBaseFee) ? BigInteger.Zero : env.CurrentBaseFee.HexToBigInteger(false);

            BigInteger gasPrice;
            BigInteger maxFeePerGas = BigInteger.Zero;
            BigInteger maxPriorityFeePerGas = BigInteger.Zero;
            bool isEip1559 = !string.IsNullOrEmpty(tx.MaxFeePerGas);

            if (isEip1559)
            {
                maxFeePerGas = tx.MaxFeePerGas.HexToBigInteger(false);
                maxPriorityFeePerGas = string.IsNullOrEmpty(tx.MaxPriorityFeePerGas) ? BigInteger.Zero : tx.MaxPriorityFeePerGas.HexToBigInteger(false);
                gasPrice = maxFeePerGas;
            }
            else
            {
                gasPrice = string.IsNullOrEmpty(tx.GasPrice) ? BigInteger.Zero : tx.GasPrice.HexToBigInteger(false);
            }

            var sender = tx.Sender ?? GetSenderFromSecretKey(tx.SecretKey);
            var toAddress = string.IsNullOrEmpty(tx.To) ? null : tx.To;
            var isContractCreation = toAddress == null;

            var blobVersionedHashes = tx.BlobVersionedHashes;
            var isType3Transaction = !string.IsNullOrEmpty(tx.MaxFeePerBlobGas);

            var accessList = tx.AccessLists != null && dataIndex < tx.AccessLists.Count
                ? tx.AccessLists[dataIndex]
                : null;

            List<AccessListEntry> accessListEntries = null;
            if (accessList != null)
            {
                accessListEntries = accessList.Select(a => new AccessListEntry
                {
                    Address = a.Address,
                    StorageKeys = a.StorageKeys
                }).ToList();
            }

            var executionState = SetupPreState(test);

            var blockNumber = string.IsNullOrEmpty(env.CurrentNumber)
                ? 1L
                : (long)env.CurrentNumber.HexToBigInteger(false);

            var timestamp = string.IsNullOrEmpty(env.CurrentTimestamp)
                ? 0L
                : (long)env.CurrentTimestamp.HexToBigInteger(false);

            var blockGasLimit = string.IsNullOrEmpty(env.CurrentGasLimit)
                ? 0L
                : (long)env.CurrentGasLimit.HexToBigInteger(false);

            EvmUInt256 difficulty = EvmUInt256.Zero;
            if (!string.IsNullOrEmpty(env.CurrentRandom))
                difficulty = EvmUInt256BigIntegerExtensions.FromBigInteger(env.CurrentRandom.HexToBigInteger(false));
            else if (!string.IsNullOrEmpty(env.CurrentDifficulty))
                difficulty = EvmUInt256BigIntegerExtensions.FromBigInteger(env.CurrentDifficulty.HexToBigInteger(false));

            ulong excessBlobGas = 0;
            if (!string.IsNullOrEmpty(env.CurrentExcessBlobGas))
                excessBlobGas = (ulong)env.CurrentExcessBlobGas.HexToBigInteger(false);

            EvmUInt256 slotNumber = EvmUInt256.Zero;
            if (!string.IsNullOrEmpty(env.SlotNumber))
                slotNumber = EvmUInt256BigIntegerExtensions.FromBigInteger(env.SlotNumber.HexToBigInteger(false));

            var nonce = string.IsNullOrEmpty(tx.Nonce) ? 0UL : (ulong)tx.Nonce.HexToBigInteger(false);

            EvmUInt256? declaredChainId = string.IsNullOrEmpty(tx.ChainId)
                ? (EvmUInt256?)null
                : EvmUInt256BigIntegerExtensions.FromBigInteger(tx.ChainId.HexToBigInteger(false));

            List<Authorisation7702Signed> authList = null;
            if (tx.AuthorizationList != null && tx.AuthorizationList.Count == 0)
            {
                authList = new List<Authorisation7702Signed>();
            }
            else if (tx.AuthorizationList != null && tx.AuthorizationList.Count > 0)
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
                BlobVersionedHashes = blobVersionedHashes,
                MaxFeePerBlobGas = isType3Transaction ? EvmUInt256BigIntegerExtensions.FromBigInteger(tx.MaxFeePerBlobGas.HexToBigInteger(false)) : EvmUInt256.Zero,
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

                ChainId = EvmUInt256.One,
                DeclaredChainId = declaredChainId,

                ExecutionState = executionState,
                TraceEnabled = captureTraces
            };
        }

        private ExecutionStateService SetupPreState(GeneralStateTest test)
        {
            var executionState = new ExecutionStateService(new MockNodeDataService(BuildPreStateReader(test)));

            foreach (var preAccount in test.Pre)
            {
                var address = preAccount.Key;
                var account = preAccount.Value;

                var accountState = executionState.CreateOrGetAccountExecutionState(address);
                accountState.WasInPreState = true;

                accountState.Code = string.IsNullOrEmpty(account.Code) || account.Code == "0x"
                    ? new byte[0]
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
                        var value = storage.Value.HexToByteArray();
                        accountState.SetPreStateStorage(key, value);
                    }
                }

            }

            return executionState;
        }

        private static InMemoryStateReader BuildPreStateReader(GeneralStateTest test)
        {
            var witnessAccounts = new List<Nethereum.EVM.Witness.WitnessAccount>();

            foreach (var preAccount in test.Pre)
            {
                var address = preAccount.Key;
                var account = preAccount.Value;

                var witnessAccount = new Nethereum.EVM.Witness.WitnessAccount
                {
                    Address = address,
                    Balance = EvmUInt256BigIntegerExtensions.FromBigInteger(
                        string.IsNullOrEmpty(account.Balance) ? BigInteger.Zero : account.Balance.HexToBigInteger(false)),
                    Nonce = string.IsNullOrEmpty(account.Nonce) ? 0UL : (ulong)account.Nonce.HexToBigInteger(false),
                    Code = string.IsNullOrEmpty(account.Code) || account.Code == "0x" ? new byte[0] : account.Code.HexToByteArray(),
                    Storage = new List<Nethereum.EVM.Witness.WitnessStorageSlot>()
                };

                if (account.Storage != null)
                {
                    foreach (var storage in account.Storage)
                    {
                        witnessAccount.Storage.Add(new Nethereum.EVM.Witness.WitnessStorageSlot
                        {
                            Key = EvmUInt256BigIntegerExtensions.FromBigInteger(storage.Key.HexToBigInteger(false)),
                            Value = EvmUInt256.FromBigEndian(storage.Value.HexToByteArray().PadTo32Bytes())
                        });
                    }
                }

                witnessAccounts.Add(witnessAccount);
            }

            return new InMemoryStateReader(
                Nethereum.EVM.Witness.WitnessStateBuilder.BuildAccountState(witnessAccounts));
        }

        private string GetSenderFromSecretKey(string secretKey)
        {
            if (string.IsNullOrEmpty(secretKey))
                return "0x0000000000000000000000000000000000000000";

            var key = new Nethereum.Signer.EthECKey(secretKey);
            return key.GetPublicAddress();
        }

        private static Dictionary<string, string> DumpFullPostState(Dictionary<string, AccountState> accountStates)
        {
            var result = new Dictionary<string, string>();
            foreach (var addr in accountStates.Keys.OrderBy(k => k))
            {
                var a = accountStates[addr];
                var balanceHex = "0x" + a.Balance.ToString("X").TrimStart('0');
                if (balanceHex == "0x") balanceHex = "0x0";
                var nonceHex = "0x" + a.Nonce.ToString("X").TrimStart('0');
                if (nonceHex == "0x") nonceHex = "0x0";
                var codeLen = a.Code?.Length ?? 0;
                var sb = new System.Text.StringBuilder();
                sb.Append("balance=").Append(balanceHex).Append(" nonce=").Append(nonceHex).Append(" codeLen=").Append(codeLen);
                if (a.Storage != null && a.Storage.Count > 0)
                {
                    sb.Append(" storage={");
                    bool first = true;
                    foreach (var kvp in a.Storage.OrderBy(k => k.Key))
                    {
                        if (!first) sb.Append(",");
                        sb.Append("0x").Append(kvp.Key.ToString("X")).Append("=0x").Append(BitConverter.ToString(kvp.Value).Replace("-", "").TrimStart('0'));
                        first = false;
                    }
                    sb.Append("}");
                }
                result[addr] = sb.ToString();
            }
            return result;
        }

        private Dictionary<string, AccountState> ExtractPostState(ExecutionStateService executionState, bool skipPhantomAccounts = true)
        {
            var result = new Dictionary<string, AccountState>();

            foreach (var kvp in executionState.AccountsState)
            {
                var address = kvp.Key.ToHexLower();
                var accountExecState = kvp.Value;

                if (accountExecState.IsRemoved) continue;

                if (skipPhantomAccounts &&
                    !accountExecState.WasInPreState &&
                    !accountExecState.IsTouched &&
                    !accountExecState.IsNewContract &&
                    accountExecState.Storage.Count == 0 &&
                    accountExecState.Balance.GetTotalBalance().IsZero &&
                    (accountExecState.Code == null || accountExecState.Code.Length == 0) &&
                    (!accountExecState.Nonce.HasValue || accountExecState.Nonce.Value.IsZero))
                {
                    continue;
                }

                var accountState = new AccountState
                {
                    Nonce = (accountExecState.Nonce ?? (EvmUInt256)0L).ToBigInteger(),
                    Balance = accountExecState.Balance.GetTotalBalance().ToBigInteger(),
                    Code = accountExecState.Code ?? new byte[0],
                    Storage = new Dictionary<BigInteger, byte[]>()
                };

                foreach (var storageKvp in accountExecState.Storage)
                {
                    if (storageKvp.Value != null)
                        accountState.Storage[storageKvp.Key.ToBigInteger()] = storageKvp.Value;
                }

                result[address] = accountState;
            }

            return result;
        }

        private List<string> CompareWithExpectedState(Dictionary<string, TestAccount> expected, Dictionary<string, AccountState> actual)
        {
            var diffs = new List<string>();

            foreach (var expectedEntry in expected)
            {
                var address = expectedEntry.Key.ToLowerInvariant();
                var expectedAccount = expectedEntry.Value;

                if (!actual.TryGetValue(address, out var actualAccount))
                {
                    diffs.Add($"MISSING: Account {address} expected but not found");
                    continue;
                }

                var expectedBalance = string.IsNullOrEmpty(expectedAccount.Balance) ? BigInteger.Zero : expectedAccount.Balance.HexToBigInteger(false);
                if (actualAccount.Balance != expectedBalance)
                {
                    var diff = actualAccount.Balance - expectedBalance;
                    diffs.Add($"BALANCE: {address} expected={expectedBalance}, actual={actualAccount.Balance}, diff={diff}");
                }

                var expectedNonce = string.IsNullOrEmpty(expectedAccount.Nonce) ? BigInteger.Zero : expectedAccount.Nonce.HexToBigInteger(false);
                if (actualAccount.Nonce != expectedNonce)
                {
                    diffs.Add($"NONCE: {address} expected={expectedNonce}, actual={actualAccount.Nonce}");
                }

                if (expectedAccount.Storage != null)
                {
                    foreach (var storageEntry in expectedAccount.Storage)
                    {
                        var slot = storageEntry.Key.HexToBigInteger(false);
                        var expectedValue = storageEntry.Value.HexToByteArray();
                        var expectedValueTrimmed = TrimLeadingZeros(expectedValue);

                        byte[] actualValue = null;
                        actualAccount.Storage?.TryGetValue(slot, out actualValue);
                        var actualValueTrimmed = TrimLeadingZeros(actualValue ?? new byte[0]);

                        if (!expectedValueTrimmed.SequenceEqual(actualValueTrimmed))
                        {
                            diffs.Add($"STORAGE: {address}[{slot}] expected={expectedValue.ToHex(true)}, actual={(actualValue?.ToHex(true) ?? "0x")}");
                        }
                    }
                }
            }

            foreach (var actualEntry in actual)
            {
                var address = actualEntry.Key.ToLowerInvariant();
                if (!expected.Any(e => e.Key.ToLowerInvariant() == address))
                {
                    var account = actualEntry.Value;
                    if (account.Balance > 0 || account.Nonce > 0 || (account.Code != null && account.Code.Length > 0) || (account.Storage != null && account.Storage.Count > 0))
                    {
                        diffs.Add($"EXTRA: Account {address} not in expected state (balance={account.Balance}, nonce={account.Nonce})");
                    }
                }
            }

            return diffs;
        }

        private List<string> CompareStates(Dictionary<string, TestAccount> pre, Dictionary<string, AccountState> actual)
        {
            var diffs = new List<string>();

            foreach (var preEntry in pre)
            {
                var address = preEntry.Key.ToLowerInvariant();
                if (!actual.TryGetValue(address, out var actualAccount))
                {
                    diffs.Add($"Account {address} missing from result");
                    continue;
                }

                var preAccount = preEntry.Value;
                var preBalance = string.IsNullOrEmpty(preAccount.Balance) ? BigInteger.Zero : preAccount.Balance.HexToBigInteger(false);

                if (actualAccount.Balance != preBalance)
                {
                    diffs.Add($"Account {address} balance: pre={preBalance}, post={actualAccount.Balance}");
                }
            }

            return diffs;
        }

        private static byte[] TrimLeadingZeros(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return new byte[0];

            var firstNonZero = 0;
            while (firstNonZero < bytes.Length && bytes[firstNonZero] == 0)
                firstNonZero++;

            if (firstNonZero == bytes.Length)
                return new byte[0];

            var result = new byte[bytes.Length - firstNonZero];
            Array.Copy(bytes, firstNonZero, result, 0, result.Length);
            return result;
        }
    }

    public class TestResult
    {
        public string FilePath { get; set; }
        public List<SingleTestResult> Results { get; set; } = new List<SingleTestResult>();

        public int PassedCount => Results.Count(r => r.Passed);
        public int FailedCount => Results.Count(r => !r.Passed && !r.Skipped);
        public int SkippedCount => Results.Count(r => r.Skipped);
    }

    public class SingleTestResult
    {
        public string TestName { get; set; }
        public int DataIndex { get; set; }
        public int GasIndex { get; set; }
        public int ValueIndex { get; set; }
        public bool Passed { get; set; }
        public bool Skipped { get; set; }
        public string SkipReason { get; set; }
        public string Message { get; set; }
        public string StackTrace { get; set; }
        public string ExpectedStateRoot { get; set; }
        public string ActualStateRoot { get; set; }
        public List<string> AccountDiffs { get; set; }
        public List<ProgramTrace> Traces { get; set; }
        public Dictionary<string, string> FullPostState { get; set; }
    }
}
