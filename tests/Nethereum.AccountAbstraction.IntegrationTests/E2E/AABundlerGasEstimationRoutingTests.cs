using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AccountAbstraction.Bundler.InProcess;
using Nethereum.AccountAbstraction.IntegrationTests.E2E.Fixtures;
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter;
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter.ContractDefinition;
using Nethereum.Signer;
using Nethereum.Web3;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.E2E
{
    [Collection(DevChainBundlerFixture.COLLECTION_NAME)]
    [Trait("Category", "AAContractHandler")]
    [Trait("ERC", "4337")]
    public class AABundlerGasEstimationRoutingTests
    {
        private readonly DevChainBundlerFixture _fixture;

        public AABundlerGasEstimationRoutingTests(DevChainBundlerFixture fixture)
        {
            _fixture = fixture;
        }

        private async Task<(string accountAddress, EthECKey accountKey, FactoryConfig factoryConfig)> CreateAccountWithFactoryAsync(ulong salt, decimal ethAmount)
        {
            var accountKey = EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();

            var accountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(ownerAddress, salt);
            await _fixture.FundAccountAsync(accountAddress, ethAmount);

            var factoryConfig = new FactoryConfig(
                _fixture.AccountFactoryService.ContractAddress,
                ownerAddress,
                salt);

            return (accountAddress, accountKey, factoryConfig);
        }

        private TestCounterService CreateCounterServiceOnRestrictedNode(string counterAddress)
        {
            var operatorAccount = new Nethereum.Web3.Accounts.Account(
                _fixture.OperatorPrivateKey, DevChainBundlerFixture.CHAIN_ID);
            var nodeWithoutEstimateGas = new Web3.Web3(
                operatorAccount,
                new EstimateGasRejectingRpcClient(_fixture.Web3.Client));
            return new TestCounterService(nodeWithoutEstimateGas, counterAddress);
        }

        private static (BigInteger verificationGas, BigInteger callGas) UnpackAccountGasLimits(byte[] accountGasLimits)
        {
            var verificationGas = new BigInteger(accountGasLimits.Take(16).Reverse().Concat(new byte[] { 0 }).ToArray());
            var callGas = new BigInteger(accountGasLimits.Skip(16).Take(16).Reverse().Concat(new byte[] { 0 }).ToArray());
            return (verificationGas, callGas);
        }

        [Fact]
        public async Task SendRequest_EstimatesGasThroughBundler_NeverThroughNode()
        {
            var (accountAddress, accountKey, factoryConfig) = await CreateAccountWithFactoryAsync(4101, 10m);

            var counter = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new TestCounterDeployment());

            var counterService = CreateCounterServiceOnRestrictedNode(counter.ContractAddress);

            counterService.ChangeContractHandlerToAA(
                accountAddress,
                accountKey,
                new BundlerServiceAdapter(_fixture.BundlerService, DevChainBundlerFixture.CHAIN_ID),
                _fixture.EntryPointService.ContractAddress,
                factory: factoryConfig);

            var receipt = await counterService.CountRequestAndWaitForReceiptAsync();

            var aaReceipt = Assert.IsType<AATransactionReceipt>(receipt);
            Assert.True(aaReceipt.UserOpSuccess,
                $"UserOperation should succeed without node eth_estimateGas. Revert: {aaReceipt.RevertReason}");

            var count = await counter.CountersQueryAsync(accountAddress);
            Assert.Equal(BigInteger.One, count);

            var secondReceipt = await counterService.CountRequestAndWaitForReceiptAsync();

            var secondAaReceipt = Assert.IsType<AATransactionReceipt>(secondReceipt);
            Assert.True(secondAaReceipt.UserOpSuccess,
                $"Deployed-account UserOperation should succeed without node eth_estimateGas. Revert: {secondAaReceipt.RevertReason}");

            var secondCount = await counter.CountersQueryAsync(accountAddress);
            Assert.Equal(new BigInteger(2), secondCount);
        }

        [Fact]
        public async Task CreateUserOperation_AppliesGasConfigMultipliersAndBuffers()
        {
            var (accountAddress, accountKey, factoryConfig) = await CreateAccountWithFactoryAsync(4102, 10m);

            var counter = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new TestCounterDeployment());

            var counterService = CreateCounterServiceOnRestrictedNode(counter.ContractAddress);

            var handler = counterService.ChangeContractHandlerToAA(
                accountAddress,
                accountKey,
                new BundlerServiceAdapter(_fixture.BundlerService, DevChainBundlerFixture.CHAIN_ID),
                _fixture.EntryPointService.ContractAddress,
                factory: factoryConfig);

            handler.WithGasConfig(new AAGasConfig());
            var defaultOp = await handler.CreateUserOperationAsync(new CountFunction());
            var (defaultVerificationGas, defaultCallGas) = UnpackAccountGasLimits(defaultOp.AccountGasLimits);

            var callGasBuffer = new BigInteger(12345);
            handler.WithGasConfig(new AAGasConfig
            {
                CallGasMultiplier = 2.0m,
                CallGasBuffer = callGasBuffer,
                PreVerificationGasBuffer = 1000
            });

            var configuredOp = await handler.CreateUserOperationAsync(new CountFunction());
            var (configuredVerificationGas, configuredCallGas) = UnpackAccountGasLimits(configuredOp.AccountGasLimits);

            Assert.Equal(defaultCallGas * 2 + callGasBuffer, configuredCallGas);
            Assert.Equal(defaultVerificationGas, configuredVerificationGas);
            Assert.Equal(defaultOp.PreVerificationGas + 1000, configuredOp.PreVerificationGas);
        }
    }
}
