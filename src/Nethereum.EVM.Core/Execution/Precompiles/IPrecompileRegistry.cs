using System.Collections.Generic;

namespace Nethereum.EVM.Execution.Precompiles
{
    /// <summary>
    /// The precompile facade exposed by a <c>ProtocolSpec</c>: a sparse
    /// int-keyed dispatch table of <see cref="IPrecompileHandler"/> plus a
    /// single <see cref="PrecompileGasCalculators"/>. Mirrors the shape of
    /// <c>Nethereum.Wallet.Hosting.RpcHandlerRegistry</c> but is int-keyed
    /// (precompile addresses collapse to integers 0x01..0x11 + 0x100) so
    /// dispatch is a bounds check plus an array index with no hashing or
    /// allocation on the hot path.
    ///
    /// Registries are built once per fork, held as immutable fields on the
    /// spec, and reused for every transaction. Crypto backends
    /// (<c>IBls12381Operations</c>, <c>IKzgOperations</c>) are injected at
    /// registry construction via the handler constructor, not looked up at
    /// runtime.
    /// </summary>
    public interface IPrecompileRegistry
    {
        /// <summary>True if the registry has a handler installed at the
        /// given numeric address.</summary>
        bool CanHandle(int address);

        /// <summary>
        /// The handler at the given numeric address, or <c>null</c> if no
        /// handler is installed. Callers normally check <see cref="CanHandle"/>
        /// first and dispatch via <see cref="Execute"/>.
        /// </summary>
        IPrecompileHandler Get(int address);

        /// <summary>
        /// Gas cost for the precompile at the given address, derived from
        /// the fork's <see cref="PrecompileGasCalculators"/>.
        /// </summary>
        long GetGasCost(int address, byte[] input);

        /// <summary>
        /// Execute the precompile at the given address on the given input.
        /// Throws <see cref="System.InvalidOperationException"/> when no
        /// handler is installed — callers should gate on
        /// <see cref="CanHandle"/>.
        /// </summary>
        byte[] Execute(int address, byte[] input);

        /// <summary>
        /// Enumerates the numeric addresses handled by this registry.
        /// Used at transaction start to pre-warm precompile addresses in
        /// the accessed-addresses set per EIP-2929.
        /// </summary>
        IEnumerable<int> GetAddresses();
    }
}
