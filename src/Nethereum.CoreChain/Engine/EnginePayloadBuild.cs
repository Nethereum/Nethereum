using System.Collections.Generic;
using Nethereum.Model;

namespace Nethereum.CoreChain.Engine
{
    public sealed class EnginePayloadBuild
    {
        public EnginePayloadBuild(BlockProductionResult result, IReadOnlyList<ISignedTransaction> submittedTransactions)
        {
            Result = result;
            SubmittedTransactions = submittedTransactions;
        }

        public BlockProductionResult Result { get; }

        public IReadOnlyList<ISignedTransaction> SubmittedTransactions { get; }
    }
}
