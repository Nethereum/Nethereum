using System;
using Nethereum.EVM.Execution.Precompiles;
using Xunit;

namespace Nethereum.EVM.UnitTests.Execution.Precompiles
{
    public class PrecompileGasCalculatorsTests
    {
        [Theory]
        [InlineData(0x01, 0, 3000)]
        [InlineData(0x02, 0, 60)]
        [InlineData(0x02, 64, 84)]
        [InlineData(0x03, 0, 600)]
        [InlineData(0x03, 128, 1080)]
        [InlineData(0x04, 0, 15)]
        [InlineData(0x04, 256, 39)]
        public void Cancun_bundle_prices_the_base_precompiles_by_their_base_and_per_word_gas(int addressNumeric, int dataLen, long expectedGas)
        {
            var bundle = PrecompileGasCalculatorSets.Cancun;

            Assert.Equal(expectedGas, bundle.GetGasCost(addressNumeric, new byte[dataLen]));
        }

        [Theory]
        [InlineData(0x06, 150)]
        [InlineData(0x07, 6000)]
        public void Cancun_bundle_returns_fixed_bn128_gas(int addressNumeric, long expectedGas)
        {
            var bundle = PrecompileGasCalculatorSets.Cancun;

            Assert.Equal(expectedGas, bundle.GetGasCost(addressNumeric, new byte[256]));
        }

        [Theory]
        [InlineData(0, 45000)]
        [InlineData(192, 79000)]
        [InlineData(384, 113000)]
        [InlineData(1536, 317000)]
        public void Cancun_bundle_prices_bn128_pairing_per_pair(int dataLen, long expectedGas)
        {
            var bundle = PrecompileGasCalculatorSets.Cancun;

            Assert.Equal(expectedGas, bundle.GetGasCost(0x08, new byte[dataLen]));
        }

        [Theory]
        [InlineData(new byte[] { 0x00, 0x00, 0x00, 0x0c }, 12)]
        [InlineData(new byte[] { 0x00, 0x00, 0x00, 0xff }, 255)]
        public void Cancun_bundle_charges_one_gas_per_blake2f_round(byte[] header, uint expectedRounds)
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
        [InlineData(1, 1, 1, 200)]
        [InlineData(32, 32, 32, 1328)]
        [InlineData(64, 64, 64, 10773)]
        [InlineData(128, 32, 128, 21248)]
        public void Cancun_bundle_prices_modexp_by_eip2565(int baseLen, int expLen, int modLen, long expectedGas)
        {
            var bundle = PrecompileGasCalculatorSets.Cancun;
            var data = BuildModExpInput(baseLen, expLen, modLen);

            Assert.Equal(expectedGas, bundle.GetGasCost(0x05, data));
        }

        [Theory]
        [InlineData(0x0b, 256, 375)]
        [InlineData(0x0c, 160, 12000)]
        [InlineData(0x0c, 320, 22776)]
        [InlineData(0x0c, 1600, 85440)]
        [InlineData(0x0d, 512, 600)]
        [InlineData(0x0e, 288, 22500)]
        [InlineData(0x0e, 576, 45000)]
        [InlineData(0x0f, 0, 37700)]
        [InlineData(0x0f, 384, 70300)]
        [InlineData(0x0f, 768, 102900)]
        [InlineData(0x10, 64, 5500)]
        [InlineData(0x11, 128, 23800)]
        public void Prague_bundle_prices_bls12_precompiles_by_eip2537(int addressNumeric, int dataLen, long expectedGas)
        {
            var bundle = PrecompileGasCalculatorSets.Prague;

            Assert.Equal(expectedGas, bundle.GetGasCost(addressNumeric, new byte[dataLen]));
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
        [InlineData(1, 1, 1, 500)]
        [InlineData(32, 32, 32, 3984)]
        [InlineData(128, 32, 128, 127488)]
        public void Osaka_bundle_prices_modexp_by_eip7883(int baseLen, int expLen, int modLen, long expectedGas)
        {
            var bundle = PrecompileGasCalculatorSets.Osaka;
            var data = BuildModExpInput(baseLen, expLen, modLen);

            Assert.Equal(expectedGas, bundle.GetGasCost(0x05, data));
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
