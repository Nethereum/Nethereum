using Nethereum.Documentation;
using System;
using System.Collections.Generic;

namespace Nethereum.EVM.Execution.Precompiles
{
    public sealed class PrecompileRegistry : IPrecompileRegistry
    {
        private readonly IPrecompileHandler[] _handlers;
        private readonly HashSet<Nethereum.Util.EvmAddress> _addresses;
        private readonly PrecompileGasCalculators _gasCalculators;
        private readonly IPrecompileCallObserver _observer;

        public PrecompileGasCalculators GasCalculators => _gasCalculators;

        public PrecompileRegistry(
            PrecompileGasCalculators gasCalculators,
            IEnumerable<IPrecompileHandler> handlers)
        {
            if (gasCalculators == null) throw new ArgumentNullException(nameof(gasCalculators));
            if (handlers == null) throw new ArgumentNullException(nameof(handlers));

            _gasCalculators = gasCalculators;
            _handlers = BuildSparseArray(handlers);
            _addresses = BuildAddressSet(_handlers);
        }

        private PrecompileRegistry(
            PrecompileGasCalculators gasCalculators, IPrecompileHandler[] handlers, IPrecompileCallObserver observer)
        {
            _gasCalculators = gasCalculators;
            _handlers = handlers;
            _observer = observer;
            _addresses = BuildAddressSet(handlers);
        }

        [NethereumDocExample(DocSection.EvmSimulator, "precompile-outcome", "Wrap a registry with a call observer")]
        public PrecompileRegistry WithCallObserver(IPrecompileCallObserver observer)
        {
            if (observer == null) throw new ArgumentNullException(nameof(observer));
            return new PrecompileRegistry(_gasCalculators, _handlers, observer);
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

        private static HashSet<Nethereum.Util.EvmAddress> BuildAddressSet(IPrecompileHandler[] handlers)
        {
            var addresses = new HashSet<Nethereum.Util.EvmAddress>();
            for (int i = 0; i < handlers.Length; i++)
                if (handlers[i] != null)
                    addresses.Add(Nethereum.Util.EvmAddress.From(new[] { (byte)(i >> 24), (byte)(i >> 16), (byte)(i >> 8), (byte)i }));
            return addresses;
        }

        [NethereumDocExample(DocSection.EvmSimulator, "precompile-outcome", "Whether a registry serves an address")]
        public bool Handles(Nethereum.Util.EvmAddress address) => _addresses.Contains(address);

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

        [NethereumDocExample(DocSection.EvmSimulator, "precompile-outcome", "Run a precompile and fail as a value")]
        public bool TryExecute(int address, byte[] input, out byte[] output)
        {
            var handler = Get(address);
            if (handler == null)
                throw new InvalidOperationException(
                    $"No precompile handler installed at address 0x{address:x} on this spec.");
            _observer?.OnCall(address);
            if (handler is PrecompileHandlerBase outcomeHandler)
                return outcomeHandler.TryExecute(input, out output);
            return TryExecuteThroughLegacyHandler(handler, input, out output);
        }

        private static bool TryExecuteThroughLegacyHandler(IPrecompileHandler handler, byte[] input, out byte[] output)
        {
            try
            {
                output = handler.Execute(input);
                return true;
            }
            catch (Exception ex) when (!BlockchainState.EvmHostException.IsHostOrSystemFault(ex))
            {
                output = null;
                return false;
            }
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

            return new PrecompileRegistry(_gasCalculators, BuildSparseArray(combined), _observer);
        }

        public PrecompileRegistry WithGasCalculators(PrecompileGasCalculators gasCalculators)
        {
            if (gasCalculators == null) throw new ArgumentNullException(nameof(gasCalculators));
            if (ReferenceEquals(gasCalculators, _gasCalculators)) return this;
            return new PrecompileRegistry(gasCalculators, _handlers, _observer);
        }
    }
}
