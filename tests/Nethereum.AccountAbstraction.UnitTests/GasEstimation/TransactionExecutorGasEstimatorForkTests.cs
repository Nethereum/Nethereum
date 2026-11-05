using System.Numerics;
using Nethereum.AccountAbstraction.Bundler.GasEstimation;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Precompiles;
using Xunit;

namespace Nethereum.AccountAbstraction.UnitTests.GasEstimation
{
    public class TransactionExecutorGasEstimatorForkTests
    {
        private const string Sender = "0x1000000000000000000000000000000000000001";
        private const int DeployedCodeBytes = 1000;
        private const long StateGasPerByte = 1530;

        private static byte[] InitCodeDeployingZeroedCode()
        {
            var prefix = new byte[] { 0x61, 0x03, 0xe8, 0x80, 0x60, 0x0c, 0x60, 0x00, 0x39, 0x60, 0x00, 0xf3 };
            var initCode = new byte[prefix.Length + DeployedCodeBytes];
            prefix.CopyTo(initCode, 0);
            return initCode;
        }

        private static TransactionExecutorGasEstimator EstimatorAt(HardforkConfig fork)
            => new TransactionExecutorGasEstimator(
                new InMemoryStateReader(new Dictionary<string, AccountState>()), 1, fork);

        [Fact]
        public async Task Given_ADeploymentAtAmsterdam_When_TheExecutorEstimatesIt_Then_TheEstimateCoversTheCodeDepositStateGasAndRunsIt()
        {
            var estimator = EstimatorAt(DefaultHardforkConfigs.For(HardforkName.Amsterdam));
            var initCode = InitCodeDeployingZeroedCode();

            var estimate = await estimator.EstimateGasAsync(Sender, null, initCode, BigInteger.Zero, 30_000_000);

            Assert.True(estimate.Success, estimate.Error);
            Assert.True(estimate.GasUsed >= DeployedCodeBytes * StateGasPerByte,
                $"estimate {estimate.GasUsed} omits the {DeployedCodeBytes * StateGasPerByte} code-deposit state gas");

            var rerun = await estimator.EstimateGasAsync(Sender, null, initCode, BigInteger.Zero, (long)estimate.GasUsed);
            Assert.True(rerun.Success, $"the deployment ran out of gas at its own estimate {estimate.GasUsed}: {rerun.Error}");
        }
    }
}
