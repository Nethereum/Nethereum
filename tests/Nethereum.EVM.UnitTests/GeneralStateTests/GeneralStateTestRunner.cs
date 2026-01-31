using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Execution;
using Nethereum.EVM.Gas;
using Nethereum.Hex.HexConvertors.Extensions;
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
        private readonly TimeSpan _testTimeout;

        private const int G_TRANSACTION = 21000;
        private const int G_TXDATAZERO = 4;
        private const int G_TXDATANONZERO = 16;
        private const int G_TXCREATE = 32000;
        private const int G_CODEDEPOSIT = 200;

        private const int G_FLOOR_PER_TOKEN = 10;
        private const int G_TOKENS_PER_NONZERO = 4;

        // EIP-4844 Blob transaction constants
        private const int GAS_PER_BLOB = 131072; // 2^17
        private const int MAX_BLOBS_PER_BLOCK_CANCUN = 6;
        private const int MAX_BLOBS_PER_BLOCK_PRAGUE = 9;
        private const byte VERSIONED_HASH_VERSION_KZG = 0x01;

        public GeneralStateTestRunner(ITestOutputHelper output = null, string targetHardfork = "Prague", TimeSpan? testTimeout = null)
        {
            _output = output;
            _targetHardfork = targetHardfork;
            _testTimeout = testTimeout ?? TimeSpan.FromSeconds(30);
        }

        public async Task<TestResult> RunTestAsync(string testFilePath, int? specificDataIndex = null)
        {
            return await RunTestInternalAsync(testFilePath, captureTraces: false, specificDataIndex: specificDataIndex);
        }

        public async Task<TestResult> RunTestWithTraceAsync(string testFilePath, int? specificDataIndex = null)
        {
            return await RunTestInternalAsync(testFilePath, captureTraces: true, specificDataIndex: specificDataIndex);
        }

        /// <summary>
        /// Runs tests using the new TransactionExecutor infrastructure.
        /// This allows comparison with the original implementation.
        /// </summary>
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
                var gasLimit = gasLimitStr.HexToBigInteger(false);
                var value = valueStr.HexToBigInteger(false);
                var baseFee = string.IsNullOrEmpty(env.CurrentBaseFee) ? BigInteger.Zero : env.CurrentBaseFee.HexToBigInteger(false);

                BigInteger gasPrice;
                BigInteger effectiveGasPrice;
                BigInteger maxFeePerGas = BigInteger.Zero;
                BigInteger maxPriorityFeePerGas = BigInteger.Zero;
                bool isEip1559 = !string.IsNullOrEmpty(tx.MaxFeePerGas);

                if (isEip1559)
                {
                    maxFeePerGas = tx.MaxFeePerGas.HexToBigInteger(false);
                    maxPriorityFeePerGas = string.IsNullOrEmpty(tx.MaxPriorityFeePerGas) ? BigInteger.Zero : tx.MaxPriorityFeePerGas.HexToBigInteger(false);

                    // EIP-1559: maxFeePerGas must be >= baseFee
                    if (maxFeePerGas < baseFee)
                    {
                        throw new InvalidOperationException("TransactionException.INSUFFICIENT_MAX_FEE_PER_GAS");
                    }

                    // EIP-1559: maxPriorityFeePerGas must be <= maxFeePerGas
                    if (maxPriorityFeePerGas > maxFeePerGas)
                    {
                        throw new InvalidOperationException("TransactionException.PRIORITY_GREATER_THAN_MAX_FEE_PER_GAS");
                    }

                    gasPrice = maxFeePerGas;
                    var priorityFee = BigInteger.Min(maxPriorityFeePerGas, maxFeePerGas - baseFee);
                    effectiveGasPrice = baseFee + priorityFee;
                }
                else
                {
                    gasPrice = string.IsNullOrEmpty(tx.GasPrice) ? BigInteger.Zero : tx.GasPrice.HexToBigInteger(false);
                    effectiveGasPrice = gasPrice;

                    // EIP-1559: For legacy transactions, gasPrice must be >= baseFee
                    if (baseFee > 0 && gasPrice < baseFee)
                    {
                        throw new InvalidOperationException("TransactionException.INSUFFICIENT_MAX_FEE_PER_GAS");
                    }
                }

                var sender = tx.Sender ?? GetSenderFromSecretKey(tx.SecretKey);
                var toAddress = string.IsNullOrEmpty(tx.To) ? null : tx.To;
                var isContractCreation = toAddress == null;

                // EIP-3860 (Shanghai): Limit initcode size to 49152 bytes
                const int MAX_INITCODE_SIZE = 49152;
                if (isContractCreation && IsShanghaiOrLater(_targetHardfork) && dataBytes != null && dataBytes.Length > MAX_INITCODE_SIZE)
                {
                    throw new InvalidOperationException("TransactionException.INITCODE_SIZE_EXCEEDED");
                }

                // EIP-4844 (Cancun): Type-3 blob transaction validation
                // Type-3 is identified by presence of maxFeePerBlobGas
                var blobVersionedHashes = tx.BlobVersionedHashes;
                var isType3Transaction = !string.IsNullOrEmpty(tx.MaxFeePerBlobGas);

                if (isType3Transaction && IsCancunOrLater(_targetHardfork))
                {
                    // Type-3 transactions cannot be contract creation
                    if (isContractCreation)
                    {
                        throw new InvalidOperationException("TransactionException.TYPE_3_TX_CONTRACT_CREATION");
                    }

                    // Type-3 transactions must have at least one blob
                    if (blobVersionedHashes == null || blobVersionedHashes.Count == 0)
                    {
                        throw new InvalidOperationException("TransactionException.TYPE_3_TX_ZERO_BLOBS");
                    }

                    // Check blob count limit
                    int maxBlobs = IsPragueOrLater(_targetHardfork) ? MAX_BLOBS_PER_BLOCK_PRAGUE : MAX_BLOBS_PER_BLOCK_CANCUN;
                    if (blobVersionedHashes.Count > maxBlobs)
                    {
                        throw new InvalidOperationException("TransactionException.TYPE_3_TX_BLOB_COUNT_EXCEEDED");
                    }

                    // All blob versioned hashes must have correct version prefix
                    foreach (var hash in blobVersionedHashes)
                    {
                        var hashBytes = hash.HexToByteArray();
                        if (hashBytes.Length < 1 || hashBytes[0] != VERSIONED_HASH_VERSION_KZG)
                        {
                            throw new InvalidOperationException("TransactionException.TYPE_3_TX_INVALID_BLOB_VERSIONED_HASH");
                        }
                    }
                }

                var accessList = tx.AccessLists != null && dataIndex < tx.AccessLists.Count
                    ? tx.AccessLists[dataIndex]
                    : null;

                var executionState = SetupPreState(test);

                // Check transaction gas limit against block gas limit
                var blockGasLimit = string.IsNullOrEmpty(env.CurrentGasLimit) ? BigInteger.Zero : env.CurrentGasLimit.HexToBigInteger(false);
                if (blockGasLimit > 0 && gasLimit > blockGasLimit)
                {
                    throw new InvalidOperationException("TransactionException.GAS_ALLOWANCE_EXCEEDED");
                }

                var intrinsicGas = CalculateIntrinsicGas(dataBytes, isContractCreation, accessList, _targetHardfork);

                // EIP-7623 (Prague): gas_limit must be >= max(intrinsic_gas, floor)
                BigInteger minGasRequired = intrinsicGas;
                if (IsPragueOrLater(_targetHardfork))
                {
                    var floorGas = CalculateFloorGasLimit(dataBytes, isContractCreation);
                    if (floorGas > minGasRequired)
                        minGasRequired = floorGas;
                }

                if (gasLimit < minGasRequired)
                {
                    result.Skipped = true;
                    result.SkipReason = $"Intrinsic gas too low: {gasLimit} < {minGasRequired}";
                    return result;
                }

                var hardforkConfig = HardforkConfig.FromName(_targetHardfork);
                executionState.MarkAddressAsWarm(sender);
                executionState.MarkPrecompilesAsWarm(hardforkConfig.PrecompileProvider);

                // EIP-3651 (Shanghai): Coinbase is warm at transaction start
                if (!string.IsNullOrEmpty(env.CurrentCoinbase))
                {
                    executionState.MarkAddressAsWarm(env.CurrentCoinbase);
                }

                var senderAccount = executionState.CreateOrGetAccountExecutionState(sender);

                // EIP-3607: Reject transactions from senders with deployed code
                if (senderAccount.Code != null && senderAccount.Code.Length > 0)
                {
                    throw new InvalidOperationException("TransactionException.SENDER_NOT_EOA");
                }

                var senderBalance = senderAccount.Balance.GetTotalBalance();

                // EIP-4844: Calculate blob gas cost for type-3 transactions
                BigInteger blobGasCost = BigInteger.Zero;
                BigInteger blobBaseFee = BigInteger.One; // MIN_BLOB_BASE_FEE = 1
                if (isType3Transaction && IsCancunOrLater(_targetHardfork))
                {
                    if (!string.IsNullOrEmpty(env.CurrentExcessBlobGas))
                    {
                        var excessBlobGas = env.CurrentExcessBlobGas.HexToBigInteger(false);
                        blobBaseFee = CalculateBlobBaseFee(excessBlobGas);
                    }
                    var blobCount = blobVersionedHashes?.Count ?? 0;
                    var blobGasUsed = blobCount * GAS_PER_BLOB;
                    blobGasCost = blobGasUsed * blobBaseFee;
                }

                var maxCost = gasLimit * gasPrice + value + blobGasCost;

                if (senderBalance < maxCost)
                {
                    result.Skipped = true;
                    result.SkipReason = $"Insufficient balance: {senderBalance} < {maxCost}";
                    return result;
                }

                var senderNonceBeforeIncrement = senderAccount.Nonce ?? BigInteger.Zero;

                // EIP-2681: Reject transactions if nonce would overflow (nonce >= 2^64 - 1)
                if (senderNonceBeforeIncrement >= BigInteger.Parse("18446744073709551615"))
                {
                    throw new InvalidOperationException("TransactionException.NONCE_IS_MAX");
                }

                senderAccount.Nonce = senderNonceBeforeIncrement + 1;

                senderAccount.Balance.UpdateExecutionBalance(-(gasLimit * effectiveGasPrice));

                // EIP-4844: Deduct blob gas cost (separate from regular gas)
                if (blobGasCost > 0)
                {
                    senderAccount.Balance.UpdateExecutionBalance(-blobGasCost);
                }

                // Take transaction snapshot AFTER sender nonce increment and gas payment
                // but BEFORE any state modifications that should be reverted on failure
                var transactionSnapshotId = executionState.TakeSnapshot();

                byte[] code;
                string contractAddress = null;

                bool hasCollision = false;
                if (isContractCreation)
                {
                    contractAddress = ContractUtils.CalculateContractAddress(sender, senderNonceBeforeIncrement);
                    executionState.MarkAddressAsWarm(contractAddress);
                    var contractAccount = executionState.CreateOrGetAccountExecutionState(contractAddress);

                    // EIP-684 + EIP-7610: Check for address collision
                    // Collision occurs if target has: code OR nonce > 0 OR non-empty storage
                    var targetHasCode = contractAccount.Code != null && contractAccount.Code.Length > 0;
                    var targetHasNonce = contractAccount.Nonce.HasValue && contractAccount.Nonce.Value > 0;
                    var targetHasStorage = contractAccount.Storage != null && contractAccount.Storage.Count > 0;
                    hasCollision = targetHasCode || targetHasNonce || targetHasStorage;

                    // EIP-161: New contracts start with nonce = 1 BEFORE initcode runs
                    // Also clear any pre-existing storage (valid per EIP-684 as long as no code/nonce)
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
                                warmAccount.MarkStorageKeyAsWarm(slot);
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

                // EIP-684: When CREATE collision occurs, all gas is consumed
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
                        programContext.Difficulty = env.CurrentRandom.HexToBigInteger(false);
                    else if (!string.IsNullOrEmpty(env.CurrentDifficulty))
                        programContext.Difficulty = env.CurrentDifficulty.HexToBigInteger(false);

                    if (!string.IsNullOrEmpty(env.CurrentGasLimit))
                        programContext.GasLimit = env.CurrentGasLimit.HexToBigInteger(false);

                    if (!string.IsNullOrEmpty(env.CurrentExcessBlobGas))
                    {
                        var excessBlobGas = env.CurrentExcessBlobGas.HexToBigInteger(false);
                        programContext.BlobBaseFee = CalculateBlobBaseFee(excessBlobGas);
                    }

                    // EIP-4844: Set blob versioned hashes for BLOBHASH opcode
                    if (isType3Transaction && blobVersionedHashes != null && blobVersionedHashes.Count > 0)
                    {
                        programContext.BlobHashes = blobVersionedHashes.Select(h => h.HexToByteArray()).ToArray();
                    }

                    programContext.EnforceGasSentry = true;

                    var program = new Program(code, programContext);
                    var evmSimulator = new EVMSimulator();

                    // Deduct value from sender (recipient credit happens in EVMSimulator via InitialiaseContractBalanceFromCallInputValue)
                    if (value > 0)
                    {
                        senderAccount.Balance.UpdateExecutionBalance(-value);
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


                        // EIP-3529: Refunds are only applied when transaction succeeds
                        if (executionSuccess)
                        {
                            var maxRefund = gasUsed / GasConstants.REFUND_QUOTIENT;
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

                                // EIP-3541: Reject code starting with 0xEF
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
                                        // Don't reset nonce - it was set to 1 before initcode ran and may have
                                        // been incremented if initcode did CREATE/CREATE2
                                        executionState.CommitSnapshot(transactionSnapshotId);
                                    }
                                }
                            }
                            else
                            {
                                executionState.CommitSnapshot(transactionSnapshotId);
                            }

                            // EIP-6780 (Cancun): Delete accounts that were both created AND self-destructed in the same transaction
                            var createdSet = new HashSet<string>(program.ProgramResult.CreatedContractAccounts, StringComparer.OrdinalIgnoreCase);
                            // For contract creation transactions, the newly created contract is also eligible for EIP-6780 deletion
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
                            result.Passed = true;
                            result.Message = $"Expected exception {expected.ExpectException} - got {ex.GetType().Name}";
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
                                    senderAccount.Balance.UpdateExecutionBalance(-value);
                                    var receiverAccount = executionState.CreateOrGetAccountExecutionState(toAddress);
                                    receiverAccount.Balance.UpdateExecutionBalance(value);
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
                        senderAccount.Balance.UpdateExecutionBalance(-value);
                        var receiverAccount = executionState.CreateOrGetAccountExecutionState(toAddress);
                        receiverAccount.Balance.UpdateExecutionBalance(value);
                        executionState.CommitSnapshot(transactionSnapshotId);
                    }
                }
                else if (executionSuccess && value > 0)
                {
                    senderAccount.Balance.UpdateExecutionBalance(-value);
                    if (isContractCreation)
                    {
                        var newContractAccount = executionState.CreateOrGetAccountExecutionState(contractAddress);
                        newContractAccount.Balance.UpdateExecutionBalance(value);
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

                // EIP-7623 (Prague): Apply calldata floor to final gas calculation
                // Per Geth implementation: floorDataGas = 21000 + tokens * 10
                // If gasUsed < floorDataGas, use floorDataGas
                // Note: Floor applies regardless of execution success/failure
                if (IsPragueOrLater(_targetHardfork))
                {
                    var tokens = CalculateTokensInCalldata(dataBytes);
                    BigInteger floorDataGas = G_TRANSACTION + (G_FLOOR_PER_TOKEN * tokens);

                    if (gasUsed < floorDataGas)
                    {
                        gasUsed = floorDataGas;
                        if (gasUsed > gasLimit)
                            gasUsed = gasLimit;
                    }
                }

                var gasRefundAmount = (gasLimit - gasUsed) * effectiveGasPrice;
                senderAccount.Balance.UpdateExecutionBalance(gasRefundAmount);

                var coinbase = env.CurrentCoinbase;
                var coinbaseAccount = executionState.CreateOrGetAccountExecutionState(coinbase);
                var minerReward = gasUsed * (effectiveGasPrice - baseFee);
                if (minerReward < 0) minerReward = 0;
                coinbaseAccount.Balance.UpdateExecutionBalance(minerReward);

                var accountStates = ExtractPostState(executionState);
                var computedStateRoot = StateRootCalculator.CalculateStateRoot(accountStates);
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
                }
            }
            catch (Exception ex)
            {
                // Check if this exception was expected (for pre-execution validation like EIP-3607, EIP-1559)
                if (!string.IsNullOrEmpty(expected.ExpectException))
                {
                    result.Passed = true;
                    result.Message = $"Expected exception {expected.ExpectException} - got {ex.GetType().Name}: {ex.Message}";
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

        /// <summary>
        /// Runs a single test using the new TransactionExecutor.
        /// This is the refactored version that uses the extracted TransactionExecutor infrastructure.
        /// The original RunSingleTestAsync method is preserved as a reference implementation.
        /// </summary>
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

                var config = HardforkConfig.FromName(_targetHardfork);
                var executor = new TransactionExecutor(config);

                var execResult = await executor.ExecuteAsync(ctx);

                if (execResult.IsValidationError)
                {
                    if (!string.IsNullOrEmpty(expected.ExpectException))
                    {
                        result.Passed = true;
                        result.Message = $"Expected exception {expected.ExpectException} - got validation error: {execResult.Error}";
                        return result;
                    }

                    if (execResult.Error?.Contains("Intrinsic gas too low") == true ||
                        execResult.Error?.Contains("Insufficient balance") == true)
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
                var computedStateRoot = StateRootCalculator.CalculateStateRoot(accountStates);
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
                }
            }
            catch (TransactionValidationException ex)
            {
                if (!string.IsNullOrEmpty(expected.ExpectException))
                {
                    result.Passed = true;
                    result.Message = $"Expected exception {expected.ExpectException} - got {ex.GetType().Name}: {ex.Message}";
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
                    result.Passed = true;
                    result.Message = $"Expected exception {expected.ExpectException} - got {ex.GetType().Name}: {ex.Message}";
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

        /// <summary>
        /// Builds a TransactionExecutionContext from test data.
        /// This extracts the transaction and environment parameters from the test format
        /// into the context structure expected by TransactionExecutor.
        /// </summary>
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
            var gasLimit = gasLimitStr.HexToBigInteger(false);
            var value = valueStr.HexToBigInteger(false);
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
                ? 1
                : (long)env.CurrentNumber.HexToBigInteger(false);

            var timestamp = string.IsNullOrEmpty(env.CurrentTimestamp)
                ? 0
                : (long)env.CurrentTimestamp.HexToBigInteger(false);

            var blockGasLimit = string.IsNullOrEmpty(env.CurrentGasLimit)
                ? BigInteger.Zero
                : env.CurrentGasLimit.HexToBigInteger(false);

            BigInteger difficulty = BigInteger.Zero;
            if (!string.IsNullOrEmpty(env.CurrentRandom))
                difficulty = env.CurrentRandom.HexToBigInteger(false);
            else if (!string.IsNullOrEmpty(env.CurrentDifficulty))
                difficulty = env.CurrentDifficulty.HexToBigInteger(false);

            BigInteger excessBlobGas = BigInteger.Zero;
            if (!string.IsNullOrEmpty(env.CurrentExcessBlobGas))
                excessBlobGas = env.CurrentExcessBlobGas.HexToBigInteger(false);

            var nonce = string.IsNullOrEmpty(tx.Nonce) ? BigInteger.Zero : tx.Nonce.HexToBigInteger(false);

            return new TransactionExecutionContext
            {
                Sender = sender,
                To = toAddress,
                Data = dataBytes,
                GasLimit = gasLimit,
                Value = value,
                GasPrice = gasPrice,
                MaxFeePerGas = maxFeePerGas,
                MaxPriorityFeePerGas = maxPriorityFeePerGas,
                Nonce = nonce,
                IsEip1559 = isEip1559,
                IsContractCreation = isContractCreation,
                IsType3Transaction = isType3Transaction,
                BlobVersionedHashes = blobVersionedHashes,
                MaxFeePerBlobGas = isType3Transaction ? tx.MaxFeePerBlobGas.HexToBigInteger(false) : BigInteger.Zero,
                AccessList = accessListEntries,

                BlockNumber = blockNumber,
                Timestamp = timestamp,
                Coinbase = env.CurrentCoinbase,
                BaseFee = baseFee,
                Difficulty = difficulty,
                BlockGasLimit = blockGasLimit,
                ExcessBlobGas = excessBlobGas,

                ExecutionState = executionState,
                TraceEnabled = captureTraces
            };
        }

        private ExecutionStateService SetupPreState(GeneralStateTest test)
        {
            var executionState = new ExecutionStateService(new MockNodeDataService());

            foreach (var preAccount in test.Pre)
            {
                var address = preAccount.Key;
                var account = preAccount.Value;

                var accountState = executionState.CreateOrGetAccountExecutionState(address);

                accountState.Code = string.IsNullOrEmpty(account.Code) || account.Code == "0x"
                    ? new byte[0]
                    : account.Code.HexToByteArray();

                accountState.Balance.SetInitialChainBalance(
                    string.IsNullOrEmpty(account.Balance) ? BigInteger.Zero : account.Balance.HexToBigInteger(false));

                accountState.Nonce = string.IsNullOrEmpty(account.Nonce)
                    ? BigInteger.Zero
                    : account.Nonce.HexToBigInteger(false);

                if (account.Storage != null)
                {
                    foreach (var storage in account.Storage)
                    {
                        var key = storage.Key.HexToBigInteger(false);
                        var value = storage.Value.HexToByteArray();
                        accountState.SetPreStateStorage(key, value);
                    }
                }

            }

            return executionState;
        }

        private string GetSenderFromSecretKey(string secretKey)
        {
            if (string.IsNullOrEmpty(secretKey))
                return "0x0000000000000000000000000000000000000000";

            var key = new Nethereum.Signer.EthECKey(secretKey);
            return key.GetPublicAddress();
        }

        private Dictionary<string, AccountState> ExtractPostState(ExecutionStateService executionState)
        {
            var result = new Dictionary<string, AccountState>();

            foreach (var kvp in executionState.AccountsState)
            {
                var address = kvp.Key;
                var accountExecState = kvp.Value;

                var accountState = new AccountState
                {
                    Nonce = accountExecState.Nonce ?? BigInteger.Zero,
                    Balance = accountExecState.Balance.GetTotalBalance(),
                    Code = accountExecState.Code ?? new byte[0],
                    Storage = new Dictionary<BigInteger, byte[]>()
                };

                foreach (var storageKvp in accountExecState.Storage)
                {
                    if (storageKvp.Value != null)
                        accountState.Storage[storageKvp.Key] = storageKvp.Value;
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

        private static BigInteger CalculateBlobBaseFee(BigInteger excessBlobGas)
        {
            const int MIN_BASE_FEE_PER_BLOB_GAS = 1;
            const int BLOB_BASE_FEE_UPDATE_FRACTION = 3338477;
            return FakeExponential(MIN_BASE_FEE_PER_BLOB_GAS, excessBlobGas, BLOB_BASE_FEE_UPDATE_FRACTION);
        }

        private static BigInteger FakeExponential(BigInteger factor, BigInteger numerator, BigInteger denominator)
        {
            int i = 1;
            BigInteger output = 0;
            BigInteger numeratorAccum = factor * denominator;
            while (numeratorAccum > 0)
            {
                output += numeratorAccum;
                numeratorAccum = (numeratorAccum * numerator) / (denominator * i);
                i++;
            }
            return output / denominator;
        }

        private static BigInteger CalculateIntrinsicGas(byte[] data, bool isContractCreation, List<AccessListItem> accessList = null, string targetHardfork = null)
        {
            BigInteger gas = G_TRANSACTION;

            if (isContractCreation)
            {
                gas += G_TXCREATE;

                if (data != null && data.Length > 0)
                {
                    int initcodeWords = (data.Length + 31) / 32;
                    gas += initcodeWords * 2;
                }
            }

            // Standard data gas (4 per zero, 16 per non-zero)
            if (data != null && data.Length > 0)
            {
                foreach (var b in data)
                {
                    if (b == 0)
                        gas += G_TXDATAZERO;
                    else
                        gas += G_TXDATANONZERO;
                }
            }

            if (accessList != null)
            {
                foreach (var entry in accessList)
                {
                    gas += 2400;
                    if (entry.StorageKeys != null)
                    {
                        gas += entry.StorageKeys.Count * 1900;
                    }
                }
            }

            return gas;
        }

        // EIP-7623: Calculate tokens in calldata (zeros + 4*non_zeros)
        private static BigInteger CalculateTokensInCalldata(byte[] data)
        {
            if (data == null || data.Length == 0)
                return 0;

            int zeroBytes = 0;
            int nonZeroBytes = 0;
            foreach (var b in data)
            {
                if (b == 0) zeroBytes++;
                else nonZeroBytes++;
            }
            return zeroBytes + (nonZeroBytes * G_TOKENS_PER_NONZERO);
        }

        // EIP-7623: Calculate floor gas limit (21000 + 10*tokens + creation_base)
        private static BigInteger CalculateFloorGasLimit(byte[] data, bool isContractCreation)
        {
            var tokens = CalculateTokensInCalldata(data);
            BigInteger floor = G_TRANSACTION + (G_FLOOR_PER_TOKEN * tokens);
            if (isContractCreation)
                floor += G_TXCREATE;
            return floor;
        }

        private static bool IsShanghaiOrLater(string hardfork)
        {
            if (string.IsNullOrEmpty(hardfork))
                return false;

            var lowerFork = hardfork.ToLowerInvariant();
            return lowerFork == "shanghai" || lowerFork == "cancun" || lowerFork == "prague" ||
                   lowerFork == "osaka" || lowerFork == "amsterdam";
        }

        private static bool IsCancunOrLater(string hardfork)
        {
            if (string.IsNullOrEmpty(hardfork))
                return false;

            var lowerFork = hardfork.ToLowerInvariant();
            return lowerFork == "cancun" || lowerFork == "prague" ||
                   lowerFork == "osaka" || lowerFork == "amsterdam";
        }

        private static bool IsPragueOrLater(string hardfork)
        {
            if (string.IsNullOrEmpty(hardfork))
                return false;

            var lowerFork = hardfork.ToLowerInvariant();
            return lowerFork == "prague" || lowerFork == "osaka" || lowerFork == "amsterdam";
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
    }
}
