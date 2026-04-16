using System;
using System.Collections.Generic;

namespace Nethereum.EVM.Execution.Precompiles
{
    /// <summary>
    /// Immutable sparse-array dispatch table for precompile handlers, paired
    /// with one fork-specific <see cref="PrecompileGasCalculators"/> bundle.
    /// Dispatch is an O(1) bounds check + array index; no hashing, no
    /// allocation per call.
    ///
    /// Registries are normally constructed once per fork via
    /// <c>PrecompileRegistries.Cancun/Prague/OsakaBase()</c>, then layered
    /// with crypto backends via the
    /// <c>WithBlsBackend</c> / <c>WithKzgBackend</c> extension methods in
    /// <c>Nethereum.EVM.Precompiles.Bls</c> and <c>.Kzg</c>. Each layering
    /// returns a new immutable registry; the base registry is unchanged.
    /// </summary>
    public sealed class PrecompileRegistry : IPrecompileRegistry
    {
        private readonly IPrecompileHandler[] _handlers;
        private readonly PrecompileGasCalculators _gasCalculators;

        /// <summary>
        /// The fork's precompile gas calculator bundle. Exposed so extension
        /// methods that produce a new registry can reuse the same
        /// composition when adding crypto backends.
        /// </summary>
        public PrecompileGasCalculators GasCalculators => _gasCalculators;

        public PrecompileRegistry(
            PrecompileGasCalculators gasCalculators,
            IEnumerable<IPrecompileHandler> handlers)
        {
            if (gasCalculators == null) throw new ArgumentNullException(nameof(gasCalculators));
            if (handlers == null) throw new ArgumentNullException(nameof(handlers));

            _gasCalculators = gasCalculators;
            _handlers = BuildSparseArray(handlers);
        }

        private static IPrecompileHandler[] BuildSparseArray(IEnumerable<IPrecompileHandler> handlers)
        {
            // First pass: find the maximum address to size the sparse array.
            int max = -1;
            foreach (var h in handlers)
            {
                if (h == null) continue;
                if (h.AddressNumeric > max) max = h.AddressNumeric;
            }
            if (max < 0) return new IPrecompileHandler[0];

            // Second pass: populate. Later registrations override earlier
            // ones for the same address ("late wins"), matching the
            // Dictionary-based CompositePrecompileProvider semantic.
            var arr = new IPrecompileHandler[max + 1];
            foreach (var h in handlers)
            {
                if (h == null) continue;
                arr[h.AddressNumeric] = h;
            }
            return arr;
        }

        public bool CanHandle(int address) =>
            address >= 0 && address < _handlers.Length && _handlers[address] != null;

        public IPrecompileHandler Get(int address) =>
            CanHandle(address) ? _handlers[address] : null;

        public long GetGasCost(int address, byte[] input) =>
            _gasCalculators.GetGasCost(address, input);

        public byte[] Execute(int address, byte[] input)
        {
            var handler = Get(address);
            if (handler == null)
                throw new InvalidOperationException(
                    $"No precompile handler installed at address 0x{address:x} on this spec.");
            return handler.Execute(input);
        }

        public IEnumerable<int> GetAddresses()
        {
            for (int i = 0; i < _handlers.Length; i++)
                if (_handlers[i] != null)
                    yield return i;
        }

        /// <summary>
        /// Enumerates every handler currently installed in the registry.
        /// Used by the <c>.WithBlsBackend</c> / <c>.WithKzgBackend</c>
        /// extensions in the backend packages to reconstruct a new registry
        /// with additional handlers layered on top.
        /// </summary>
        public IEnumerable<IPrecompileHandler> GetHandlers()
        {
            for (int i = 0; i < _handlers.Length; i++)
                if (_handlers[i] != null)
                    yield return _handlers[i];
        }

        /// <summary>
        /// Returns a new immutable registry that contains every handler in
        /// this registry plus the given <paramref name="additional"/>
        /// handlers, using the same gas calculator bundle. Later handlers
        /// override earlier ones for the same address — this is how backend
        /// extensions (e.g. <c>WithBlsBackend</c>) layer real crypto on top
        /// of the base registry.
        /// </summary>
        public PrecompileRegistry WithHandlers(params IPrecompileHandler[] additional)
        {
            if (additional == null || additional.Length == 0) return this;

            var combined = new List<IPrecompileHandler>();
            foreach (var h in GetHandlers()) combined.Add(h);
            foreach (var h in additional) if (h != null) combined.Add(h);

            return new PrecompileRegistry(_gasCalculators, combined);
        }

        /// <summary>
        /// Returns a new immutable registry with every handler from this
        /// registry plus the given gas calculator bundle. Used when an
        /// extension needs to replace the gas composition (e.g. L2s
        /// swapping one calculator) without touching handlers.
        /// </summary>
        public PrecompileRegistry WithGasCalculators(PrecompileGasCalculators gasCalculators)
        {
            if (gasCalculators == null) throw new ArgumentNullException(nameof(gasCalculators));
            if (ReferenceEquals(gasCalculators, _gasCalculators)) return this;
            return new PrecompileRegistry(gasCalculators, GetHandlers());
        }
    }
}
