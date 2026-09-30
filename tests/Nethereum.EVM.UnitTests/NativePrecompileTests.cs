using System;
using System.Linq;
using Nethereum.EVM;
using Nethereum.EVM.Execution.Precompiles;
using Nethereum.EVM.Execution.Precompiles.Handlers;
using Nethereum.EVM.Precompiles;
using Nethereum.EVM.Precompiles.Bls;
using Nethereum.EVM.Precompiles.Kzg;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer.Bls;
using Nethereum.Signer.Bls.Herumi;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.EVM.UnitTests
{
    public class NativeBlsPrecompileTests
    {
        private readonly ITestOutputHelper _output;
        private readonly PrecompileRegistry _registry;

        public NativeBlsPrecompileTests(ITestOutputHelper output)
        {
            _output = output;
            var blsOps = new Bls12381Operations();
            _registry = DefaultPrecompileRegistries.PragueBase()
                .WithBlsBackend(blsOps);
        }

        [Fact]
        public void BlsRegistry_CanHandle_ReturnsCorrectAddresses()
        {
            Assert.True(_registry.CanHandle(0x0b));
            Assert.True(_registry.CanHandle(0x0c));
            Assert.True(_registry.CanHandle(0x0d));
            Assert.True(_registry.CanHandle(0x0e));
            Assert.True(_registry.CanHandle(0x0f));
            Assert.True(_registry.CanHandle(0x10));
            Assert.True(_registry.CanHandle(0x11));

            Assert.True(_registry.CanHandle(1));
        }

        [Fact]
        public void BlsRegistry_GetAddresses_IncludesBls()
        {
            int blsCount = 0;
            foreach (var addr in _registry.GetAddresses())
                if (addr >= 0x0b && addr <= 0x11) blsCount++;
            Assert.Equal(7, blsCount);
        }

        [Theory]
        [InlineData(0x0b, 256, 375)]
        [InlineData(0x0c, 160, 12000)]
        [InlineData(0x0d, 512, 600)]
        [InlineData(0x0e, 288, 22500)]
        [InlineData(0x10, 64, 5500)]
        [InlineData(0x11, 128, 23800)]
        public void BlsRegistry_GetGasCost_ReturnsCorrectGas(int address, int dataSize, int expectedGas)
        {
            var data = new byte[dataSize];
            var gas = _registry.GetGasCost(address, data);
            Assert.Equal(expectedGas, (int)gas);
        }

        [Theory]
        [MemberData(nameof(G1AddTestVectors))]
        public void G1Add_ExecutesCorrectly(string name, string input, string expected)
        {
            _output.WriteLine($"Running G1ADD test: {name}");

            var inputBytes = input.HexToByteArray();
            var expectedBytes = expected.HexToByteArray();

            var result = _registry.Execute(0x0b, inputBytes);

            Assert.Equal(expectedBytes.ToHex(), result.ToHex());
        }

        [Theory]
        [MemberData(nameof(G2AddTestVectors))]
        public void G2Add_ExecutesCorrectly(string name, string input, string expected)
        {
            _output.WriteLine($"Running G2ADD test: {name}");

            var inputBytes = input.HexToByteArray();
            var expectedBytes = expected.HexToByteArray();

            var result = _registry.Execute(0x0d, inputBytes);

            Assert.Equal(expectedBytes.ToHex(), result.ToHex());
        }

        [Theory]
        [MemberData(nameof(MapFpToG1TestVectors))]
        public void MapFpToG1_ExecutesCorrectly(string name, string input, string expected)
        {
            _output.WriteLine($"Running MAP_FP_TO_G1 test: {name}");

            var inputBytes = input.HexToByteArray();
            var expectedBytes = expected.HexToByteArray();

            var result = _registry.Execute(0x10, inputBytes);

            Assert.Equal(expectedBytes.ToHex(), result.ToHex());
        }

        [Fact]
        public void HardforkConfig_WithBlsBackend_Works()
        {
            var blsOps = new Bls12381Operations();
            var config = Nethereum.EVM.Precompiles.DefaultHardforkConfigs.Prague
                .WithBlsBackend(blsOps);

            Assert.NotNull(config.Precompiles);
            Assert.True(config.Precompiles.CanHandle(0x0b));
            Assert.True(config.Precompiles.CanHandle(1));
        }

        [Fact]
        public void Bls12381AwareRegistry_ExecutesWhereDefaultPlaceholderThrows()
        {
            var input = new byte[64];
            input[63] = 1;

            var placeholderOsaka = DefaultMainnetHardforkRegistry.Instance.Get(HardforkName.Osaka);
            Assert.True(placeholderOsaka.Precompiles.CanHandle(0x10));
            Assert.Throws<UnwiredPrecompileException>(() => placeholderOsaka.Precompiles.Execute(0x10, input));

            var aware = Bls12381AwareMainnetHardforkRegistry.Build(
                DefaultMainnetHardforkRegistry.Instance, new Bls12381Operations());
            var awareOsaka = aware.Get(HardforkName.Osaka);

            Assert.Equal(5500, (int)awareOsaka.Precompiles.GetGasCost(0x10, input));
            var result = awareOsaka.Precompiles.Execute(0x10, input);
            Assert.NotNull(result);
            Assert.Equal(128, result.Length);

            Assert.False(aware.Get(HardforkName.Cancun).Precompiles.CanHandle(0x10));
        }

        [Fact]
        public void MainnetNativeRegistry_LeavesNoPrecompileAsPlaceholder_Osaka()
        {
            var precompiles = Bls12381AwareMainnetHardforkRegistry.Build(
                    KzgAwareMainnetHardforkRegistry.Instance, new Bls12381Operations())
                .Get(HardforkName.Osaka).Precompiles;

            var placeholders = precompiles.GetAddresses()
                .Where(a => precompiles.Get(a) is PlaceholderPrecompile)
                .Select(a => "0x" + a.ToString("x"))
                .ToList();
            Assert.True(placeholders.Count == 0,
                "Precompiles with no backend wired (throwing placeholder): " + string.Join(", ", placeholders));

            foreach (var addr in new[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09,
                                         0x0a, 0x0b, 0x0c, 0x0d, 0x0e, 0x0f, 0x10, 0x11, 0x100 })
                Assert.True(precompiles.CanHandle(addr), $"missing precompile 0x{addr:x}");
        }

        public static TheoryData<string, string, string> G1AddTestVectors => new TheoryData<string, string, string>
        {
            {
                "bls_g1add_g1+p1",
                "0000000000000000000000000000000017f1d3a73197d7942695638c4fa9ac0fc3688c4f9774b905a14e3a3f171bac586c55e83ff97a1aeffb3af00adb22c6bb0000000000000000000000000000000008b3f481e3aaa0f1a09e30ed741d8ae4fcf5e095d5d00af600db18cb2c04b3edd03cc744a2888ae40caa232946c5e7e100000000000000000000000000000000112b98340eee2777cc3c14163dea3ec97977ac3dc5c70da32e6e87578f44912e902ccef9efe28d4a78b8999dfbca942600000000000000000000000000000000186b28d92356c4dfec4b5201ad099dbdede3781f8998ddf929b4cd7756192185ca7b8f4ef7088f813270ac3d48868a21",
                "000000000000000000000000000000000a40300ce2dec9888b60690e9a41d3004fda4886854573974fab73b046d3147ba5b7a5bde85279ffede1b45b3918d82d0000000000000000000000000000000006d3d887e9f53b9ec4eb6cedf5607226754b07c01ace7834f57f3e7315faefb739e59018e22c492006190fba4a870025"
            },
            {
                "bls_g1add_(g1+0=g1)",
                "0000000000000000000000000000000017f1d3a73197d7942695638c4fa9ac0fc3688c4f9774b905a14e3a3f171bac586c55e83ff97a1aeffb3af00adb22c6bb0000000000000000000000000000000008b3f481e3aaa0f1a09e30ed741d8ae4fcf5e095d5d00af600db18cb2c04b3edd03cc744a2888ae40caa232946c5e7e10000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000",
                "0000000000000000000000000000000017f1d3a73197d7942695638c4fa9ac0fc3688c4f9774b905a14e3a3f171bac586c55e83ff97a1aeffb3af00adb22c6bb0000000000000000000000000000000008b3f481e3aaa0f1a09e30ed741d8ae4fcf5e095d5d00af600db18cb2c04b3edd03cc744a2888ae40caa232946c5e7e1"
            },
            {
                "bls_g1add_(g1-g1=0)",
                "0000000000000000000000000000000017f1d3a73197d7942695638c4fa9ac0fc3688c4f9774b905a14e3a3f171bac586c55e83ff97a1aeffb3af00adb22c6bb0000000000000000000000000000000008b3f481e3aaa0f1a09e30ed741d8ae4fcf5e095d5d00af600db18cb2c04b3edd03cc744a2888ae40caa232946c5e7e10000000000000000000000000000000017f1d3a73197d7942695638c4fa9ac0fc3688c4f9774b905a14e3a3f171bac586c55e83ff97a1aeffb3af00adb22c6bb00000000000000000000000000000000114d1d6855d545a8aa7d76c8cf2e21f267816aef1db507c96655b9d5caac42364e6f38ba0ecb751bad54dcd6b939c2ca",
                "0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000"
            },
            {
                "bls_g1add_(g1+g1=2*g1)",
                "0000000000000000000000000000000017f1d3a73197d7942695638c4fa9ac0fc3688c4f9774b905a14e3a3f171bac586c55e83ff97a1aeffb3af00adb22c6bb0000000000000000000000000000000008b3f481e3aaa0f1a09e30ed741d8ae4fcf5e095d5d00af600db18cb2c04b3edd03cc744a2888ae40caa232946c5e7e10000000000000000000000000000000017f1d3a73197d7942695638c4fa9ac0fc3688c4f9774b905a14e3a3f171bac586c55e83ff97a1aeffb3af00adb22c6bb0000000000000000000000000000000008b3f481e3aaa0f1a09e30ed741d8ae4fcf5e095d5d00af600db18cb2c04b3edd03cc744a2888ae40caa232946c5e7e1",
                "000000000000000000000000000000000572cbea904d67468808c8eb50a9450c9721db309128012543902d0ac358a62ae28f75bb8f1c7c42c39a8c5529bf0f4e00000000000000000000000000000000166a9d8cabc673a322fda673779d8e3822ba3ecb8670e461f73bb9021d5fd76a4c56d9d4cd16bd1bba86881979749d28"
            }
        };

        public static TheoryData<string, string, string> G2AddTestVectors => new TheoryData<string, string, string>
        {
            {
                "g2_add(g2,0)",
                "00000000000000000000000000000000024aa2b2f08f0a91260805272dc51051c6e47ad4fa403b02b4510b647ae3d1770bac0326a805bbefd48056c8c121bdb80000000000000000000000000000000013e02b6052719f607dacd3a088274f65596bd0d09920b61ab5da61bbdc7f5049334cf11213945d57e5ac7d055d042b7e000000000000000000000000000000000ce5d527727d6e118cc9cdc6da2e351aadfd9baa8cbdd3a76d429a695160d12c923ac9cc3baca289e193548608b82801000000000000000000000000000000000606c4a02ea734cc32acd2b02bc28b99cb3e287e85a763af267492ab572e99ab3f370d275cec1da1aaa9075ff05f79be00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000",
                "00000000000000000000000000000000024aa2b2f08f0a91260805272dc51051c6e47ad4fa403b02b4510b647ae3d1770bac0326a805bbefd48056c8c121bdb80000000000000000000000000000000013e02b6052719f607dacd3a088274f65596bd0d09920b61ab5da61bbdc7f5049334cf11213945d57e5ac7d055d042b7e000000000000000000000000000000000ce5d527727d6e118cc9cdc6da2e351aadfd9baa8cbdd3a76d429a695160d12c923ac9cc3baca289e193548608b82801000000000000000000000000000000000606c4a02ea734cc32acd2b02bc28b99cb3e287e85a763af267492ab572e99ab3f370d275cec1da1aaa9075ff05f79be"
            }
        };

        public static TheoryData<string, string, string> MapFpToG1TestVectors => new TheoryData<string, string, string>
        {
            {
                "matter_fp_to_g1_0",
                "0000000000000000000000000000000014406e5bfb9209256a3820879a29ac2f62d6aca82324bf3ae2aa7d3c54792043bd8c791fccdb080c1a52dc68b8b69350",
                "000000000000000000000000000000000d7721bcdb7ce1047557776eb2659a444166dc6dd55c7ca6e240e21ae9aa18f529f04ac31d861b54faf3307692545db700000000000000000000000000000000108286acbdf4384f67659a8abe89e712a504cb3ce1cba07a716869025d60d499a00d1da8cdc92958918c222ea93d87f0"
            }
        };

    }

    public class NativeKzgPrecompileTests
    {
        private readonly ITestOutputHelper _output;
        private readonly PrecompileRegistry _kzgRegistry;

        public NativeKzgPrecompileTests(ITestOutputHelper output)
        {
            _output = output;
            CkzgOperations.InitializeFromEmbeddedSetup();
            _kzgRegistry = DefaultPrecompileRegistries.PragueBase()
                .WithKzgBackend(new CkzgOperations());
        }

        [Fact]
        public void KzgRegistry_CanHandle_ReturnsCorrectAddress()
        {
            Assert.True(_kzgRegistry.CanHandle(0x0a));
            Assert.True(_kzgRegistry.CanHandle(1));
            Assert.True(_kzgRegistry.CanHandle(0x0b));
        }

        [Fact]
        public void KzgRegistry_GetGasCost_Returns50000()
        {
            var gas = _kzgRegistry.GetGasCost(0x0a, new byte[192]);
            Assert.Equal(50000, (int)gas);
        }

        [Fact]
        public void HardforkConfig_WithKzgBackend_Works()
        {
            var config = Nethereum.EVM.Precompiles.DefaultHardforkConfigs.Prague
                .WithKzgBackend();

            Assert.NotNull(config.Precompiles);
            Assert.True(config.Precompiles.CanHandle(0x0a));
            Assert.True(config.Precompiles.CanHandle(1));
        }

        [Fact]
        public void HardforkConfig_WithBothBackends_Works()
        {
            var blsOps = new Bls12381Operations();
            var config = Nethereum.EVM.Precompiles.DefaultHardforkConfigs.Prague
                .WithBlsBackend(blsOps)
                .WithKzgBackend();

            Assert.NotNull(config.Precompiles);

            Assert.True(config.Precompiles.CanHandle(0x0b));
            Assert.True(config.Precompiles.CanHandle(0x11));

            Assert.True(config.Precompiles.CanHandle(0x0a));

            Assert.True(config.Precompiles.CanHandle(1));
            Assert.True(config.Precompiles.CanHandle(9));
        }

        [Fact]
        public void LightClient_And_EVM_Modes_Coexist()
        {
            var evmOps = new Bls12381Operations();

            var g1Point = "0000000000000000000000000000000017f1d3a73197d7942695638c4fa9ac0fc3688c4f9774b905a14e3a3f171bac586c55e83ff97a1aeffb3af00adb22c6bb0000000000000000000000000000000008b3f481e3aaa0f1a09e30ed741d8ae4fcf5e095d5d00af600db18cb2c04b3edd03cc744a2888ae40caa232946c5e7e1".HexToByteArray();
            var zeroPoint = new byte[128];

            var evmResult = evmOps.G1Add(g1Point, zeroPoint);
            Assert.Equal(128, evmResult.Length);
            Assert.Equal(g1Point, evmResult);

            var lightClient = new HerumiNativeBindings();
            lightClient.EnsureAvailableAsync(default).Wait();

            byte[] pubKeyBytes;
            byte[] sigBytes;
            byte[] message = System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes("light-client-test-message"));
            (pubKeyBytes, sigBytes) = MclSerialization.InEthMode(() =>
            {
                var secretKey = new mcl.BLS.SecretKey();
                secretKey.SetHashOf("test-coexistence-key");
                var publicKey = secretKey.GetPublicKey();

                var signature = secretKey.Sign(message);

                return (publicKey.Serialize(), signature.Serialize());
            });

            _output.WriteLine($"Light Client pubkey size: {pubKeyBytes.Length} bytes (expected 48 for compressed G1)");
            _output.WriteLine($"Light Client signature size: {sigBytes.Length} bytes (expected 96 for compressed G2)");

            Assert.Equal(48, pubKeyBytes.Length);
            Assert.Equal(96, sigBytes.Length);

            Assert.True(lightClient.VerifyAggregate(
                sigBytes,
                new[] { pubKeyBytes },
                new[] { message },
                null));

            var evmResult2 = evmOps.G1Add(g1Point, g1Point);
            Assert.Equal(128, evmResult2.Length);
            Assert.NotEqual(g1Point, evmResult2);

            var expected2G1 = "000000000000000000000000000000000572cbea904d67468808c8eb50a9450c9721db309128012543902d0ac358a62ae28f75bb8f1c7c42c39a8c5529bf0f4e00000000000000000000000000000000166a9d8cabc673a322fda673779d8e3822ba3ecb8670e461f73bb9021d5fd76a4c56d9d5caac42364e6f38ba0ecb751bad54dcd6b939c2ca".HexToByteArray();
            Assert.Equal(128, evmResult2.Length);

            Assert.True(lightClient.VerifyAggregate(
                sigBytes,
                new[] { pubKeyBytes },
                new[] { message },
                null));

            _output.WriteLine("SUCCESS: Light Client and EVM modes coexist!");
            _output.WriteLine($"EVM G1+0 result: {evmResult.ToHex().Substring(0, 64)}...");
            _output.WriteLine($"EVM G1+G1 result: {evmResult2.ToHex().Substring(0, 64)}...");
            _output.WriteLine("Light Client signature verification passed before and after EVM operations");
        }

        [Fact]
        public void Beacon_Eth2Signature_Verifies_After_Evm_BlsOperation()
        {
            var (pubKeyBytes, sigBytes, msg) = MclSerialization.InEthMode(() =>
            {
                var sk = new mcl.BLS.SecretKey();
                sk.SetHashOf("beacon-coexist-key");
                var pk = sk.GetPublicKey();
                var m = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("m"));
                var sig = sk.Sign(m);
                return (pk.Serialize(), sig.Serialize(), m);
            });

            Assert.Equal(48, pubKeyBytes.Length);
            Assert.Equal(96, sigBytes.Length);

            var g1Point = "0000000000000000000000000000000017f1d3a73197d7942695638c4fa9ac0fc3688c4f9774b905a14e3a3f171bac586c55e83ff97a1aeffb3af00adb22c6bb0000000000000000000000000000000008b3f481e3aaa0f1a09e30ed741d8ae4fcf5e095d5d00af600db18cb2c04b3edd03cc744a2888ae40caa232946c5e7e1".HexToByteArray();
            new Bls12381Operations().G1Add(g1Point, new byte[128]);

            Assert.True(new HerumiNativeBindings().VerifyAggregate(
                sigBytes,
                new[] { pubKeyBytes },
                new[] { msg },
                null));
        }
    }
}
