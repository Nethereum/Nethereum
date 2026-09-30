namespace Nethereum.EVM.Execution.Precompiles.Handlers
{
    public sealed class PlaceholderPrecompile : IPrecompileHandler
    {
        public int AddressNumeric { get; }

        public PlaceholderPrecompile(int address)
        {
            AddressNumeric = address;
        }

        public byte[] Execute(byte[] input)
        {
            throw new UnwiredPrecompileException(
                AddressNumeric,
                $"Precompile at address 0x{AddressNumeric:x} has no backend wired. " +
                "Layer one onto the fork's PrecompileRegistry: .WithKzgBackend(IKzgOperations) " +
                "from Nethereum.EVM.Precompiles.Kzg for 0x0a, or " +
                ".WithBlsBackend(IBls12381Operations) from Nethereum.EVM.Precompiles.Bls " +
                "for 0x0b..0x11.");
        }
    }
}
