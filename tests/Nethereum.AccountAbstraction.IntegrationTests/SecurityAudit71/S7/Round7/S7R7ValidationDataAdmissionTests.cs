using Nethereum.DevChain;
using System.Numerics;
using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.IntegrationTests.E2E.Fixtures;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.SecurityAudit71.S7.Round7
{
    [Collection(DevChainBundlerFixture.COLLECTION_NAME)]
    [Trait("Category", "DevChainBundler")]
    public class S7R7ValidationDataAdmissionTests
    {
        private const long BlockRangeFlag = 0x800000000000;

        private readonly DevChainBundlerFixture _fixture;
        private readonly S7R7PaymasterSupport _support;

        public S7R7ValidationDataAdmissionTests(DevChainBundlerFixture fixture)
        {
            _fixture = fixture;
            _support = new S7R7PaymasterSupport(fixture);
        }

        private BundlerService NewBundler() =>
            new(_fixture.Node.CreateWeb3(_fixture.BundlerAccount), _support.SimulatingConfig());

        private async Task<string> AdmitAsync(BigInteger paymasterValidationData)
        {
            var paymaster = await _support.DeployPaymasterReturningAsync(paymasterValidationData);
            var (op, _) = await _support.PaymasterOpAsync(paymaster);
            using var bundler = NewBundler();
            return await bundler.SendUserOperationAsync(op, _support.EntryPoint);
        }

        [Fact]
        [Trait("Rule", "S7-R7-01")]
        [Trait("Erc", "EntryPoint._getValidationData VALIDITY_BLOCK_RANGE_FLAG")]
        public async Task SendUserOperation_WhenThePaymasterValidationDataIsAnOpenTimeRange_IsAdmitted()
        {
            var hash = await AdmitAsync(BigInteger.Zero);

            Assert.False(string.IsNullOrEmpty(hash));
        }

        [Fact]
        [Trait("Rule", "S7-R7-01")]
        [Trait("Erc", "EntryPoint._getValidationData VALIDITY_BLOCK_RANGE_FLAG")]
        public async Task SendUserOperation_WhenThePaymasterValidityIsABlockRangeThatContainsTheNextBlock_IsAdmitted()
        {
            var validAfter = new BigInteger(BlockRangeFlag);
            var validUntil = new BigInteger(BlockRangeFlag | 1_000_000_000L);
            var validationData = (validAfter << 208) | (validUntil << 160);

            var hash = await AdmitAsync(validationData);

            Assert.False(string.IsNullOrEmpty(hash));
        }

        [Fact]
        [Trait("Rule", "S7-R7-02")]
        [Trait("Erc", "EntryPoint._validateAccountAndPaymasterValidationData AA34")]
        public async Task SendUserOperation_WhenThePaymasterValidationDataNamesAnAggregator_IsRefusedAtAdmission()
        {
            var aggregator = BigInteger.Parse("0dEaD", System.Globalization.NumberStyles.HexNumber);

            await Assert.ThrowsAsync<BundlerRpcException>(() => AdmitAsync(aggregator));
        }
    }
}
