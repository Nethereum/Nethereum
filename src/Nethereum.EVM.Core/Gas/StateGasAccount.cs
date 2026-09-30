
using Nethereum.Documentation;

namespace Nethereum.EVM.Gas
{
    [NethereumDocExample(DocSection.EvmSimulator, "state-gas", "EIP-8037 per-transaction state-gas reservoir accounting")]
    public sealed class StateGasAccount
    {
        public long ReservoirRemaining { get; set; }

        public long FromReservoir { get; set; }

        public long SpilledIntoExecution { get; set; }
    }
}
