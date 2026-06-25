namespace Nethereum.EVM.Hardforks
{
    /// <summary>
    /// Lazy-loaded mapping from <see cref="PrecompileKind"/> to its
    /// concrete executor implementation. Provided by the EVM host
    /// (production Nethereum, Zisk zkVM guest, in-memory simulator,
    /// etc.) so the spec stays portable.
    ///
    /// <para><b>Why a registry interface:</b> the cryptographic
    /// implementations differ per EVM type:</para>
    /// <list type="bullet">
    ///   <item>Production Nethereum host: full Bouncy Castle / native
    ///   crypto (e.g. <c>Bn128PairingPrecompile</c> calling into
    ///   <c>System.Security.Cryptography</c> + libsecp256k1).</item>
    ///   <item>Zisk zkVM guest: stripped-down implementations that
    ///   delegate to RISC-V CSR precompile slots, or constant-time
    ///   re-implementations.</item>
    ///   <item>In-memory simulator: stub implementations for fast
    ///   property tests.</item>
    /// </list>
    ///
    /// <para><b>Lazy loading:</b> <see cref="Get"/> resolves the
    /// executor on first call and may cache. The registry decides the
    /// caching strategy (eager singletons vs lazy per-call vs LRU).
    /// Heavy crypto libraries are not pulled into the assembly's
    /// metadata until first use, keeping Zisk witness binaries small.</para>
    ///
    /// <para><b>Wiring:</b> each host produces a concrete
    /// <see cref="IPrecompileExecutorRegistry"/> at startup
    /// (e.g. <c>MainnetExecutorRegistry</c>, <c>ZiskExecutorRegistry</c>).
    /// Consumers grab the executor by spec:</para>
    /// <code>
    ///     var precompile = spec.Precompiles[0]; // address 0x05, ModExp_Eip2565
    ///     var exec = registry.Get(precompile.Kind);
    ///     var result = exec.Execute(input);
    /// </code>
    /// </summary>
    public interface IPrecompileExecutorRegistry
    {
        /// <summary>
        /// Resolves the executor for a precompile kind. Implementations
        /// may resolve lazily and cache. Throws
        /// <see cref="System.NotSupportedException"/> if the kind isn't
        /// available on this host (e.g. an Osaka precompile on a
        /// Cancun-only host).
        /// </summary>
        IPrecompileExecutor Get(PrecompileKind kind);
    }

    /// <summary>
    /// Bytecode + crypto-call executor for a single precompile. Stateless
    /// — instances are safe to share across threads / transactions.
    /// </summary>
    public interface IPrecompileExecutor
    {
        /// <summary>
        /// Computes the gas cost given the input bytes. Called BEFORE
        /// <see cref="Execute"/> so the EVM can OOG without running the
        /// crypto.
        /// </summary>
        long GetGasCost(byte[] input);

        /// <summary>
        /// Computes the precompile output. Caller has already deducted
        /// the gas via <see cref="GetGasCost"/>.
        /// </summary>
        byte[] Execute(byte[] input);
    }
}
