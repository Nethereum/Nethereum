using Nethereum.CoreChain.Validation;

using Nethereum.Documentation;
namespace Nethereum.CoreChain.Sync
{
    public interface IValidationPolicy
    {
        bool ShouldAnchorAt(ulong blockNumber);

        ValidationAction OnVerdict(DivergenceVerdict verdict, ulong blockNumber);
    }

    [NethereumDocExample(DocSection.ChainInfrastructure, "corechain", "ValidationAction - what a divergence verdict makes the follower do")]
    public enum ValidationAction
    {
        Continue,
        RewindAndRetry,
        Fatal,
    }
}
