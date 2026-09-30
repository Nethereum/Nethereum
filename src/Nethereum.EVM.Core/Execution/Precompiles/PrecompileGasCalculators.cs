using System;
using System.Collections.Generic;
using Nethereum.EVM.Execution.Precompiles.GasCalculators;

namespace Nethereum.EVM.Execution.Precompiles
{
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

            var arr = new IPrecompileGasCalculator[max + 1];
            foreach (var e in entries)
            {
                if (e.Calculator == null) continue;
                arr[e.Address] = e.Calculator;
            }
            return arr;
        }

        public long GetGasCost(int address, byte[] input)
        {
            if (address < 0 || address >= _byAddress.Length) return 0;
            var calc = _byAddress[address];
            return calc == null ? 0 : calc.GetGasCost(input);
        }

        public IPrecompileGasCalculator Get(int address) =>
            address >= 0 && address < _byAddress.Length ? _byAddress[address] : null;

        public IEnumerable<int> GetAddresses()
        {
            for (int i = 0; i < _byAddress.Length; i++)
                if (_byAddress[i] != null)
                    yield return i;
        }

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
