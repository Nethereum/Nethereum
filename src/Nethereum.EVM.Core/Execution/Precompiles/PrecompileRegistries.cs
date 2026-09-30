using System;
using System.Collections.Generic;
using Nethereum.EVM.Execution.Precompiles.CryptoBackends;
using Nethereum.EVM.Execution.Precompiles.GasCalculators;
using Nethereum.EVM.Execution.Precompiles.Handlers;
using Nethereum.EVM.Hardforks;

namespace Nethereum.EVM.Execution.Precompiles
{
    public static class PrecompileRegistries
    {
        public static IEnumerable<IPrecompileHandler> FrontierHandlers(
            IEcRecoverBackend ecRecover,
            ISha256Backend sha256,
            IRipemd160Backend ripemd160)
        {
            yield return new EcRecoverPrecompile(ecRecover);
            yield return new Sha256Precompile(sha256);
            yield return new Ripemd160Precompile(ripemd160);
            yield return new IdentityPrecompile();
        }

        public static IEnumerable<IPrecompileHandler> CoreHandlers(
            IEcRecoverBackend ecRecover,
            ISha256Backend sha256,
            IRipemd160Backend ripemd160,
            IModExpBackend modExp,
            IBn128Backend bn128,
            IBlake2fBackend blake2f,
            bool enforceModExpBounds = false)
        {
            if (ecRecover == null) throw new ArgumentNullException(nameof(ecRecover));
            if (sha256 == null) throw new ArgumentNullException(nameof(sha256));
            if (ripemd160 == null) throw new ArgumentNullException(nameof(ripemd160));
            if (modExp == null) throw new ArgumentNullException(nameof(modExp));
            if (bn128 == null) throw new ArgumentNullException(nameof(bn128));

            yield return new EcRecoverPrecompile(ecRecover);
            yield return new Sha256Precompile(sha256);
            yield return new Ripemd160Precompile(ripemd160);
            yield return new IdentityPrecompile();
            yield return new ModExpPrecompile(modExp, enforceModExpBounds);
            yield return new Bn128AddPrecompile(bn128);
            yield return new Bn128MulPrecompile(bn128);
            yield return new Bn128PairingPrecompile(bn128);
            if (blake2f != null)
                yield return new Blake2fPrecompile(blake2f);
        }

        public static PrecompileRegistry FromSpec(PrecompileSpec[] precompiles, IPrecompileExecutorFactory factory)
        {
            if (precompiles == null) throw new ArgumentNullException(nameof(precompiles));
            if (factory == null) throw new ArgumentNullException(nameof(factory));

            var handlers = new List<IPrecompileHandler>(precompiles.Length);
            var entries = new PrecompileGasCalculatorEntry[precompiles.Length];

            for (int i = 0; i < precompiles.Length; i++)
            {
                var p = precompiles[i];
                handlers.Add(factory.GetHandler(p));
                entries[i] = new PrecompileGasCalculatorEntry(p.Address, factory.GetGasCalculator(p));
            }

            return new PrecompileRegistry(new PrecompileGasCalculators(entries), handlers);
        }

        public static PrecompileRegistry WithGas(
            PrecompileGasCalculators gasCalculators,
            IEcRecoverBackend ecRecover,
            ISha256Backend sha256,
            IRipemd160Backend ripemd160,
            IModExpBackend modExp,
            IBn128Backend bn128,
            IBlake2fBackend blake2f = null,
            bool addKzgPlaceholder = false,
            bool addBlsPlaceholders = false)
        {
            var handlers = new List<IPrecompileHandler>(
                CoreHandlers(ecRecover, sha256, ripemd160, modExp, bn128, blake2f));
            if (addKzgPlaceholder)
                handlers.Add(new PlaceholderPrecompile(0x0a));
            if (addBlsPlaceholders)
                for (int addr = 0x0b; addr <= 0x11; addr++)
                    handlers.Add(new PlaceholderPrecompile(addr));
            return new PrecompileRegistry(gasCalculators, handlers);
        }

        public static PrecompileRegistry CancunBase(
            IEcRecoverBackend ecRecover,
            ISha256Backend sha256,
            IRipemd160Backend ripemd160,
            IModExpBackend modExp,
            IBn128Backend bn128,
            IBlake2fBackend blake2f)
        {
            var handlers = new List<IPrecompileHandler>(
                CoreHandlers(ecRecover, sha256, ripemd160, modExp, bn128, blake2f));
            handlers.Add(new PlaceholderPrecompile(0x0a));
            return new PrecompileRegistry(PrecompileGasCalculatorSets.Cancun, handlers);
        }

        public static PrecompileRegistry PragueBase(
            IEcRecoverBackend ecRecover,
            ISha256Backend sha256,
            IRipemd160Backend ripemd160,
            IModExpBackend modExp,
            IBn128Backend bn128,
            IBlake2fBackend blake2f)
        {
            var handlers = new List<IPrecompileHandler>(
                CoreHandlers(ecRecover, sha256, ripemd160, modExp, bn128, blake2f));
            handlers.Add(new PlaceholderPrecompile(0x0a));
            for (int addr = 0x0b; addr <= 0x11; addr++)
                handlers.Add(new PlaceholderPrecompile(addr));
            return new PrecompileRegistry(PrecompileGasCalculatorSets.Prague, handlers);
        }

        public static PrecompileRegistry OsakaBase(
            IEcRecoverBackend ecRecover,
            ISha256Backend sha256,
            IRipemd160Backend ripemd160,
            IModExpBackend modExp,
            IBn128Backend bn128,
            IBlake2fBackend blake2f,
            IP256VerifyBackend p256Verify)
        {
            if (p256Verify == null) throw new ArgumentNullException(nameof(p256Verify));

            var handlers = new List<IPrecompileHandler>(
                CoreHandlers(ecRecover, sha256, ripemd160, modExp, bn128, blake2f,
                    enforceModExpBounds: true));
            handlers.Add(new PlaceholderPrecompile(0x0a));
            for (int addr = 0x0b; addr <= 0x11; addr++)
                handlers.Add(new PlaceholderPrecompile(addr));
            handlers.Add(new P256VerifyPrecompile(p256Verify));
            return new PrecompileRegistry(PrecompileGasCalculatorSets.Osaka, handlers);
        }
    }
}
