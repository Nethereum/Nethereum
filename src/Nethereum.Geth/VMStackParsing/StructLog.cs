using System.Numerics;

namespace Nethereum.Geth.VMStackParsing
{
    public struct StructLog
    {
        public string From { get; set; }
        public uint PC { get; set; }
        public string Op { get; set; }
        public BigInteger Gas { get; set; }
        public BigInteger GasCost { get; set; }
        public uint Depth { get; set; }

        public BigInteger StackEtherValue { get; set; }
        public string To { get; set; }
        public BigInteger StackGas { get; set; }
    }
}
