using System.Collections.Generic;
using Nethereum.Model;

namespace Nethereum.CoreChain.Engine
{
    public sealed class EnginePayloadBuild
    {
        public EnginePayloadBuild(BlockProductionResult result, IReadOnlyList<ISignedTransaction> submittedTransactions)
            : this(result, submittedTransactions, null)
        {
        }

        public EnginePayloadBuild(
            BlockProductionResult result,
            IReadOnlyList<ISignedTransaction> submittedTransactions,
            IReadOnlyList<Withdrawal>? withdrawals)
        {
            Result = result;
            SubmittedTransactions = submittedTransactions;
            Withdrawals = withdrawals ?? new List<Withdrawal>();
        }

        public BlockProductionResult Result { get; }

        public IReadOnlyList<ISignedTransaction> SubmittedTransactions { get; }

        public IReadOnlyList<Withdrawal> Withdrawals { get; }
    }
}
