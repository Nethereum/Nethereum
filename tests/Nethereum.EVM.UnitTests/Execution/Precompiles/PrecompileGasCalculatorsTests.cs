using System;
using Nethereum.EVM.Execution;
using Nethereum.EVM.Execution.Precompiles;
using Xunit;

namespace Nethereum.EVM.UnitTests.Execution.Precompiles
{
    public class PrecompileGasCalculatorsTests
    {
        private static string HexAddress(int addressNumeric) =>
            "0x" + addressNumeric.ToString("x").PadLeft(40, '0');

        [Theory]
        [InlineData(0x01, 0)]
        [InlineData(0x02, 0)]
        [InlineData(0x02, 64)]
        [InlineData(0x03, 0)]
        [InlineData(0x03, 128)]
        [InlineData(0x04, 0)]
        [InlineData(0x04, 256)]
        public void Cancun_bundle_matches_legacy_for_base_precompiles(int addressNumeric, int dataLen)
        {
            var legacy = new EvmPreCompiledContractsExecution();
            var bundle = PrecompileGasCalculatorSets.Cancun;

            var data = new byte[dataLen];
            var legacyGas = legacy.GetPrecompileGasCost(HexAddress(addressNumeric), data);
            var newGas = bundle.GetGasCost(addressNumeric, data);

            Assert.Equal(legacyGas, newGas);
        }

        [Theory]
        [InlineData(0x06)]
        [InlineData(0x07)]
        public void Cancun_bundle_returns_fixed_bn128_gas(int addressNumeric)
        {
            var legacy = new EvmPreCompiledContractsExecution();
            var bundle = PrecompileGasCalculatorSets.Cancun;

            var data = new byte[256];
            var legacyGas = legacy.GetPrecompileGasCost(HexAddress(addressNumeric), data);
            var newGas = bundle.GetGasCost(addressNumeric, data);

            Assert.Equal(legacyGas, newGas);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(192)]
        [InlineData(384)]
        [InlineData(1536)]
        public void Cancun_bundle_matches_legacy_for_bn128_pairing(int dataLen)
        {
            var legacy = new EvmPreCompiledContractsExecution();
            var bundle = PrecompileGasCalculatorSets.Cancun;

            var data = new byte[dataLen];
            var legacyGas = legacy.GetPrecompileGasCost(HexAddress(0x08), data);
            var newGas = bundle.GetGasCost(0x08, data);

            Assert.Equal(legacyGas, newGas);
        }

        [Theory]
        [InlineData(new byte[] { 0x00, 0x00, 0x00, 0x0c }, 12)]
        [InlineData(new byte[] { 0x00, 0x00, 0x00, 0xff }, 255)]
        public void Cancun_bundle_matches_legacy_for_blake2f(byte[] header, uint expectedRounds)
        {
            var bundle = PrecompileGasCalculatorSets.Cancun;
            var data = new byte[213];
            Array.Copy(header, data, 4);

            var newGas = bundle.GetGasCost(0x09, data);

            Assert.Equal((long)expectedRounds, newGas);
        }

        [Fact]
        public void Cancun_bundle_kzg_point_eval_is_fixed_50000()
        {
            var bundle = PrecompileGasCalculatorSets.Cancun;
            var data = new byte[192];
            Assert.Equal(50000L, bundle.GetGasCost(0x0a, data));
        }

        [Theory]
        [InlineData(1, 1, 1)]
        [InlineData(32, 32, 32)]
        [InlineData(64, 64, 64)]
        [InlineData(128, 32, 128)]
        public void Cancun_bundle_matches_legacy_for_modexp(int baseLen, int expLen, int modLen)
        {
            var legacy = new EvmPreCompiledContractsExecution();
            var bundle = PrecompileGasCalculatorSets.Cancun;
            var data = BuildModExpInput(baseLen, expLen, modLen);

            var legacyGas = legacy.GetPrecompileGasCost(HexAddress(0x05), data);
            var newGas = bundle.GetGasCost(0x05, data);

            Assert.Equal(legacyGas, newGas);
        }

        [Theory]
        [InlineData(0x0b, 256)]
        [InlineData(0x0c, 160)]
        [InlineData(0x0c, 320)]
        [InlineData(0x0c, 1600)]
        [InlineData(0x0d, 512)]
        [InlineData(0x0e, 288)]
        [InlineData(0x0e, 576)]
        [InlineData(0x0f, 0)]
        [InlineData(0x0f, 384)]
        [InlineData(0x0f, 768)]
        [InlineData(0x10, 64)]
        [InlineData(0x11, 128)]
        public void Prague_bundle_matches_legacy_for_bls12_precompiles(int addressNumeric, int dataLen)
        {
            var legacy = new EvmPreCompiledContractsExecution();
            var bundle = PrecompileGasCalculatorSets.Prague;
            var data = new byte[dataLen];

            var legacyGas = legacy.GetPrecompileGasCost(HexAddress(addressNumeric), data);
            var newGas = bundle.GetGasCost(addressNumeric, data);

            Assert.Equal(legacyGas, newGas);
        }

        [Fact]
        public void Prague_bundle_reuses_cancun_for_base_precompiles()
        {
            var cancun = PrecompileGasCalculatorSets.Cancun;
            var prague = PrecompileGasCalculatorSets.Prague;
            var data = new byte[256];

            for (int addr = 1; addr <= 0x0a; addr++)
                Assert.Equal(cancun.GetGasCost(addr, data), prague.GetGasCost(addr, data));
        }

        [Fact]
        public void Osaka_bundle_adds_p256verify()
        {
            var bundle = PrecompileGasCalculatorSets.Osaka;
            Assert.Equal(6900L, bundle.GetGasCost(0x100, new byte[160]));
        }

        [Fact]
        public void Osaka_bundle_reuses_prague_bls12()
        {
            var prague = PrecompileGasCalculatorSets.Prague;
            var osaka = PrecompileGasCalculatorSets.Osaka;
            var data = new byte[256];

            for (int addr = 0x0b; addr <= 0x11; addr++)
                Assert.Equal(prague.GetGasCost(addr, data), osaka.GetGasCost(addr, data));
        }

        [Theory]
        [InlineData(1, 1, 1)]
        [InlineData(32, 32, 32)]
        [InlineData(128, 32, 128)]
        public void Osaka_bundle_matches_legacy_modexp_with_eip7883(int baseLen, int expLen, int modLen)
        {
            var legacy = new EvmPreCompiledContractsExecution { EnableEIP7883 = true };
            var bundle = PrecompileGasCalculatorSets.Osaka;
            var data = BuildModExpInput(baseLen, expLen, modLen);

            var legacyGas = legacy.GetPrecompileGasCost(HexAddress(0x05), data);
            var newGas = bundle.GetGasCost(0x05, data);

            Assert.Equal(legacyGas, newGas);
        }

        [Fact]
        public void Osaka_modexp_has_higher_minimum_than_cancun()
        {
            var cancun = PrecompileGasCalculatorSets.Cancun;
            var osaka = PrecompileGasCalculatorSets.Osaka;
            var data = BuildModExpInput(1, 1, 1);

            Assert.Equal(200L, cancun.GetGasCost(0x05, data));
            Assert.Equal(500L, osaka.GetGasCost(0x05, data));
        }

        [Fact]
        public void All_bundles_return_zero_for_unknown_address()
        {
            var bundles = new PrecompileGasCalculators[]
            {
                PrecompileGasCalculatorSets.Cancun,
                PrecompileGasCalculatorSets.Prague,
                PrecompileGasCalculatorSets.Osaka
            };

            foreach (var bundle in bundles)
            {
                Assert.Equal(0L, bundle.GetGasCost(0x1234, new byte[0]));
                Assert.Equal(0L, bundle.GetGasCost(unchecked((int)0xdeadbeef), new byte[100]));
            }
        }

        private static byte[] BuildModExpInput(int baseLen, int expLen, int modLen)
        {
            var data = new byte[96 + baseLen + expLen + modLen];
            WriteU256BigEndian(data, 0,  baseLen);
            WriteU256BigEndian(data, 32, expLen);
            WriteU256BigEndian(data, 64, modLen);
            if (expLen > 0)
                data[96 + baseLen] = 0x02;
            return data;
        }

        private static void WriteU256BigEndian(byte[] data, int offset, int value)
        {
            data[offset + 28] = (byte)((value >> 24) & 0xff);
            data[offset + 29] = (byte)((value >> 16) & 0xff);
            data[offset + 30] = (byte)((value >> 8) & 0xff);
            data[offset + 31] = (byte)(value & 0xff);
        }
    }
}
