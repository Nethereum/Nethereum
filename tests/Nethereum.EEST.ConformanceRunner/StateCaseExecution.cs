using System.Collections.Generic;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;

namespace Nethereum.EEST.ConformanceRunner
{
    public sealed class StateCaseExecution
    {
        public ConformanceCaseResult Result { get; set; } = ConformanceCaseResult.Ok();
        public List<ProgramTrace>? Traces { get; set; }
        public ExecutionStateService? ExecutionState { get; set; }
        public string? Sender { get; set; }
        public string? Coinbase { get; set; }
    }
}
