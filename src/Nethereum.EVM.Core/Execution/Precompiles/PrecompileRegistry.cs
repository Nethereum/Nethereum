using System;
using System.Collections.Generic;

namespace Nethereum.EVM.Execution.Precompiles
{
    public sealed class PrecompileRegistry : IPrecompileRegistry
    {
        private readonly IPrecompileHandler[] _handlers;
        private readonly PrecompileGasCalculators _gasCalculators;

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
            int max = -1;
            foreach (var h in handlers)
            {
                if (h == null) continue;
                if (h.AddressNumeric > max) max = h.AddressNumeric;
            }
            if (max < 0) return new IPrecompileHandler[0];

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
        /// The addresses whose backend is actually wired and will execute here - excludes a
        /// <see cref="Handlers.PlaceholderPrecompile"/>, which occupies a spec address (KZG,
        /// BLS12-381) whose native backend was not loaded and throws on call. Use this to
        /// report what a simulator can call in this environment; use <see cref="GetAddresses"/>
        /// for the spec set (EIP-2929 warming, EIP-7562 allow-lists).
        /// </summary>
        public IEnumerable<int> GetWiredAddresses()
        {
            for (int i = 0; i < _handlers.Length; i++)
                if (_handlers[i] != null && !(_handlers[i] is Handlers.PlaceholderPrecompile))
                    yield return i;
        }

        public IEnumerable<IPrecompileHandler> GetHandlers()
        {
            for (int i = 0; i < _handlers.Length; i++)
                if (_handlers[i] != null)
                    yield return _handlers[i];
        }

        public PrecompileRegistry WithHandlers(params IPrecompileHandler[] additional)
        {
            if (additional == null || additional.Length == 0) return this;

            var combined = new List<IPrecompileHandler>();
            foreach (var h in GetHandlers()) combined.Add(h);
            foreach (var h in additional) if (h != null) combined.Add(h);

            return new PrecompileRegistry(_gasCalculators, combined);
        }

        public PrecompileRegistry WithGasCalculators(PrecompileGasCalculators gasCalculators)
        {
            if (gasCalculators == null) throw new ArgumentNullException(nameof(gasCalculators));
            if (ReferenceEquals(gasCalculators, _gasCalculators)) return this;
            return new PrecompileRegistry(gasCalculators, GetHandlers());
        }
    }
}
