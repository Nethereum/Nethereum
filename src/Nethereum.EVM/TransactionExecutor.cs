using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Execution;
using Nethereum.EVM.Gas;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Signer;
using Nethereum.Util;

namespace Nethereum.EVM
{
    public class TransactionExecutor
    {
        private const byte VERSIONED_HASH_VERSION_KZG = 0x01;
        private const int MAX_INITCODE_SIZE = 49152;
        private const int MAX_CODE_SIZE = 24576;
        private static readonly byte[] DELEGATION_PREFIX = new byte[] { 0xef, 0x01, 0x00 };
        private const int PER_AUTH_BASE_COST = 12500;
        private const int PER_EMPTY_ACCOUNT_COST = 25000;

        private readonly EVMSimulator _evmSimulator;
        private readonly HardforkConfig _config;
        private readonly IPrecompileProvider _precompileProvider;

        public TransactionExecutor(HardforkConfig config = null, EVMSimulator evmSimulator = null, IPrecompileProvider customPrecompileProvider = null)
        {
            _config = config ?? HardforkConfig.Default;

            _precompileProvider = customPrecompileProvider != null
                ? new CompositePrecompileProvider(_config.PrecompileProvider, customPrecompileProvider)
                : _config.PrecompileProvider;

            if (evmSimulator != null)
            {
                _evmSimulator = evmSimulator;
            }
            else
            {
                var precompileHandler = new EvmPreCompiledContractsExecution(_precompileProvider);
                var programExecution = new EvmProgramExecution(precompileHandler);
                _evmSimulator = new EVMSimulator(programExecution);
            }
        }

        public async Task<TransactionExecutionResult> ExecuteAsync(TransactionExecutionContext ctx)
        {
            var result = new TransactionExecutionResult();

            try
            {
                ValidateTransaction(ctx, result);
                if (result.IsValidationError)
                    return result;

                await SetupStateAsync(ctx, result);
                if (result.IsValidationError)
                    return result;

                await ExecuteTransaction(ctx, result);

                FinalizeTransaction(ctx, result);
            }
            catch (TransactionValidationException ex)
            {
                result.Success = false;
                result.Error = ex.Message;
                result.IsValidationError = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
            }

            return result;
        }

        private void ValidateTransaction(TransactionExecutionContext ctx, TransactionExecutionResult result)
        {
            // EIP-1559 validation (always enabled - post-London)
            if (ctx.IsEip1559)
            {
                if (ctx.MaxFeePerGas < ctx.BaseFee)
                    throw new TransactionValidationException("INSUFFICIENT_MAX_FEE_PER_GAS");

                if (ctx.MaxPriorityFeePerGas > ctx.MaxFeePerGas)
                    throw new TransactionValidationException("PRIORITY_GREATER_THAN_MAX_FEE_PER_GAS");

                var priorityFee = BigInteger.Min(ctx.MaxPriorityFeePerGas, ctx.MaxFeePerGas - ctx.BaseFee);
                ctx.EffectiveGasPrice = ctx.BaseFee + priorityFee;
            }
            else
            {
                ctx.EffectiveGasPrice = ctx.GasPrice;

                if (ctx.BaseFee > 0 && ctx.GasPrice < ctx.BaseFee)
                    throw new TransactionValidationException("INSUFFICIENT_MAX_FEE_PER_GAS");
            }

            // EIP-3860: Initcode size limit (always enabled - post-Shanghai)
            if (ctx.IsContractCreation && ctx.Data != null && ctx.Data.Length > MAX_INITCODE_SIZE)
                throw new TransactionValidationException("INITCODE_SIZE_EXCEEDED");

            // EIP-4844: Blob transaction validation (configurable)
            if (_config.EnableEIP4844)
                ValidateBlobTransaction(ctx);

            if (ctx.BlockGasLimit > 0 && ctx.GasLimit > ctx.BlockGasLimit)
                throw new TransactionValidationException("GAS_ALLOWANCE_EXCEEDED");

            ctx.IntrinsicGas = IntrinsicGasCalculator.CalculateIntrinsicGas(ctx.Data, ctx.IsContractCreation, ctx.AccessList);

            // EIP-7702: Authorization list gas (configurable - Prague)
            if (_config.EnableEIP7702 && ctx.AuthorisationList != null && ctx.AuthorisationList.Count > 0)
            {
                ctx.IntrinsicGas += ctx.AuthorisationList.Count * PER_AUTH_BASE_COST;
            }

            // EIP-7623: Floor gas (configurable - Prague)
            ctx.MinGasRequired = ctx.IntrinsicGas;
            if (_config.EnableEIP7623)
            {
                ctx.FloorGas = IntrinsicGasCalculator.CalculateFloorGasLimit(ctx.Data, ctx.IsContractCreation);
                if (ctx.FloorGas > ctx.MinGasRequired)
                    ctx.MinGasRequired = ctx.FloorGas;
            }

            if (ctx.GasLimit < ctx.MinGasRequired)
            {
                result.IsValidationError = true;
                result.Error = $"Intrinsic gas too low: {ctx.GasLimit} < {ctx.MinGasRequired}";
                return;
            }
        }

        private void ValidateBlobTransaction(TransactionExecutionContext ctx)
        {
            if (!ctx.IsType3Transaction)
                return;

            if (ctx.IsContractCreation)
                throw new TransactionValidationException("TYPE_3_TX_CONTRACT_CREATION");

            if (ctx.BlobVersionedHashes == null || ctx.BlobVersionedHashes.Count == 0)
                throw new TransactionValidationException("TYPE_3_TX_ZERO_BLOBS");

            if (ctx.BlobVersionedHashes.Count > _config.MaxBlobsPerBlock)
                throw new TransactionValidationException("TYPE_3_TX_BLOB_COUNT_EXCEEDED");

            foreach (var hash in ctx.BlobVersionedHashes)
            {
                var hashBytes = hash.HexToByteArray();
                if (hashBytes.Length < 1 || hashBytes[0] != VERSIONED_HASH_VERSION_KZG)
                    throw new TransactionValidationException("TYPE_3_TX_INVALID_BLOB_VERSIONED_HASH");
            }
        }

        private async Task SetupStateAsync(TransactionExecutionContext ctx, TransactionExecutionResult result)
        {
            ctx.ExecutionState.MarkAddressAsWarm(ctx.Sender);
            ctx.ExecutionState.MarkPrecompilesAsWarm(_precompileProvider);

            // EIP-3651: Warm coinbase (always enabled - post-Shanghai)
            if (!string.IsNullOrEmpty(ctx.Coinbase))
                ctx.ExecutionState.MarkAddressAsWarm(ctx.Coinbase);

            ctx.SenderAccount = ctx.ExecutionState.CreateOrGetAccountExecutionState(ctx.Sender);

            // Load sender's nonce from storage if not already loaded
            if (ctx.SenderAccount.Nonce == null)
            {
                ctx.SenderAccount.Nonce = await ctx.ExecutionState.NodeDataService.GetTransactionCount(ctx.Sender);
            }

            // EIP-3607: Sender must be EOA (always enabled - post-London)
            // EIP-7702: Delegated EOAs are allowed (they have 0xef0100 + address code)
            var senderCode = await ctx.ExecutionState.GetCodeAsync(ctx.Sender);
            if (senderCode != null && senderCode.Length > 0)
            {
                if (!IsDelegatedCode(senderCode))
                    throw new TransactionValidationException("SENDER_NOT_EOA");
            }

            // EIP-7702: Process authorization list before execution
            if (_config.EnableEIP7702 && ctx.AuthorisationList != null && ctx.AuthorisationList.Count > 0)
            {
                await ProcessAuthorizationListAsync(ctx, result);
                if (result.IsValidationError)
                    return;
            }

            var senderBalance = ctx.SenderAccount.Balance.GetTotalBalance();

            // EIP-4844: Blob gas cost
            ctx.BlobGasCost = BigInteger.Zero;
            ctx.BlobBaseFee = BigInteger.One;
            if (ctx.IsType3Transaction && _config.EnableEIP4844)
            {
                ctx.BlobBaseFee = IntrinsicGasCalculator.CalculateBlobBaseFee(ctx.ExcessBlobGas);
                var blobCount = ctx.BlobVersionedHashes?.Count ?? 0;
                ctx.BlobGasCost = IntrinsicGasCalculator.CalculateBlobGasCost(blobCount, ctx.BlobBaseFee);
            }

            var maxCost = ctx.GasLimit * (ctx.IsEip1559 ? ctx.MaxFeePerGas : ctx.GasPrice) + ctx.Value + ctx.BlobGasCost;

            if (senderBalance < maxCost)
            {
                result.IsValidationError = true;
                result.Error = $"Insufficient balance: {senderBalance} < {maxCost}";
                return;
            }

            ctx.SenderNonceBeforeIncrement = ctx.SenderAccount.Nonce ?? BigInteger.Zero;

            // EIP-2681: Nonce overflow (always enabled)
            if (ctx.SenderNonceBeforeIncrement >= BigInteger.Parse("18446744073709551615"))
                throw new TransactionValidationException("NONCE_IS_MAX");

            ctx.SenderAccount.Nonce = ctx.SenderNonceBeforeIncrement + 1;
            ctx.SenderAccount.Balance.UpdateExecutionBalance(-(ctx.GasLimit * ctx.EffectiveGasPrice));

            if (ctx.BlobGasCost > 0)
                ctx.SenderAccount.Balance.UpdateExecutionBalance(-ctx.BlobGasCost);

            ctx.TransactionSnapshotId = ctx.ExecutionState.TakeSnapshot();

            await SetupTargetAccountAsync(ctx);
            ProcessAccessList(ctx);
        }

        private async Task SetupTargetAccountAsync(TransactionExecutionContext ctx)
        {
            ctx.HasCollision = false;

            if (ctx.IsContractCreation)
            {
                ctx.ContractAddress = ContractUtils.CalculateContractAddress(ctx.Sender, ctx.SenderNonceBeforeIncrement);
                ctx.ExecutionState.MarkAddressAsWarm(ctx.ContractAddress);

                // Load existing account state to check for collision
                var contractCode = await ctx.ExecutionState.GetCodeAsync(ctx.ContractAddress);
                var contractNonce = await ctx.ExecutionState.GetNonceAsync(ctx.ContractAddress);
                var contractAccount = ctx.ExecutionState.CreateOrGetAccountExecutionState(ctx.ContractAddress);

                // EIP-684 + EIP-7610: Collision detection (always enabled)
                var targetHasCode = contractCode != null && contractCode.Length > 0;
                var targetHasNonce = contractNonce > 0;
                var targetHasStorage = contractAccount.Storage != null && contractAccount.Storage.Count > 0;
                ctx.HasCollision = targetHasCode || targetHasNonce || targetHasStorage;

                // EIP-161: Contract nonce = 1 (always enabled)
                if (!ctx.HasCollision)
                    ctx.ExecutionState.PrepareNewContractAccount(ctx.ContractAddress);

                ctx.Code = ctx.HasCollision ? null : ctx.Data;
            }
            else if (!string.IsNullOrEmpty(ctx.To))
            {
                ctx.ExecutionState.MarkAddressAsWarm(ctx.To);
                // Load code from underlying storage
                ctx.Code = await ctx.ExecutionState.GetCodeAsync(ctx.To);
            }
        }

        private void ProcessAccessList(TransactionExecutionContext ctx)
        {
            if (ctx.AccessList == null)
                return;

            foreach (var entry in ctx.AccessList)
            {
                ctx.ExecutionState.MarkAddressAsWarm(entry.Address);
                var warmAccount = ctx.ExecutionState.CreateOrGetAccountExecutionState(entry.Address);
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

        private static bool IsDelegatedCode(byte[] code)
        {
            if (code == null || code.Length != 23)
                return false;

            return code[0] == DELEGATION_PREFIX[0] &&
                   code[1] == DELEGATION_PREFIX[1] &&
                   code[2] == DELEGATION_PREFIX[2];
        }

        private static byte[] CreateDelegationCode(string address)
        {
            var addressBytes = address.HexToByteArray();
            var code = new byte[23];
            code[0] = DELEGATION_PREFIX[0];
            code[1] = DELEGATION_PREFIX[1];
            code[2] = DELEGATION_PREFIX[2];
            Array.Copy(addressBytes, 0, code, 3, 20);
            return code;
        }

        private async Task ProcessAuthorizationListAsync(TransactionExecutionContext ctx, TransactionExecutionResult result)
        {
            var processedAuthorities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var auth in ctx.AuthorisationList)
            {
                try
                {
                    // Recover the authority address from the signature
                    var authorityAddress = auth.RecoverSignerAddress();

                    // Skip duplicate authorities in the same transaction
                    if (processedAuthorities.Contains(authorityAddress))
                        continue;

                    processedAuthorities.Add(authorityAddress);

                    // EIP-7702: Validate chain ID (0 means valid on any chain)
                    if (auth.ChainId != 0 && auth.ChainId != ctx.ChainId)
                        continue;

                    // Mark authority address as warm
                    ctx.ExecutionState.MarkAddressAsWarm(authorityAddress);

                    // Get authority account state
                    var authorityAccount = ctx.ExecutionState.CreateOrGetAccountExecutionState(authorityAddress);

                    // Load current nonce if not already loaded
                    if (authorityAccount.Nonce == null)
                    {
                        authorityAccount.Nonce = await ctx.ExecutionState.NodeDataService.GetTransactionCount(authorityAddress);
                    }

                    // EIP-7702: Validate authorization nonce matches authority's current nonce
                    if (auth.Nonce != authorityAccount.Nonce)
                        continue;

                    // Check if authority already has code (and it's not delegated code)
                    var existingCode = await ctx.ExecutionState.GetCodeAsync(authorityAddress);
                    if (existingCode != null && existingCode.Length > 0 && !IsDelegatedCode(existingCode))
                        continue;

                    // Increment the authority's nonce
                    authorityAccount.Nonce = authorityAccount.Nonce + 1;

                    // Install delegation code: 0xef0100 + address
                    // If auth.Address is empty, remove delegation (set code to empty)
                    if (string.IsNullOrEmpty(auth.Address) || auth.Address == "0x" ||
                        auth.Address == "0x0000000000000000000000000000000000000000")
                    {
                        authorityAccount.Code = new byte[0];
                    }
                    else
                    {
                        authorityAccount.Code = CreateDelegationCode(auth.Address);
                    }

                    // Mark the delegate address as warm too
                    if (!string.IsNullOrEmpty(auth.Address) && auth.Address != "0x")
                    {
                        ctx.ExecutionState.MarkAddressAsWarm(auth.Address);
                    }
                }
                catch
                {
                    // Invalid signature or other error - skip this authorization
                    continue;
                }
            }
        }

        private async Task ExecuteTransaction(TransactionExecutionContext ctx, TransactionExecutionResult result)
        {
            result.GasUsed = ctx.IntrinsicGas;
            result.Success = !ctx.HasCollision;
            result.GasRefund = BigInteger.Zero;

            if (ctx.HasCollision)
            {
                result.GasUsed = ctx.GasLimit;
                ctx.ExecutionState.RevertToSnapshot(ctx.TransactionSnapshotId);
                result.Error = "ADDRESS_COLLISION";
                return;
            }

            if (ctx.Code != null && ctx.Code.Length > 0)
            {
                await ExecuteCode(ctx, result);
            }
            else if (!ctx.IsContractCreation && !string.IsNullOrEmpty(ctx.To))
            {
                await ExecutePrecompileOrTransfer(ctx, result);
            }
            else if (result.Success && ctx.Value > 0)
            {
                ExecuteSimpleTransfer(ctx, result);
            }
        }

        private async Task ExecuteCode(TransactionExecutionContext ctx, TransactionExecutionResult result)
        {
            var targetAddress = ctx.IsContractCreation ? ctx.ContractAddress : ctx.To;
            var transaction = new TransactionInput
            {
                From = ctx.Sender,
                To = targetAddress,
                Data = ctx.IsContractCreation ? "" : ctx.Data?.ToHex(true) ?? "",
                Gas = new Hex.HexTypes.HexBigInteger(ctx.GasLimit - ctx.IntrinsicGas),
                Value = new Hex.HexTypes.HexBigInteger(ctx.Value),
                Nonce = new Hex.HexTypes.HexBigInteger(ctx.Nonce),
                GasPrice = new Hex.HexTypes.HexBigInteger(ctx.EffectiveGasPrice),
                ChainId = new Hex.HexTypes.HexBigInteger(1)
            };

            var programContext = new ProgramContext(
                transaction,
                ctx.ExecutionState,
                null,
                blockNumber: ctx.BlockNumber,
                timestamp: ctx.Timestamp,
                coinbase: ctx.Coinbase,
                baseFee: (long)ctx.BaseFee,
                codeAddress: ctx.IsContractCreation ? ctx.ContractAddress : null
            );

            programContext.Difficulty = ctx.Difficulty;
            programContext.GasLimit = ctx.BlockGasLimit;
            programContext.BlobBaseFee = ctx.BlobBaseFee;

            if (ctx.IsType3Transaction && ctx.BlobVersionedHashes != null && ctx.BlobVersionedHashes.Count > 0)
                programContext.BlobHashes = ctx.BlobVersionedHashes.Select(h => h.HexToByteArray()).ToArray();

            programContext.EnforceGasSentry = true;

            var program = new Program(ctx.Code, programContext);

            if (ctx.Value > 0)
                ctx.SenderAccount.Balance.UpdateExecutionBalance(-ctx.Value);

            try
            {
                program = await _evmSimulator.ExecuteWithCallStackAsync(program, traceEnabled: ctx.TraceEnabled);

                if (ctx.TraceEnabled)
                    result.Traces = program.Trace;

                result.GasUsed = ctx.IntrinsicGas + program.TotalGasUsed;
                result.Success = !program.ProgramResult.IsRevert;
                result.ReturnData = program.ProgramResult.Result;

                // EIP-3529: Refund quotient = 5 (always enabled - post-London)
                if (result.Success)
                {
                    var maxRefund = IntrinsicGasCalculator.CalculateMaxRefund(result.GasUsed);
                    result.GasRefund = BigInteger.Min(program.RefundCounter, maxRefund);

                    var effectiveGasUsed = result.GasUsed - result.GasRefund;
                    if (effectiveGasUsed < ctx.IntrinsicGas)
                        effectiveGasUsed = ctx.IntrinsicGas;

                    result.GasUsed = effectiveGasUsed;
                }

                result.RevertReason = program.ProgramResult.GetRevertMessage();

                // Capture full program result for decoding (logs, inner calls, etc.)
                result.ProgramResult = program.ProgramResult;
                result.Logs = program.ProgramResult.Logs;
                result.InnerCalls = program.ProgramResult.InnerCalls;
                result.InnerContractCodeCalls = program.ProgramResult.InnerContractCodeCalls;

                if (result.Success)
                {
                    if (ctx.IsContractCreation)
                    {
                        HandleSuccessfulContractCreation(ctx, result, program);
                    }
                    else
                    {
                        ctx.ExecutionState.CommitSnapshot(ctx.TransactionSnapshotId);
                    }

                    // EIP-6780: SELFDESTRUCT cleanup (always enabled - post-Cancun)
                    CleanupSelfDestructedContracts(ctx, program);

                    result.CreatedAccounts = program.ProgramResult.CreatedContractAccounts.ToList();
                    result.DeletedAccounts = program.ProgramResult.DeletedContractAccounts.ToList();
                }
                else
                {
                    ctx.ExecutionState.RevertToSnapshot(ctx.TransactionSnapshotId);
                    if (program.GasRemaining == 0)
                        result.GasUsed = ctx.GasLimit;
                }
            }
            catch (Exception)
            {
                result.Success = false;
                ctx.ExecutionState.RevertToSnapshot(ctx.TransactionSnapshotId);
                throw;
            }
        }

        private void HandleSuccessfulContractCreation(
            TransactionExecutionContext ctx,
            TransactionExecutionResult result,
            Program program)
        {
            var deployedCode = program.ProgramResult.Result ?? new byte[0];

            // EIP-3541: Reject 0xEF prefix (always enabled - post-London)
            if (deployedCode.Length > 0 && deployedCode[0] == 0xEF)
            {
                result.Success = false;
                ctx.ExecutionState.RevertToSnapshot(ctx.TransactionSnapshotId);
                result.GasUsed = ctx.GasLimit;
                result.Error = "INVALID_EF_PREFIX";
                return;
            }

            var codeDepositGas = IntrinsicGasCalculator.CalculateCodeDepositGas(deployedCode.Length);

            if (result.GasUsed + codeDepositGas > ctx.GasLimit || deployedCode.Length > MAX_CODE_SIZE)
            {
                result.Success = false;
                ctx.ExecutionState.RevertToSnapshot(ctx.TransactionSnapshotId);
                result.GasUsed = ctx.GasLimit;
                result.Error = deployedCode.Length > MAX_CODE_SIZE ? "MAX_CODE_SIZE_EXCEEDED" : "OUT_OF_GAS";
                return;
            }

            result.GasUsed += codeDepositGas;
            var newContractAccount = ctx.ExecutionState.CreateOrGetAccountExecutionState(ctx.ContractAddress);
            newContractAccount.Code = deployedCode;
            ctx.ExecutionState.CommitSnapshot(ctx.TransactionSnapshotId);
            result.ContractAddress = ctx.ContractAddress;
        }

        private void CleanupSelfDestructedContracts(TransactionExecutionContext ctx, Program program)
        {
            var createdSet = new HashSet<string>(
                program.ProgramResult.CreatedContractAccounts,
                StringComparer.OrdinalIgnoreCase);

            if (ctx.IsContractCreation)
                createdSet.Add(ctx.ContractAddress);

            foreach (var deletedAddr in program.ProgramResult.DeletedContractAccounts)
            {
                if (createdSet.Contains(deletedAddr))
                    ctx.ExecutionState.DeleteAccount(deletedAddr);
            }
        }

        private async Task ExecutePrecompileOrTransfer(TransactionExecutionContext ctx, TransactionExecutionResult result)
        {
            if (_precompileProvider.CanHandle(ctx.To))
            {
                var precompileInput = ctx.Data ?? new byte[0];
                var precompileGasCost = _precompileProvider.GetGasCost(ctx.To, precompileInput);
                var availableGas = ctx.GasLimit - ctx.IntrinsicGas;

                if (precompileGasCost > availableGas)
                {
                    result.GasUsed = ctx.GasLimit;
                    result.Success = false;
                    result.Error = "PRECOMPILE_OUT_OF_GAS";
                    ctx.ExecutionState.RevertToSnapshot(ctx.TransactionSnapshotId);
                }
                else
                {
                    result.GasUsed = ctx.IntrinsicGas + (long)precompileGasCost;
                    try
                    {
                        result.ReturnData = _precompileProvider.Execute(ctx.To, precompileInput);
                        result.Success = true;

                        if (ctx.Value > 0)
                        {
                            ctx.SenderAccount.Balance.UpdateExecutionBalance(-ctx.Value);
                            var receiverAccount = ctx.ExecutionState.CreateOrGetAccountExecutionState(ctx.To);
                            receiverAccount.Balance.UpdateExecutionBalance(ctx.Value);
                        }

                        ctx.ExecutionState.CommitSnapshot(ctx.TransactionSnapshotId);
                    }
                    catch
                    {
                        result.GasUsed = ctx.GasLimit;
                        result.Success = false;
                        result.Error = "PRECOMPILE_FAILED";
                        ctx.ExecutionState.RevertToSnapshot(ctx.TransactionSnapshotId);
                    }
                }
            }
            else if (ctx.Value > 0)
            {
                ctx.SenderAccount.Balance.UpdateExecutionBalance(-ctx.Value);
                var receiverAccount = ctx.ExecutionState.CreateOrGetAccountExecutionState(ctx.To);
                receiverAccount.Balance.UpdateExecutionBalance(ctx.Value);
                ctx.ExecutionState.CommitSnapshot(ctx.TransactionSnapshotId);
            }
        }

        private void ExecuteSimpleTransfer(TransactionExecutionContext ctx, TransactionExecutionResult result)
        {
            ctx.SenderAccount.Balance.UpdateExecutionBalance(-ctx.Value);

            if (ctx.IsContractCreation)
            {
                var newContractAccount = ctx.ExecutionState.CreateOrGetAccountExecutionState(ctx.ContractAddress);
                newContractAccount.Balance.UpdateExecutionBalance(ctx.Value);
            }

            ctx.ExecutionState.CommitSnapshot(ctx.TransactionSnapshotId);
        }

        private void FinalizeTransaction(TransactionExecutionContext ctx, TransactionExecutionResult result)
        {
            if (result.GasUsed > ctx.GasLimit)
                result.GasUsed = ctx.GasLimit;

            // EIP-7623: Floor gas (configurable - Prague)
            if (_config.EnableEIP7623)
            {
                var tokens = IntrinsicGasCalculator.CalculateTokensInCalldata(ctx.Data);
                BigInteger floorDataGas = IntrinsicGasCalculator.G_TRANSACTION +
                    (IntrinsicGasCalculator.G_FLOOR_PER_TOKEN * tokens);

                if (result.GasUsed < floorDataGas)
                {
                    result.GasUsed = floorDataGas;
                    if (result.GasUsed > ctx.GasLimit)
                        result.GasUsed = ctx.GasLimit;
                }
            }

            result.EffectiveGasUsed = result.GasUsed;

            var gasRefundAmount = (ctx.GasLimit - result.GasUsed) * ctx.EffectiveGasPrice;
            ctx.SenderAccount.Balance.UpdateExecutionBalance(gasRefundAmount);

            var coinbaseAccount = ctx.ExecutionState.CreateOrGetAccountExecutionState(ctx.Coinbase);
            var minerReward = result.GasUsed * (ctx.EffectiveGasPrice - ctx.BaseFee);
            if (minerReward < 0) minerReward = 0;
            coinbaseAccount.Balance.UpdateExecutionBalance(minerReward);
        }
    }

    public class TransactionValidationException : Exception
    {
        public TransactionValidationException(string message) : base(message) { }
    }
}
