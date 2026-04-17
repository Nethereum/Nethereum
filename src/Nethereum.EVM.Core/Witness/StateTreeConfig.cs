namespace Nethereum.EVM.Witness
{
    public enum WitnessStateTreeType : byte
    {
        Patricia = 0,
        Binary = 1
    }

    public enum WitnessHashFunction : byte
    {
        Keccak = 0,
        Blake3 = 1,
        Poseidon = 2,
        Sha256 = 3
    }
}
