namespace Nethereum.EVM.Execution.Precompiles
{
    /// <summary>
    /// A single precompiled-contract implementation. Modelled on
    /// <c>Nethereum.Wallet.Hosting.IRpcMethodHandler</c>: one key property
    /// (<see cref="AddressNumeric"/>) and one pure action
    /// (<see cref="Execute(byte[])"/>). Gas cost lives on
    /// <see cref="PrecompileGasCalculators"/>, not on the handler — a single
    /// handler class is reused across forks and only the fork-specific gas
    /// calculator bundle changes.
    ///
    /// Handlers are eagerly instantiated at spec-build time, held in a sparse
    /// array indexed by <see cref="AddressNumeric"/>, and dispatched in O(1)
    /// with no allocation on the hot path.
    ///
    /// Handlers are pure, synchronous transformers. They never call the EVM
    /// state reader and never await. External crypto backends
    /// (<c>IBls12381Operations</c>, <c>IKzgOperations</c>) are injected via
    /// the handler constructor at spec construction, not per call.
    /// </summary>
    public interface IPrecompileHandler
    {
        /// <summary>
        /// Numeric form of the precompile address (1..17 for the Prague set,
        /// 256 for the Osaka EIP-7951 P256VERIFY). This is the dispatch key
        /// used by <c>PrecompileRegistry</c>.
        /// </summary>
        int AddressNumeric { get; }

        /// <summary>
        /// Execute the precompile on the given input bytes. Implementations
        /// must not allocate unnecessarily, must not touch EVM state, and
        /// must throw a specific exception (most commonly
        /// <see cref="System.ArgumentException"/>) for malformed input — the
        /// caller (the SetupCallFrame dispatch path) interprets any
        /// exception as precompile failure and consumes all forwarded gas,
        /// which matches Ethereum semantics for invalid precompile input.
        /// </summary>
        byte[] Execute(byte[] input);
    }
}
