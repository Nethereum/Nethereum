using Nethereum.Hex.HexTypes;

namespace Nethereum.GSN.Models
{
    public class RelayResponse
    {
        public string Error { get; set; }
        public HexBigInteger Nonce { get; set; }
        public HexBigInteger GasPrice { get; set; }
        public HexBigInteger Gas { get; set; }
        public string To { get; set; }
        public HexBigInteger Value { get; set; }
        public string Input { get; set; }
        public string Hash { get; set; }
        public string V { get; set; }
        public string R { get; set; }
        public string S { get; set; }
    }
}
