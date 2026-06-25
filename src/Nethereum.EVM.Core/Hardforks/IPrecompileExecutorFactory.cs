using Nethereum.EVM.Execution.Precompiles;
using Nethereum.EVM.Execution.Precompiles.GasCalculators;

namespace Nethereum.EVM.Hardforks
{
    /// <summary>
    /// EVM-type-specific factory that resolves a declarative
    /// <see cref="PrecompileSpec"/> (address + EIP gas-formula variant)
    /// to the concrete <see cref="IPrecompileHandler"/> + matching
    /// <see cref="IPrecompileGasCalculator"/> the runtime uses to
    /// execute and price the precompile.
    ///
    /// <para><b>Why an interface:</b> the cryptographic implementations
    /// differ per EVM host:</para>
    /// <list type="bullet">
    ///   <item>Production Nethereum: <see cref="MainnetPrecompileExecutorFactory"/>
    ///   wires Bouncy Castle / native crypto via
    ///   <see cref="PrecompileBackends"/>.</item>
    ///   <item>Zisk zkVM guest: a Zisk-specific factory wires
    ///   precompile-as-CSR or stripped re-implementations and routes
    ///   pre-Cancun precompiles through the guest's poseidon /
    ///   secp256k1 syscalls.</item>
    ///   <item>In-memory simulator: a factory wires stub or recorded
    ///   responses for fast property tests.</item>
    /// </list>
    ///
    /// <para>The factory is consulted lazily — handlers are constructed
    /// only when the runtime registry is built (once per fork at
    /// startup). Heavy crypto libraries don't pull into Zisk witness
    /// binaries that don't activate the corresponding precompile.</para>
    /// </summary>
    public interface IPrecompileExecutorFactory
    {
        /// <summary>
        /// Returns the executor (bytecode + crypto implementation) for
        /// the precompile at the given spec entry. May return a
        /// <see cref="PlaceholderPrecompile"/> for spec kinds that are
        /// recognised but not implemented in this host.
        /// </summary>
        IPrecompileHandler GetHandler(PrecompileSpec spec);

        /// <summary>
        /// Returns the gas calculator for the precompile at the given
        /// spec entry. The calculator and handler MUST agree on which
        /// EIP variant is active (the <see cref="PrecompileSpec.Kind"/>
        /// disambiguates).
        /// </summary>
        IPrecompileGasCalculator GetGasCalculator(PrecompileSpec spec);
    }
}
