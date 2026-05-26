namespace Nethereum.CoreChain.Proving
{
    public class BlockProofResult
    {
        public byte[] ProofBytes { get; set; }
        public byte[] PreStateRoot { get; set; }
        public byte[] PostStateRoot { get; set; }
        public byte[] ProverComputedStateRoot { get; set; }
        public byte[] ProverComputedBlockHash { get; set; }
        public bool StateRootVerified { get; set; }
        public bool BlockHashVerified { get; set; }
        public long BlockNumber { get; set; }
        public byte[] WitnessHash { get; set; }
        public byte[] ElfHash { get; set; }
        public string ProverMode { get; set; }
        public long GasUsed { get; set; }
    }
}
