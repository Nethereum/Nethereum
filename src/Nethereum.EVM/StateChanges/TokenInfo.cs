
using Nethereum.Documentation;

namespace Nethereum.EVM.StateChanges
{
    [NethereumDocExample(DocSection.EvmSimulator, "state-changes", "The symbol and decimals a token resolver supplies for display")]
    public class TokenInfo
    {
        public string Symbol { get; set; }
        public int Decimals { get; set; }

        public TokenInfo()
        {
        }

        public TokenInfo(string symbol, int decimals)
        {
            Symbol = symbol;
            Decimals = decimals;
        }
    }
}
