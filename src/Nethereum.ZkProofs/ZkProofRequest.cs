namespace Nethereum.ZkProofs
{
    public class ZkProofRequest
    {
        public byte[] CircuitWasm { get; set; } = new byte[0];
        public byte[] CircuitZkey { get; set; } = new byte[0];
        public string InputJson { get; set; } = "";
        public ZkProofScheme Scheme { get; set; } = ZkProofScheme.Groth16;
    }
}
