using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AppChain.Genesis;
using Nethereum.AppChain.Policy.Bootstrap;
using Xunit;

namespace Nethereum.AppChain.IntegrationTests
{
    [Collection("Sequential")]
    public class AppChainE2ETests : IAsyncLifetime
    {
        private readonly AppChainE2ETestFixture _fixture;

        public AppChainE2ETests()
        {
            _fixture = new AppChainE2ETestFixture();
        }

        public async Task InitializeAsync()
        {
            await _fixture.InitializeAsync();
        }

        public Task DisposeAsync()
        {
            _fixture.Dispose();
            return Task.CompletedTask;
        }

        [Fact]
        public async Task Genesis_DeploysCreate2Factory()
        {
            var code = await _fixture.AppChain!.GetCodeAsync(Create2FactoryGenesisBuilder.CREATE2_FACTORY_ADDRESS);

            Assert.NotNull(code);
            Assert.True(code.Length > 0);
        }

        [Fact]
        public async Task Genesis_PrefundsSequencerAccount()
        {
            var balance = await _fixture.AppChain!.GetBalanceAsync(_fixture.SequencerAddress);

            Assert.True(balance > 0);
        }

        [Fact]
        public async Task Sequencer_StartsSuccessfully()
        {
            await _fixture.Sequencer!.StartAsync();

            var blockNumber = await _fixture.Sequencer.GetBlockNumberAsync();
            Assert.Equal(0, blockNumber);
        }

        [Fact]
        public async Task Sequencer_ProducesBlocks()
        {
            await _fixture.Sequencer!.StartAsync();

            await _fixture.Sequencer.ProduceBlockAsync();
            await _fixture.Sequencer.ProduceBlockAsync();

            var blockNumber = await _fixture.Sequencer.GetBlockNumberAsync();
            Assert.Equal(2, blockNumber);
        }

        [Fact]
        public async Task Sequencer_LatestBlockHasCorrectNumber()
        {
            await _fixture.Sequencer!.StartAsync();
            await _fixture.Sequencer.ProduceBlockAsync();

            var latestBlock = await _fixture.Sequencer.GetLatestBlockAsync();

            Assert.NotNull(latestBlock);
            Assert.Equal(1, latestBlock.BlockNumber);
        }

        [Fact]
        public async Task L1Node_StartsSuccessfully()
        {
            var height = await _fixture.L1Node!.GetBlockNumberAsync();

            Assert.True(height >= 0);
        }

        [Fact]
        public async Task L1Node_HasPrefundedAccount()
        {
            var balance = await _fixture.L1Node!.GetBalanceAsync(_fixture.SequencerAddress);

            Assert.True(balance > 0);
        }

        [Fact]
        public void AppChain_ExposesCreate2FactoryAddress()
        {
            Assert.Equal(Create2FactoryGenesisBuilder.CREATE2_FACTORY_ADDRESS, _fixture.AppChain!.Create2FactoryAddress);
        }

        [Fact]
        public async Task Create2Address_CalculationWorks()
        {
            var deployer = Create2FactoryGenesisBuilder.CREATE2_FACTORY_ADDRESS;
            var salt = new byte[32];
            var initCode = new byte[] { 0x60, 0x80, 0x60, 0x40 };

            var address = Create2FactoryGenesisBuilder.CalculateCreate2Address(deployer, salt, initCode);

            Assert.NotNull(address);
            Assert.StartsWith("0x", address);
            Assert.Equal(42, address.Length);

            var address2 = Create2FactoryGenesisBuilder.CalculateCreate2Address(deployer, salt, initCode);
            Assert.Equal(address, address2);
        }
    }

    [Collection("Sequential")]
    public class BootstrapPolicyE2ETests : IAsyncLifetime
    {
        private readonly AppChainE2ETestFixture _fixture;

        public BootstrapPolicyE2ETests()
        {
            _fixture = new AppChainE2ETestFixture();
        }

        public async Task InitializeAsync()
        {
            await _fixture.InitializeAsync();
        }

        public Task DisposeAsync()
        {
            _fixture.Dispose();
            return Task.CompletedTask;
        }

        [Fact]
        public async Task BootstrapPolicy_OpenAccess_AllowsAllWriters()
        {
            var config = BootstrapPolicyConfig.OpenAccess();
            var service = new BootstrapPolicyService(config);

            var isValid = await service.IsValidWriterAsync("0xany", Array.Empty<byte[]>());

            Assert.True(isValid);
        }

        [Fact]
        public async Task BootstrapPolicy_WithWriters_RestrictsAccess()
        {
            var config = new BootstrapPolicyConfig
            {
                AllowedWriters = new System.Collections.Generic.List<string> { _fixture.SequencerAddress }
            };
            var service = new BootstrapPolicyService(config);

            var isValid = await service.IsValidWriterAsync(_fixture.SequencerAddress, Array.Empty<byte[]>());
            var isInvalid = await service.IsValidWriterAsync("0xunauthorized", Array.Empty<byte[]>());

            Assert.True(isValid);
            Assert.False(isInvalid);
        }

        [Fact]
        public async Task BootstrapPolicy_GetPolicy_ReturnsConfig()
        {
            var config = new BootstrapPolicyConfig
            {
                MaxCalldataBytes = 200_000,
                MaxLogBytes = 2_000_000,
                BlockGasLimit = 50_000_000,
                SequencerAddress = _fixture.SequencerAddress
            };
            var service = new BootstrapPolicyService(config);

            var policy = await service.GetCurrentPolicyAsync();

            Assert.Equal(200_000, policy.MaxCalldataBytes);
            Assert.Equal(2_000_000, policy.MaxLogBytes);
            Assert.Equal(50_000_000, policy.BlockGasLimit);
        }

        [Fact]
        public void PolicyMigration_ComputesMerkleRoot()
        {
            var migrationService = new PolicyMigrationService();
            var config = new BootstrapPolicyConfig
            {
                AllowedWriters = new System.Collections.Generic.List<string>
                {
                    "0x1234567890123456789012345678901234567890",
                    "0x2345678901234567890123456789012345678901"
                }
            };

            var data = migrationService.PrepareMigrationData(config);

            Assert.NotNull(data.WritersRoot);
            Assert.Equal(32, data.WritersRoot.Length);
            Assert.False(Array.TrueForAll(data.WritersRoot, b => b == 0));
        }
    }

    [Collection("Sequential")]
    public class GenesisOptionsE2ETests
    {
        [Fact]
        public async Task Genesis_WithoutCreate2Factory_DoesNotDeployFactory()
        {
            var fixture = new AppChainE2ETestFixture();
            try
            {
                await fixture.InitializeAsync(deployCreate2Factory: false);

                var code = await fixture.AppChain!.GetCodeAsync(Create2FactoryGenesisBuilder.CREATE2_FACTORY_ADDRESS);
                Assert.Null(code);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Fact]
        public async Task Genesis_WithoutMudWorld_DoesNotDeployWorld()
        {
            var fixture = new AppChainE2ETestFixture();
            try
            {
                await fixture.InitializeAsync(deployCreate2Factory: true);

                var code = await fixture.AppChain!.GetCodeAsync(fixture.AppChain.WorldAddress);
                Assert.Null(code);
            }
            finally
            {
                fixture.Dispose();
            }
        }

    }
}
