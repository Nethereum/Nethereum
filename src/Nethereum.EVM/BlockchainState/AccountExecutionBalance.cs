using System.Numerics;
using System.Text;

namespace Nethereum.EVM.BlockchainState
{
    public class AccountExecutionBalance
    {
        public string Address { get; set; }
        public BigInteger? InitialChainBalance { get; protected set; }
        public BigInteger? ExecutionBalance { get; protected set; }
        public BigInteger GetTotalBalance()
        {
            var originalBalance = InitialChainBalance == null ? 0 : InitialChainBalance.Value;
            var internalBalance = ExecutionBalance == null ? 0 : ExecutionBalance.Value;
            return originalBalance + internalBalance;
        }

        public void UpdateExecutionBalance(BigInteger value)
        {
            if (ExecutionBalance != null)
            {
                ExecutionBalance = ExecutionBalance + value;
            }
            else
            {
                ExecutionBalance = value;
            }
        }

        public void SetInitialChainBalance(BigInteger value)
        {
            InitialChainBalance = value;
        }

        public string ToTraceString()
        {
            var builder = new StringBuilder();
            builder.AppendLine($"{Address} Balance:");
            builder.AppendLine($"Initial: {(InitialChainBalance == null ? "Not set" : InitialChainBalance.Value.ToString())}");
            builder.AppendLine($"Execution: {(ExecutionBalance == null ? "Not set" : ExecutionBalance.Value.ToString())}");
            builder.AppendLine($"Total: {GetTotalBalance()}");

            return builder.ToString();
        }
    }
}