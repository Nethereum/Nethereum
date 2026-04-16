using System.Linq;
using Nethereum.EVM;
using Nethereum.EVM.Execution.Precompiles;
using Nethereum.EVM.Execution.Precompiles.Handlers;
using Nethereum.EVM.Precompiles;
using Nethereum.EVM.Precompiles.Bls;
using Nethereum.EVM.Precompiles.Kzg;
using Nethereum.Signer.Bls.Herumi;
using Xunit;

namespace Nethereum.EVM.UnitTests.Execution.Precompiles
{
    /// <summary>
    /// End-to-end tests for PrecompileRegistry composition — verifies the
    /// base factories wire the right handlers, that the BLS and KZG
    /// extension methods layer on top correctly (late-wins override),
    /// and that HardforkConfig preset wiring is consistent with the
    /// factories.
    /// </summary>
    public class PrecompileRegistryTests
    {
        [Fact]
        public void Cancun_base_has_self_contained_handlers_and_no_kzg()
        {
            var r = DefaultPrecompileRegistries.CancunBase();

            // Core handlers 0x01..0x09 are installed (async build).
            Assert.True(r.CanHandle(0x01));  // EcRecover
            Assert.True(r.CanHandle(0x02));  // Sha256
            Assert.True(r.CanHandle(0x03));  // Ripemd160
            Assert.True(r.CanHandle(0x04));  // Identity
            Assert.True(r.CanHandle(0x05));  // ModExp
            Assert.True(r.CanHandle(0x06));  // Bn128Add
            Assert.True(r.CanHandle(0x07));  // Bn128Mul
            Assert.True(r.CanHandle(0x08));  // Bn128Pairing
            Assert.True(r.CanHandle(0x09));  // Blake2f

            // KZG placeholder registered (address known to fork).
            Assert.True(r.CanHandle(0x0a));
            // BLS12-381 not in Cancun.
            Assert.False(r.CanHandle(0x0b));
            // P256VERIFY is an Osaka feature — not in Cancun base.
            Assert.False(r.CanHandle(0x100));
        }

        [Fact]
        public void Prague_base_uses_prague_gas_schedule_but_no_bls_handlers()
        {
            var r = DefaultPrecompileRegistries.PragueBase();

            // Same installed handlers as Cancun base.
            Assert.True(r.CanHandle(0x01));
            Assert.True(r.CanHandle(0x09));
            // BLS addresses have placeholders — real handlers installed via .WithBlsBackend(...).
            Assert.True(r.CanHandle(0x0b));
            // Gas schedule *does* know about BLS12 gas (positive number).
            Assert.True(r.GetGasCost(0x0b, new byte[256]) > 0);

            // Gas calculator bundle is the Prague composition
            // (Cancun.With(...) adding BLS12 gas rules — no class
            // inheritance). Same reference as PrecompileGasCalculatorSets.Prague.
            Assert.Same(PrecompileGasCalculatorSets.Prague, r.GasCalculators);
        }

        [Fact]
        public void Osaka_base_has_p256verify_and_uses_osaka_gas_schedule()
        {
            var r = DefaultPrecompileRegistries.OsakaBase();

            // P256VERIFY is only in Osaka.
            Assert.True(r.CanHandle(0x100));
            Assert.Equal(6900L, r.GetGasCost(0x100, new byte[160]));

            Assert.Same(PrecompileGasCalculatorSets.Osaka, r.GasCalculators);
        }

        [Fact]
        public void WithBlsBackend_installs_seven_bls_handlers()
        {
            var herumi = new Bls12381Operations();
            var r = DefaultPrecompileRegistries.PragueBase().WithBlsBackend(herumi);

            for (int addr = 0x0b; addr <= 0x11; addr++)
            {
                Assert.True(r.CanHandle(addr), $"Expected handler at 0x{addr:x}");
                Assert.NotNull(r.Get(addr));
            }

            // Base handlers still present.
            Assert.True(r.CanHandle(0x01));
        }

        [Fact]
        public void WithKzgBackend_installs_kzg_point_evaluation_handler()
        {
            var r = DefaultPrecompileRegistries.CancunBase().WithKzgBackend();

            Assert.True(r.CanHandle(0x0a));
            Assert.NotNull(r.Get(0x0a));
            // Base handlers still present.
            Assert.True(r.CanHandle(0x04));
        }

        [Fact]
        public void WithBlsBackend_and_WithKzgBackend_compose()
        {
            var herumi = new Bls12381Operations();
            var r = DefaultPrecompileRegistries.PragueBase()
                .WithKzgBackend()
                .WithBlsBackend(herumi);

            // Both backends active.
            Assert.True(r.CanHandle(0x0a));
            for (int addr = 0x0b; addr <= 0x11; addr++)
                Assert.True(r.CanHandle(addr));
        }

        [Fact]
        public void HardforkConfig_WithBlsBackend_upgrades_registry()
        {
            var herumi = new Bls12381Operations();
            var config = HardforkConfig.Prague
                .WithPrecompiles(DefaultPrecompileRegistries.PragueBase())
                .WithBlsBackend(herumi);

            Assert.NotNull(config.Precompiles);
            Assert.True(config.Precompiles.CanHandle(0x0b));
            // Original HardforkConfig.Prague is unchanged (immutable) — its
            // Precompiles is still null because Core does not know the
            // default crypto backends.
            Assert.Null(HardforkConfig.Prague.Precompiles);
        }

        [Fact]
        public void HardforkConfig_WithKzgBackend_upgrades_registry()
        {
            var config = HardforkConfig.Cancun
                .WithPrecompiles(DefaultPrecompileRegistries.CancunBase())
                .WithKzgBackend();

            Assert.NotNull(config.Precompiles);
            Assert.True(config.Precompiles.CanHandle(0x0a));
            // Original HardforkConfig.Cancun is unchanged — its Precompiles
            // is still null because Core does not know the default crypto
            // backends.
            Assert.Null(HardforkConfig.Cancun.Precompiles);
        }

        [Fact]
        public void GetAddresses_returns_installed_handler_addresses()
        {
            var r = DefaultPrecompileRegistries.CancunBase();
            var addresses = r.GetAddresses().ToHashSet();

            Assert.Contains(0x01, addresses);
            Assert.Contains(0x09, addresses);
            Assert.Contains(0x0a, addresses);  // KZG placeholder
            Assert.DoesNotContain(0x0b, addresses);  // BLS not in Cancun
        }

        [Fact]
        public void WithHandlers_is_late_wins()
        {
            var r = DefaultPrecompileRegistries.CancunBase();
            var originalIdentity = r.Get(0x04);

            // Replace 0x04 with a different IdentityPrecompile instance.
            var replacement = new IdentityPrecompile();
            var upgraded = r.WithHandlers(replacement);

            Assert.Same(replacement, upgraded.Get(0x04));
            // Original registry unchanged.
            Assert.Same(originalIdentity, r.Get(0x04));
        }

        [Fact]
        public void Execute_throws_for_unknown_address()
        {
            var r = DefaultPrecompileRegistries.CancunBase();
            Assert.Throws<System.InvalidOperationException>(() => r.Execute(0x1234, new byte[0]));
        }
    }
}
