using Nethereum.CoreChain.Validation;

namespace Nethereum.CoreChain.Sync
{
    /// <summary>
    /// Two-question contract the follower consults on every block: "should
    /// I anchor here?" and "what do I do with this verdict?". Recovery
    /// behaviour is encapsulated in <see cref="OnVerdict"/>; no recovery
    /// policy lives in <see cref="IFollowerService"/>.
    /// </summary>
    public interface IValidationPolicy
    {
        /// <summary>
        /// True when the follower should consult
        /// <see cref="ICanonicalStateRootSource"/> after executing the
        /// block at <paramref name="blockNumber"/>, independent of
        /// whether the root matched.
        /// </summary>
        bool ShouldAnchorAt(ulong blockNumber);

        /// <summary>
        /// Map a divergence verdict to an action. Called on root mismatch
        /// and on periodic anchor checks. Implementations are pure: no
        /// side effects beyond logging.
        /// </summary>
        ValidationAction OnVerdict(DivergenceVerdict verdict, ulong blockNumber);
    }

    public enum ValidationAction
    {
        Continue,
        RewindAndRetry,
        Fatal,
    }
}
