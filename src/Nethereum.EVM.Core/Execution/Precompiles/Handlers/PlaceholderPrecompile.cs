using System;

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
            throw new InvalidOperationException(
                $"Precompile at address 0x{AddressNumeric:x} has no backend wired. " +
                $"Layer one via .WithBlsBackend() or .WithKzgBackend().");
        }
    }
}
