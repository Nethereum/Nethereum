using Nethereum.Util;

namespace Nethereum.EVM.BlockchainState
{
    public class AccountExecutionBalance
    {
        public string Address { get; set; }
        private EvmUInt256? _initialChainBalance;
        public EvmUInt256? InitialChainBalance
        {
            get => _initialChainBalance;
            protected set
            {
                Owner?.Journal?.InitialBalanceChanged(Owner, _initialChainBalance);
                _initialChainBalance = value;
            }
        }

        private EvmUInt256? _executionBalance;
        public EvmUInt256? ExecutionBalance
        {
            get => _executionBalance;
            protected set
            {
                Owner?.Journal?.ExecutionBalanceChanged(Owner, _executionBalance);
                _executionBalance = value;
            }
        }

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

        internal void RestoreInitialChainBalance(EvmUInt256? value) => _initialChainBalance = value;

        internal void RestoreExecutionBalance(EvmUInt256? value) => _executionBalance = value;

        public string ToTraceString()
        {
            return $"{Address} Balance: Total={GetTotalBalance()}";
        }
    }
}
