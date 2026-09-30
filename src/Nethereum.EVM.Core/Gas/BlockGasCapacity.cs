using System;

namespace Nethereum.EVM.Gas
{
    /// <summary>
    /// The block's running gas totals and the admission test every
    /// transaction is put through before it executes.
    ///
    /// <para>EIP-8037: "At block level, instead of tracking a single
    /// <c>gas_used</c> counter, we keep track of two counters, one for
    /// <c>state-gas</c> and one for <c>execution-gas</c>"; a transaction is
    /// admitted only while <c>min(TX_MAX_GAS_LIMIT, tx.gas) &lt;=
    /// execution_gas_available</c> and <c>tx.gas &lt;= state_gas_available</c>,
    /// where <c>execution_gas_available = block_env.block_gas_limit -
    /// block_output.block_execution_gas_used</c> and <c>state_gas_available =
    /// block_env.block_gas_limit - block_output.block_state_gas_used</c>.</para>
    ///
    /// <para>EIP-7778: "Block gas accounting becomes: <c>block.gas_used +=
    /// max(tx_gas_used, calldata_floor_gas_cost)</c>" — the refund is not
    /// subtracted, which is why <see cref="Add"/> takes the per-dimension
    /// totals rather than the sender-facing gas used.</para>
    ///
    /// <para>EIP-4844: "<c>assert blob_gas_used &lt;= MAX_BLOB_GAS_PER_BLOCK</c>".
    /// Counted in blobs against <see cref="HardforkConfig.MaxBlobsPerBlock"/>,
    /// the form the limit is already stated in.</para>
    ///
    /// <para>THE EMPTY-CAPACITY RULE, stated here and nowhere else. One
    /// instance per block, shared by every transaction context in it. A
    /// context that names no block — <c>eth_call</c>, a system call, a bare
    /// simulation — keeps an empty one and so measures against a block
    /// holding nothing but itself. That is what makes the blob dimension a
    /// strict superset of the per-transaction cap it replaced: with the
    /// totals at zero the comparison is identical.</para>
    /// </summary>
    public sealed class BlockGasCapacity
    {
        public long ExecutionGasUsed { get; private set; }

        public long StateGasUsed { get; private set; }

        public int BlobCount { get; private set; }

        /// <summary>
        /// EIP-8037: "The block header <c>gas_used</c> field is set to:
        /// <c>gas_used = max(block_output.block_execution_gas_used,
        /// block_output.block_state_gas_used)</c>".
        /// </summary>
        public long HeaderGasUsed => Math.Max(ExecutionGasUsed, StateGasUsed);

        public void Add(long gasUsed, long executionGasUsed, long stateGasUsed, int blobCount, bool stateGasActive)
        {
            if (stateGasActive)
            {
                ExecutionGasUsed += executionGasUsed;
                StateGasUsed += stateGasUsed;
            }
            else
            {
                ExecutionGasUsed += gasUsed;
            }

            BlobCount += blobCount;
        }

        public void CheckTransactionFits(TransactionExecutionContext ctx, HardforkConfig config)
        {
            CheckBlobsFit(ctx, config);
            CheckGasAllowanceFits(ctx, config);
        }

        public void CheckGasAllowanceFits(TransactionExecutionContext ctx, HardforkConfig config)
        {
            if (!IsBlockMember(ctx))
                return;

            CheckExecutionGasFits(ctx, config);
            CheckStateGasFits(ctx, config);
        }

        private static bool IsBlockMember(TransactionExecutionContext ctx) =>
            !ctx.IsCallMode && ctx.BlockGasLimit > 0;

        private void CheckExecutionGasFits(TransactionExecutionContext ctx, HardforkConfig config)
        {
            var available = ctx.BlockGasLimit.ToLongSafe() - ExecutionGasUsed;
            var charged = ChargedAgainstExecutionGas(ctx, config);
            if (charged > available)
                throw new TransactionValidationException(TransactionError.GasAllowanceExceeded, "GAS_ALLOWANCE_EXCEEDED");
        }

        private static long ChargedAgainstExecutionGas(TransactionExecutionContext ctx, HardforkConfig config)
        {
            var txGasLimit = ctx.GasLimit.ToLongSafe();
            return config.IntrinsicGasRules.StateGasActive
                ? Math.Min(GasConstants.EIP8037_TX_MAX_GAS_LIMIT, txGasLimit)
                : txGasLimit;
        }

        private void CheckStateGasFits(TransactionExecutionContext ctx, HardforkConfig config)
        {
            if (!config.IntrinsicGasRules.StateGasActive)
                return;

            var available = ctx.BlockGasLimit.ToLongSafe() - StateGasUsed;
            if (ctx.GasLimit.ToLongSafe() > available)
                throw new TransactionValidationException(TransactionError.GasAllowanceExceeded, "GAS_ALLOWANCE_EXCEEDED");
        }

        public void CheckBlobsFit(TransactionExecutionContext ctx, HardforkConfig config)
        {
            if (!ctx.IsType3Transaction || ctx.BlobVersionedHashes == null)
                return;

            if (BlobCount + ctx.BlobVersionedHashes.Count > config.MaxBlobsPerBlock)
                throw new TransactionValidationException(TransactionError.Type3TxBlobCountExceeded, "TYPE_3_TX_BLOB_COUNT_EXCEEDED");
        }
    }
}
