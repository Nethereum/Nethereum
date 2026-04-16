using System;
using System.Collections.Generic;
using Nethereum.EVM.Execution.Precompiles.GasCalculators;

namespace Nethereum.EVM.Execution.Precompiles
{
    /// <summary>
    /// Immutable composition object that holds one
    /// <see cref="IPrecompileGasCalculator"/> per precompile address.
    /// Replaces the old <c>IPrecompileGasSchedule</c> inheritance chain
    /// (Cancun → Prague → Osaka) with a flat sparse-array bundle.
    ///
    /// Fork bundles are built by composition, not class inheritance:
    /// <see cref="PrecompileGasCalculatorSets.Prague"/> is literally
    /// <c>Cancun.With(...)</c> with specific slots replaced, and
    /// <see cref="PrecompileGasCalculatorSets.Osaka"/> is
    /// <c>Prague.With(...)</c>. Each <see cref="With(int, IPrecompileGasCalculator)"/>
    /// call returns a new bundle — the original is never mutated.
    ///
    /// Dispatch is the same O(1) bounds-check + array-index shape as
    /// <see cref="PrecompileRegistry"/>, and the two arrays (handlers and
    /// calculators) are intentionally kept in parallel so a future
    /// <c>ProtocolSpec</c> can swap either side independently.
    ///
    /// Entries are passed as <see cref="PrecompileGasCalculatorEntry"/>
    /// structs instead of named value tuples so the public API remains
    /// compatible with the older .NET Framework targets that link the
    /// EVM.Core source.
    /// </summary>
    public sealed class PrecompileGasCalculators
    {
        private readonly IPrecompileGasCalculator[] _byAddress;

        public PrecompileGasCalculators(IEnumerable<PrecompileGasCalculatorEntry> entries)
        {
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            _byAddress = BuildSparseArray(entries);
        }

        private PrecompileGasCalculators(IPrecompileGasCalculator[] byAddress)
        {
            _byAddress = byAddress;
        }

        private static IPrecompileGasCalculator[] BuildSparseArray(
            IEnumerable<PrecompileGasCalculatorEntry> entries)
        {
            // First pass: find the max address to size the sparse array.
            int max = -1;
            foreach (var e in entries)
            {
                if (e.Calculator == null) continue;
                if (e.Address < 0)
                    throw new ArgumentOutOfRangeException(nameof(entries),
                        "Precompile gas calculator address must be non-negative, got " + e.Address + ".");
                if (e.Address > max) max = e.Address;
            }
            if (max < 0) return new IPrecompileGasCalculator[0];

            // Second pass: populate. Later entries for the same address
            // win, matching the late-wins semantic of PrecompileRegistry.
            var arr = new IPrecompileGasCalculator[max + 1];
            foreach (var e in entries)
            {
                if (e.Calculator == null) continue;
                arr[e.Address] = e.Calculator;
            }
            return arr;
        }

        /// <summary>
        /// Returns the gas cost for the precompile at <paramref name="address"/>,
        /// or 0 if no calculator is installed at that slot. Callers are
        /// expected to check address existence via the handler registry
        /// before routing through here.
        /// </summary>
        public long GetGasCost(int address, byte[] input)
        {
            if (address < 0 || address >= _byAddress.Length) return 0;
            var calc = _byAddress[address];
            return calc == null ? 0 : calc.GetGasCost(input);
        }

        /// <summary>Direct access to the calculator at <paramref name="address"/>, or null.</summary>
        public IPrecompileGasCalculator Get(int address) =>
            address >= 0 && address < _byAddress.Length ? _byAddress[address] : null;

        /// <summary>Enumerates every address that has a calculator installed.</summary>
        public IEnumerable<int> GetAddresses()
        {
            for (int i = 0; i < _byAddress.Length; i++)
                if (_byAddress[i] != null)
                    yield return i;
        }

        /// <summary>
        /// Returns a new bundle with the calculator at <paramref name="address"/>
        /// replaced by <paramref name="calculator"/>. The original bundle is
        /// unchanged. Used by fork factories to layer EIP deltas on top of a
        /// parent composition.
        /// </summary>
        public PrecompileGasCalculators With(int address, IPrecompileGasCalculator calculator)
        {
            if (calculator == null) throw new ArgumentNullException(nameof(calculator));
            if (address < 0)
                throw new ArgumentOutOfRangeException(nameof(address),
                    "Precompile gas calculator address must be non-negative, got " + address + ".");

            int newLen = Math.Max(_byAddress.Length, address + 1);
            var next = new IPrecompileGasCalculator[newLen];
            Array.Copy(_byAddress, next, _byAddress.Length);
            next[address] = calculator;
            return new PrecompileGasCalculators(next);
        }

        /// <summary>
        /// Bulk override: returns a new bundle with multiple slots replaced
        /// in a single call. Convenience for fork bundles that touch several
        /// addresses at once (e.g. Prague adds 0x0b..0x11 on top of Cancun).
        /// </summary>
        public PrecompileGasCalculators With(params PrecompileGasCalculatorEntry[] overrides)
        {
            if (overrides == null || overrides.Length == 0) return this;

            int maxNewAddress = _byAddress.Length - 1;
            foreach (var o in overrides)
            {
                if (o.Calculator == null)
                    throw new ArgumentNullException(nameof(overrides),
                        "Calculator for address 0x" + o.Address.ToString("x") + " is null.");
                if (o.Address < 0)
                    throw new ArgumentOutOfRangeException(nameof(overrides),
                        "Precompile gas calculator address must be non-negative, got " + o.Address + ".");
                if (o.Address > maxNewAddress) maxNewAddress = o.Address;
            }

            var next = new IPrecompileGasCalculator[maxNewAddress + 1];
            Array.Copy(_byAddress, next, _byAddress.Length);
            foreach (var o in overrides)
                next[o.Address] = o.Calculator;

            return new PrecompileGasCalculators(next);
        }
    }
}
