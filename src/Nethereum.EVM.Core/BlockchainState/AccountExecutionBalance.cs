using Nethereum.Util;

namespace Nethereum.EVM.BlockchainState
{
    public class AccountExecutionBalance
    {
        public string Address { get; set; }
        public EvmUInt256? InitialChainBalance { get; protected set; }
        public EvmUInt256? ExecutionBalance { get; protected set; }

        /// <summary>
        /// Owning account state — wired by AccountExecutionState during
        /// construction. Lets Credit/Debit set the EIP-161 dirty bit on
        /// the owner without each call site having to set it manually.
        /// May be null on legacy paths that construct a bare balance.
        /// </summary>
        public AccountExecutionState Owner { get; set; }

        public EvmUInt256 GetTotalBalance()
        {
            var originalBalance = InitialChainBalance ?? EvmUInt256.Zero;
            var internalBalance = ExecutionBalance ?? EvmUInt256.Zero;
            return originalBalance + internalBalance;
        }

        public bool IsZero()
        {
            return GetTotalBalance().IsZero;
        }

        public void CreditExecutionBalance(EvmUInt256 value)
        {
            // EIP-161: Credit is a touch even when value == 0 — mirrors
            // geth StateDB.AddBalance which always journals a dirty entry.
            // Pre-EIP-161 forks register the NoOp cleanup rule so this
            // flag never affects their behaviour.
            if (Owner != null) Owner.IsTouched = true;
            ExecutionBalance = (ExecutionBalance ?? EvmUInt256.Zero) + value;
        }

        public void DebitExecutionBalance(EvmUInt256 value)
        {
            if (Owner != null) Owner.IsTouched = true;
            ExecutionBalance = (ExecutionBalance ?? EvmUInt256.Zero) - value;
        }

        public void SetInitialChainBalance(EvmUInt256 value)
        {
            InitialChainBalance = value;
        }

        public void ClearInitialChainBalance()
        {
            InitialChainBalance = null;
        }

        public void SetExecutionBalance(EvmUInt256? value)
        {
            ExecutionBalance = value;
        }

        public string ToTraceString()
        {
            return $"{Address} Balance: Total={GetTotalBalance()}";
        }
    }
}
