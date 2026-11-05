using Nethereum.Documentation;
namespace Nethereum.EVM.Execution.Precompiles
{
    [NethereumDocExample(DocSection.EvmSimulator, "precompile-outcome", "Observe every precompile call a registry dispatches")]
    public interface IPrecompileCallObserver
    {
        void OnCall(int address);
    }
}
