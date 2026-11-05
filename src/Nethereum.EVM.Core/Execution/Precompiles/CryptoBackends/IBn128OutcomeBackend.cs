using Nethereum.Documentation;
namespace Nethereum.EVM.Execution.Precompiles.CryptoBackends
{
    [NethereumDocExample(DocSection.EvmSimulator, "precompile-outcome", "A bn128 backend that reports invalid input as a value")]
    public interface IBn128OutcomeBackend
    {
        bool TryAdd(byte[] input, out byte[] output);

        bool TryMul(byte[] input, out byte[] output);

        bool TryPairing(byte[] input, out byte[] output);
    }
}
