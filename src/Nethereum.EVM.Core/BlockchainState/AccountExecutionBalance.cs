using Nethereum.Util;

namespace Nethereum.EVM.BlockchainState
{
    public class AccountExecutionBalance
    {
        public string Address { get; set; }
        public EvmUInt256? InitialChainBalance { get; protected set; }
        public EvmUInt256? ExecutionBalance { get; protected set; }

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
            ExecutionBalance = (ExecutionBalance ?? EvmUInt256.Zero) + value;
        }

        public void DebitExecutionBalance(EvmUInt256 value)
        {
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
