using System.Threading.Tasks;
using Nethereum.DevChain;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.DevChain
{
    public class EstimateContractCreationGasForkTests
    {
        private const int DeployedByteCount = 32;

        private static byte[] InitCodeReturning32Bytes() =>
            new byte[] { 0x60, 0x20, 0x60, 0x00, 0xf3 };

        private const long PreAmsterdamPerByte = 200;
        private const long AmsterdamPerStateByte = 1530;

        private static async Task<long> EvmAndDepositGasAsync(string hardfork)
        {
            var config = DevChainConfig.Default;
            config.Hardfork = hardfork;

            using var node = new DevChainNode(config);
            await node.StartAsync();

            var result = await node.EstimateContractCreationGasAsync(InitCodeReturning32Bytes());

            Assert.True(result.Success, $"creation should succeed at {hardfork}: {result.RevertReason}");
            Assert.Equal(DeployedByteCount, result.ReturnData.Length);
            return (long)(result.GasUsed - result.IntrinsicGasUsed);
        }

        [Fact]
        [Trait("Category", "EIP8037")]
        public async Task Given_ContractCreation_When_EstimatedAtAmsterdam_Then_CostsMoreThanPreAmsterdam()
        {
            var prague = await EvmAndDepositGasAsync("prague");
            var amsterdam = await EvmAndDepositGasAsync("amsterdam");

            Assert.True(amsterdam > prague,
                $"Amsterdam creation estimate ({amsterdam}) must exceed pre-Amsterdam ({prague}); " +
                "equal values mean the flat pre-Amsterdam deposit cost is being applied at every fork");

            var minimumIncrease = DeployedByteCount * (AmsterdamPerStateByte - PreAmsterdamPerByte);
            Assert.True(amsterdam - prague >= minimumIncrease,
                $"expected the Amsterdam estimate to exceed pre-Amsterdam by at least {minimumIncrease} " +
                $"({DeployedByteCount} bytes x ({AmsterdamPerStateByte} - {PreAmsterdamPerByte})), " +
                $"but the difference was {amsterdam - prague}");
        }

        [Fact]
        [Trait("Category", "EIP8037")]
        public async Task Given_ContractCreation_When_EstimatedAtPrague_Then_ChargesFlatPerByteDeposit()
        {
            var estimate = await EvmAndDepositGasAsync("prague");

            var deposit = DeployedByteCount * PreAmsterdamPerByte;
            Assert.True(estimate > deposit,
                $"estimate ({estimate}) must include the {deposit} deposit charge plus execution");
            Assert.True(estimate < deposit + 1000,
                $"estimate ({estimate}) should be the {deposit} deposit plus only a few gas of " +
                "execution for a 5-byte init code; a much larger value means an Amsterdam-shaped " +
                "charge is leaking into a pre-Amsterdam fork");
        }

        [Fact]
        [Trait("Category", "EIP8037")]
        public async Task Given_ContractCreation_When_EstimatedAtAmsterdam_Then_ChargesStateByteDeposit()
        {
            var estimate = await EvmAndDepositGasAsync("amsterdam");

            var deposit = DeployedByteCount * AmsterdamPerStateByte;
            Assert.True(estimate >= deposit,
                $"Amsterdam estimate ({estimate}) must cover the {deposit} state-byte deposit " +
                $"({DeployedByteCount} bytes x {AmsterdamPerStateByte})");
        }
    }
}
