using System.Numerics;

namespace Nethereum.AppChain.Policy
{
    public class PolicyInfo
    {
        public BigInteger Version { get; set; }
        public BigInteger MaxCalldataBytes { get; set; }
        public BigInteger MaxLogBytes { get; set; }
        public BigInteger BlockGasLimit { get; set; }
        public string? Sequencer { get; set; }

        public byte[]? WritersRoot { get; set; }
        public byte[]? AdminsRoot { get; set; }
        public byte[]? BlacklistRoot { get; set; }
        public BigInteger Epoch { get; set; }
    }

    public class MembershipProof
    {
        public string Address { get; set; } = string.Empty;
        public byte[][] Proof { get; set; } = System.Array.Empty<byte[]>();
        public byte[]? BlacklistProof { get; set; }
    }
}
