using System;
using System.Collections.Generic;
#if !EVM_SYNC
using System.Numerics;
using System.Threading.Tasks;
#endif
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Execution;
using Nethereum.EVM.Gas;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
#if EVM_SYNC
using Nethereum.EVM.Types;
#else
using Nethereum.RPC.Eth.DTOs;
#endif
#if !EVM_SYNC
using Nethereum.Signer;
#endif
using Nethereum.Util;

namespace Nethereum.EVM
{
    public partial class TransactionExecutor
    {
        private const int MAX_INITCODE_SIZE = 49152;

        private readonly EVMSimulator _evmSimulator;
        public EVMSimulator GetSimulator() => _evmSimulator;
        private readonly HardforkConfig _config;

        public TransactionExecutor(HardforkConfig config, EVMSimulator evmSimulator = null)
        {
            _config = config ?? throw new System.ArgumentNullException(nameof(config));
            _evmSimulator = evmSimulator ?? new EVMSimulator(_config);
        }


        private void ValidateTransaction(TransactionExecutionContext ctx, TransactionExecutionResult result)
        {
            // Skip gas price validation in Call mode (eth_call doesn't pay for gas)
            if (ctx.IsCallMode)
            {
                ctx.EffectiveGasPrice = EvmUInt256.Zero;
            }
            else if (ctx.IsEip1559)
            {
                if (ctx.MaxFeePerGas < ctx.BaseFee)
                    throw new TransactionValidationException("INSUFFICIENT_MAX_FEE_PER_GAS");

                if (ctx.MaxPriorityFeePerGas > ctx.MaxFeePerGas)
                    throw new TransactionValidationException("PRIORITY_GREATER_THAN_MAX_FEE_PER_GAS");

                var diff = ctx.MaxFeePerGas - ctx.BaseFee;
                var priorityFee = ctx.MaxPriorityFeePerGas < diff ? ctx.MaxPriorityFeePerGas : diff;
                ctx.EffectiveGasPrice = ctx.BaseFee + priorityFee;
            }
            else
            {
                ctx.EffectiveGasPrice = ctx.GasPrice;

                if (!ctx.BaseFee.IsZero && ctx.GasPrice < ctx.BaseFee)
                    throw new TransactionValidationException("INSUFFICIENT_MAX_FEE_PER_GAS");
            }

            // EIP-3860: Initcode size limit (Shanghai+)
            if (_config.MaxInitcodeSize > 0 && ctx.IsContractCreation && ctx.Data != null && ctx.Data.Length > _config.MaxInitcodeSize)
                throw new TransactionValidationException("INITCODE_SIZE_EXCEEDED");

            if (!ctx.IsCallMode && ctx.BlockGasLimit > 0 && ctx.GasLimit > ctx.BlockGasLimit)
                throw new TransactionValidationException("GAS_ALLOWANCE_EXCEEDED");

            ctx.IntrinsicGas = _config.IntrinsicGasRules.CalculateIntrinsicGas(ctx.Data, ctx.IsContractCreation, ctx.AccessList);

            // EIP-4844: reject the tx if the user's MaxFeePerBlobGas can't
            // cover the block's current blob_base_fee. Spec text:
            // "the transaction is invalid if max_fee_per_blob_gas <
            // get_blob_base_fee(block.excess_blob_gas)". This MUST run
            // before custom rules so a malformed type-3 tx never reaches
            // the rest of the validation chain.
            // Caught by EEST cancun/eip4844_blobs/test_blob_txs.py::
            // test_invalid_tx_max_fee_per_blob_gas_state (2 cases × 3 forks).
            if (ctx.IsType3Transaction && _config.IntrinsicGasRules.Blob != null)
            {
                var blobBaseFee = _config.IntrinsicGasRules.Blob.CalculateBlobBaseFee(ctx.ExcessBlobGas);
                if (ctx.MaxFeePerBlobGas < blobBaseFee)
                    throw new TransactionValidationException("INSUFFICIENT_MAX_FEE_PER_BLOB_GAS");
            }

            _config.TransactionValidationRules?.Validate(ctx, _config);

            // EIP-7623 calldata floor. The rule being non-null is the
            // "floor active at this fork" signal — no EIP flag consulted.
            // Returns 0 (no-op) pre-Prague.
            ctx.MinGasRequired = ctx.IntrinsicGas;
            ctx.FloorGas = _config.IntrinsicGasRules.CalculateFloorGasLimit(ctx.Data, ctx.IsContractCreation);
            if (ctx.FloorGas > ctx.MinGasRequired)
                ctx.MinGasRequired = ctx.FloorGas;

            if (ctx.GasLimit < ctx.MinGasRequired)
            {
                result.IsValidationError = true;
                result.Error = $"Intrinsic gas too low: {ctx.GasLimit} < {ctx.MinGasRequired}";
                return;
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
                        var slot = EvmUInt256.FromBigEndian(storageKey.HexToByteArray());
                        warmAccount.MarkStorageKeyAsWarm(slot);
                    }
                }
            }
        }



        private void HandleSuccessfulContractCreation(
            TransactionExecutionContext ctx,
            TransactionExecutionResult result,
            Program program)
        {
            var deployedCode = program.ProgramResult.Result ?? new byte[0];

            // EIP-3541: Reject 0xEF prefix (London+)
            if (_config.RejectEfPrefix && deployedCode.Length > 0 && deployedCode[0] == 0xEF)
            {
                result.Success = false;
                ctx.ExecutionState.RevertToSnapshot(ctx.TransactionSnapshotId);
                result.GasUsed = ctx.GasLimit.ToLongSafe();
                result.Error = "INVALID_EF_PREFIX";
                return;
            }

            var codeDepositGas = (long)deployedCode.Length * GasConstants.CREATE_DATA_GAS;

            // EIP-170 (Spurious Dragon+): cap deployed code at MaxCodeSize.
            // Gated on EIP-158 (Spurious Dragon onward).
            // HardforkConfig.MaxCodeSize is 0 for Frontier/Homestead/TangerineWhistle
            // (no limit) and 24576 from Spurious Dragon onward. Matches the
            // existing pattern in EVMSimulator's CREATE/CREATE2 opcode path.
            if (_config.MaxCodeSize > 0 && deployedCode.Length > _config.MaxCodeSize)
            {
                result.Success = false;
                ctx.ExecutionState.RevertToSnapshot(ctx.TransactionSnapshotId);
                result.GasUsed = ctx.GasLimit.ToLongSafe();
                result.Error = "MAX_CODE_SIZE_EXCEEDED";
                return;
            }

            if (result.GasUsed + codeDepositGas > ctx.GasLimit)
            {
                var depositResult = _config.CodeDepositRule.HandleCodeDepositOOG(
                    new Execution.Create.CodeDepositContext
                    {
                        Code = deployedCode,
                        GasRemaining = ctx.GasLimit.ToLongSafe() - result.GasUsed,
                        CodeDepositCost = codeDepositGas
                    });
                if (depositResult.Failed)
                {
                    result.Success = false;
                    ctx.ExecutionState.RevertToSnapshot(ctx.TransactionSnapshotId);
                    result.GasUsed = ctx.GasLimit.ToLongSafe();
                    result.Error = "OUT_OF_GAS";
                    return;
                }
                deployedCode = depositResult.FinalCode;
                codeDepositGas = depositResult.FinalCodeDepositCost;
            }

            result.GasUsed += codeDepositGas;
            var newContractAccount = ctx.ExecutionState.CreateOrGetAccountExecutionState(ctx.ContractAddress);
            newContractAccount.Code = deployedCode;
            ctx.ExecutionState.CommitSnapshot(ctx.TransactionSnapshotId);
            result.ContractAddress = ctx.ContractAddress;
        }

        private void CleanupSelfDestructedContracts(TransactionExecutionContext ctx, Program program)
        {
            foreach (var deletedAddr in program.ProgramResult.DeletedContractAccounts)
            {
                ctx.ExecutionState.DeleteAccount(deletedAddr);
            }
        }


        private void ExecuteSimpleTransfer(TransactionExecutionContext ctx, TransactionExecutionResult result)
        {
            ctx.SenderAccount.Balance.DebitExecutionBalance(ctx.Value);

            if (ctx.IsContractCreation)
            {
                var newContractAccount = ctx.ExecutionState.CreateOrGetAccountExecutionState(ctx.ContractAddress);
                newContractAccount.Balance.CreditExecutionBalance(ctx.Value);
            }

            ctx.ExecutionState.CommitSnapshot(ctx.TransactionSnapshotId);
        }

        private void FinalizeTransaction(TransactionExecutionContext ctx, TransactionExecutionResult result)
        {
            if (result.GasUsed > ctx.GasLimit)
                result.GasUsed = ctx.GasLimit.ToLongSafe();

            // Compute refund: program refund (SSTORE clears) + auth refund (existing accounts)
            // Per python spec: auth refund applies even on failure, program refund only on success
            var totalRefundCounter = ctx.AuthRefund;
            if (result.Success)
                totalRefundCounter += result.GasRefund; // GasRefund = program.RefundCounter from ExecuteCode

            var maxRefund = result.GasUsed / _config.RefundQuotient;
            var appliedRefund = Math.Min(totalRefundCounter, maxRefund);
            result.GasUsed -= appliedRefund;
            result.GasRefund = appliedRefund;

            // EIP-7623 calldata floor at finalisation — uses the raw floor
            // (without the contract-creation adder). Returns 0 pre-Prague.
            var floorDataGas = _config.IntrinsicGasRules.CalculateFloorGasLimit(ctx.Data, isContractCreation: false);
            if (floorDataGas > 0 && result.GasUsed < floorDataGas)
            {
                result.GasUsed = floorDataGas;
                if (result.GasUsed > ctx.GasLimit)
                    result.GasUsed = ctx.GasLimit.ToLongSafe();
            }

            result.EffectiveGasUsed = result.GasUsed;

            if (ctx.IsCallMode)
                return;

            var gasRefundAmount = (ctx.GasLimit - new EvmUInt256(result.GasUsed)) * ctx.EffectiveGasPrice;
            ctx.SenderAccount.Balance.CreditExecutionBalance(gasRefundAmount);

            // EIP-1559 (London+): coinbase receives only the priority tip; the
            // baseFee portion is burnt. Pre-London the full effectiveGasPrice
            // goes to the miner, with baseFee irrelevant to fee accounting
            // (the env field still may be populated by EEST fixtures generated
            // for shared multi-fork test data — must NOT subtract it pre-London
            // or the coinbase balance diverges from canonical).
            var perGasReward = _config.BaseFeeApplies
                ? (ctx.EffectiveGasPrice > ctx.BaseFee ? ctx.EffectiveGasPrice - ctx.BaseFee : EvmUInt256.Zero)
                : ctx.EffectiveGasPrice;
            var minerReward = new EvmUInt256(result.GasUsed) * perGasReward;
            // Always materialise the coinbase, then either credit it or
            // flag it as touched. This mirrors the canonical client
            // behaviour: paying the miner reward is a touch even with a
            // zero credit, so the coinbase enters the post-state. The
            // per-fork TouchedEmptyCleanupRule below removes the entry
            // again at EIP-161+ when it is empty (no balance, no nonce,
            // no code) — closing the Prague/Cancun "phantom empty
            // coinbase on priorityFee==0" leak. Pre-EIP-161 forks have
            // NoOp cleanup, so the touched empty coinbase persists —
            // required by EEST frontier/touch/test_zero_gas_price_and_touching.
            var coinbaseAccount = ctx.ExecutionState.CreateOrGetAccountExecutionState(ctx.Coinbase);
            if (!minerReward.IsZero)
                coinbaseAccount.Balance.CreditExecutionBalance(minerReward);
            else
                coinbaseAccount.IsTouched = true;

            // EIP-161 STATE_CLEARING — per-fork strategy. NoOp pre-EIP-161,
            // Eip161 at Spurious Dragon+. The executor stays fork-blind;
            // the choice is registered on HardforkConfig.
            _config.TouchedEmptyCleanupRule.Apply(ctx.ExecutionState);
        }
    }

    public class TransactionValidationException : Exception
    {
        public TransactionValidationException(string message) : base(message) { }
    }
}
