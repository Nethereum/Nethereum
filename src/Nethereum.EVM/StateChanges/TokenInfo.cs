namespace Nethereum.EVM.StateChanges
{
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
