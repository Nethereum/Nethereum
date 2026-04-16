using System;
using System.Collections.Generic;
using Nethereum.EVM.Execution.Precompiles.CryptoBackends;
using Nethereum.EVM.Execution.Precompiles.Handlers;

namespace Nethereum.EVM.Execution.Precompiles
{
    /// <summary>
    /// Static factory for the per-fork base precompile registries. Each
    /// factory returns a fresh immutable <see cref="PrecompileRegistry"/>
    /// populated with the handlers that fork requires, wired up against
    /// caller-supplied crypto backends.
    ///
    /// The backends are the only injection point — no crypto library is
    /// referenced from <c>Nethereum.EVM.Core</c> directly. Production
    /// callers wire the managed defaults via
    /// <c>DefaultPrecompileRegistries</c> in <c>Nethereum.EVM</c>; the
    /// Zisk sync path wires witness-backed implementations via
    /// <c>ZiskPrecompileRegistries</c> in <c>Nethereum.EVM.Zisk</c>.
    ///
    /// BLS12-381 (0x0b..0x11) and KZG point evaluation (0x0a) are layered
    /// on top via the extension methods in the backend packages:
    ///   * <c>.WithBlsBackend(IBls12381Operations)</c>
    ///     (<c>Nethereum.EVM.Precompiles.Bls</c>)
    ///   * <c>.WithKzgBackend(IKzgOperations)</c>
    ///     (<c>Nethereum.EVM.Precompiles.Kzg</c>)
    /// </summary>
    public static class PrecompileRegistries
    {
        /// <summary>
        /// The always-available precompile handlers for a Cancun-class
        /// fork: ECRECOVER, SHA256, RIPEMD160, IDENTITY, MODEXP,
        /// BN128 ADD/MUL/PAIRING, BLAKE2F. Every handler takes its
        /// crypto backend via constructor injection; this factory only
        /// wires the ones that need a backend.
        /// </summary>
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
            if (blake2f == null) throw new ArgumentNullException(nameof(blake2f));

            yield return new EcRecoverPrecompile(ecRecover);
            yield return new Sha256Precompile(sha256);
            yield return new Ripemd160Precompile(ripemd160);
            yield return new IdentityPrecompile();
            yield return new ModExpPrecompile(modExp, enforceModExpBounds);
            yield return new Bn128AddPrecompile(bn128);
            yield return new Bn128MulPrecompile(bn128);
            yield return new Bn128PairingPrecompile(bn128);
            yield return new Blake2fPrecompile(blake2f);
        }

        /// <summary>
        /// Cancun base registry: core handlers + the Cancun gas calculator
        /// bundle from <see cref="PrecompileGasCalculatorSets.Cancun"/>
        /// (EIP-2565 MODEXP, EIP-1108 BN128, EIP-152 BLAKE2F, EIP-4844 KZG
        /// gas constant). The KZG handler itself (0x0a) is NOT installed —
        /// callers that want EIP-4844 point evaluation layer a real backend
        /// on top via <c>.WithKzgBackend(IKzgOperations)</c> from
        /// <c>Nethereum.EVM.Precompiles.Kzg</c>.
        /// </summary>
        public static PrecompileRegistry WithGas(
            PrecompileGasCalculators gasCalculators,
            IEcRecoverBackend ecRecover,
            ISha256Backend sha256,
            IRipemd160Backend ripemd160,
            IModExpBackend modExp,
            IBn128Backend bn128,
            IBlake2fBackend blake2f,
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
            handlers.Add(new PlaceholderPrecompile(0x0a)); // KZG
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
            handlers.Add(new PlaceholderPrecompile(0x0a)); // KZG
            for (int addr = 0x0b; addr <= 0x11; addr++)
                handlers.Add(new PlaceholderPrecompile(addr)); // BLS12-381
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
            handlers.Add(new PlaceholderPrecompile(0x0a)); // KZG
            for (int addr = 0x0b; addr <= 0x11; addr++)
                handlers.Add(new PlaceholderPrecompile(addr)); // BLS12-381
            handlers.Add(new P256VerifyPrecompile(p256Verify));
            return new PrecompileRegistry(PrecompileGasCalculatorSets.Osaka, handlers);
        }
    }
}
