namespace Nethereum.CoreChain.Validation
{
    /// <summary>
    /// Three-way classification of a state-root divergence after consulting
    /// an <see cref="ICanonicalStateRootSource"/>.
    /// </summary>
    public enum DivergenceOutcome
    {
        /// <summary>
        /// The peer / block source supplied a correct header, but our
        /// re-execution produced a different state root. The block source
        /// is honest; OUR EVM is wrong at this block. Halt and investigate,
        /// or log + continue per --continue-on-mismatch.
        /// </summary>
        EvmBug,

        /// <summary>
        /// The peer's header didn't match canonical — peer was on the wrong
        /// fork or fed deliberately bad data. Ban the peer (or rotate
        /// sources), journal-rewind one block, retry from a different
        /// source.
        /// </summary>
        PeerLied,

        /// <summary>
        /// The canonical source has no answer at this height (anchor not
        /// yet posted, RPC pruned, beacon not yet finalised). No verdict
        /// is possible from this source alone; caller falls through to
        /// another source or to existing rewind-and-retry policy.
        /// </summary>
        SourceUnavailable
    }

    /// <summary>
    /// Verdict returned by
    /// <see cref="CanonicalStateRootDiagnostics.DiagnoseAsync"/>. Names the
    /// outcome, the canonical values consulted (so the operator can
    /// inspect them), and which source delivered the answer.
    /// </summary>
    public sealed record DivergenceVerdict(
        DivergenceOutcome Outcome,
        byte[] CanonicalStateRoot,
        byte[] CanonicalBlockHash,
        string SourceName,
        string Detail);
}
