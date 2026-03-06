using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AppChain.Sequencer.Builder;
using Nethereum.Web3.Accounts;
using Xunit;

namespace Nethereum.AppChain.IntegrationTests.Builder
{
    [Collection("Sequential")]
    public class AppChainBuilderTests
    {
        private const string TestPrivateKey = "0x12345678901234567890123456789012345678901234567890123456789012ab";
        private const int TestChainId = 420420;

        [Fact]
        public async Task Given_MinimalConfig_When_BuildAsync_Then_ChainStarts()
        {
            await using var chain = await new AppChainBuilder("TestChain", TestChainId)
                .WithOperator(TestPrivateKey)
                .BuildAsync();

            var blockNumber = await chain.GetBlockNumberAsync();

            Assert.Equal(BigInteger.Zero, blockNumber);
            Assert.NotNull(chain.AppChain);
            Assert.NotNull(chain.Sequencer);
            Assert.NotNull(chain.Node);
            Assert.NotNull(chain.RpcClient);
            Assert.NotNull(chain.Web3);
        }

        [Fact]
        public async Task Given_OperatorWithPrivateKey_When_BuildAsync_Then_OperatorAddressSet()
        {
            var account = new Account(TestPrivateKey, TestChainId);

            await using var chain = await new AppChainBuilder("TestChain", TestChainId)
                .WithOperator(TestPrivateKey)
                .BuildAsync();

            Assert.Equal(account.Address.ToLower(), chain.OperatorAddress.ToLower());
        }

        [Fact]
        public async Task Given_OperatorWithIAccount_When_BuildAsync_Then_OperatorAddressSet()
        {
            var account = new Account(TestPrivateKey, TestChainId);

            await using var chain = await new AppChainBuilder("TestChain", TestChainId)
                .WithOperator(account)
                .BuildAsync();

            Assert.Equal(account.Address.ToLower(), chain.OperatorAddress.ToLower());
        }

        [Fact]
        public async Task Given_ChainWithConfig_When_GetProperties_Then_ReturnsCorrectValues()
        {
            await using var chain = await new AppChainBuilder("MyTestChain", 999999)
                .WithOperator(TestPrivateKey)
                .BuildAsync();

            Assert.Equal("MyTestChain", chain.ChainName);
            Assert.Equal(new BigInteger(999999), chain.ChainId);
        }

        [Fact]
        public async Task Given_ChainBuilt_When_ProduceBlockAsync_Then_BlockNumberIncreases()
        {
            await using var chain = await new AppChainBuilder("TestChain", TestChainId)
                .WithOperator(TestPrivateKey)
                .BuildAsync();

            await chain.ProduceBlockAsync();
            await chain.ProduceBlockAsync();

            var blockNumber = await chain.GetBlockNumberAsync();
            Assert.Equal(new BigInteger(2), blockNumber);
        }

        [Fact]
        public async Task Given_ChainBuilt_When_GetBalanceAsync_Then_OperatorHasBalance()
        {
            await using var chain = await new AppChainBuilder("TestChain", TestChainId)
                .WithOperator(TestPrivateKey)
                .BuildAsync();

            var balance = await chain.GetBalanceAsync(chain.OperatorAddress);

            Assert.True(balance > 0);
        }

        [Fact]
        public async Task Given_CustomBaseFee_When_BuildAsync_Then_ChainUsesCustomFee()
        {
            await using var chain = await new AppChainBuilder("TestChain", TestChainId)
                .WithOperator(TestPrivateKey)
                .WithBaseFee(0)
                .BuildAsync();

            Assert.NotNull(chain.AppChain);
            Assert.Equal((ulong)0, chain.AppChain.Config.BaseFee);
        }

        [Fact]
        public async Task Given_CustomBlockGasLimit_When_BuildAsync_Then_ChainUsesCustomLimit()
        {
            await using var chain = await new AppChainBuilder("TestChain", TestChainId)
                .WithOperator(TestPrivateKey)
                .WithBlockGasLimit(50_000_000)
                .BuildAsync();

            Assert.NotNull(chain.AppChain);
            Assert.Equal((ulong)50_000_000, chain.AppChain.Config.BlockGasLimit);
        }

        [Fact]
        public async Task Given_InMemoryStorage_When_BuildAsync_Then_ChainStarts()
        {
            await using var chain = await new AppChainBuilder("TestChain", TestChainId)
                .WithOperator(TestPrivateKey)
                .WithStorage(StorageType.InMemory)
                .BuildAsync();

            var blockNumber = await chain.GetBlockNumberAsync();
            Assert.Equal(BigInteger.Zero, blockNumber);
        }

        [Fact]
        public async Task Given_RocksDbStorage_When_BuildAsync_Then_ThrowsNotSupported()
        {
            var builder = new AppChainBuilder("TestChain", TestChainId)
                .WithOperator(TestPrivateKey)
                .WithStorage(StorageType.RocksDb, "./test-data");

            await Assert.ThrowsAsync<NotSupportedException>(() => builder.BuildAsync());
        }
    }

    [Collection("Sequential")]
    public class AppChainBuilderTrustModelTests
    {
        private const string TestPrivateKey = "0x12345678901234567890123456789012345678901234567890123456789012ab";
        private const int TestChainId = 420420;
        private const string AdminAddress = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        [Fact]
        public async Task Given_OpenTrustModel_When_BuildAsync_Then_ChainStarts()
        {
            await using var chain = await new AppChainBuilder("TestChain", TestChainId)
                .WithOperator(TestPrivateKey)
                .WithTrust(TrustModel.Open)
                .BuildAsync();

            Assert.NotNull(chain.AppChain);
        }

        [Fact]
        public async Task Given_WhitelistTrustModel_When_BuildAsync_Then_ChainStarts()
        {
            await using var chain = await new AppChainBuilder("TestChain", TestChainId)
                .WithOperator(TestPrivateKey)
                .WithTrust(TrustModel.Whitelist, AdminAddress)
                .BuildAsync();

            Assert.NotNull(chain.AppChain);
        }

        [Fact]
        public async Task Given_InviteTreeTrustModel_When_BuildAsync_Then_ChainStarts()
        {
            await using var chain = await new AppChainBuilder("TestChain", TestChainId)
                .WithOperator(TestPrivateKey)
                .WithTrust(TrustModel.InviteTree, maxInvites: 5)
                .BuildAsync();

            Assert.NotNull(chain.AppChain);
        }
    }

    [Collection("Sequential")]
    public class AppChainBuilderAATests
    {
        private const string TestPrivateKey = "0x12345678901234567890123456789012345678901234567890123456789012ab";
        private const int TestChainId = 420420;

        [Fact]
        public async Task Given_AAEnabled_When_BuildAsync_Then_ChainStarts()
        {
            await using var chain = await new AppChainBuilder("TestChain", TestChainId)
                .WithOperator(TestPrivateKey)
                .WithAA()
                .BuildAsync();

            Assert.NotNull(chain.AppChain);
        }

        [Fact]
        public async Task Given_AASponsoredPaymaster_When_BuildAsync_Then_ChainStarts()
        {
            await using var chain = await new AppChainBuilder("TestChain", TestChainId)
                .WithOperator(TestPrivateKey)
                .WithAA(PaymasterType.Sponsored)
                .BuildAsync();

            Assert.NotNull(chain.AppChain);
        }
    }

    [Collection("Sequential")]
    public class AppChainPresetsTests
    {
        private const string TestPrivateKey = "0x12345678901234567890123456789012345678901234567890123456789012ab";
        private const string AdminAddress = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        [Fact]
        public async Task Given_GamingPreset_When_BuildAsync_Then_ChainStarts()
        {
            await using var chain = await AppChainPresets
                .ForGaming("GameChain", 420420, TestPrivateKey)
                .BuildAsync();

            Assert.NotNull(chain.AppChain);
            Assert.Equal("GameChain", chain.ChainName);
        }

        [Fact]
        public async Task Given_SocialPreset_When_BuildAsync_Then_ChainStarts()
        {
            await using var chain = await AppChainPresets
                .ForSocial("SocialChain", 420421, TestPrivateKey, maxInvites: 3)
                .BuildAsync();

            Assert.NotNull(chain.AppChain);
            Assert.Equal("SocialChain", chain.ChainName);
        }

        [Fact]
        public async Task Given_EnterprisePreset_When_BuildAsync_Then_ChainStarts()
        {
            await using var chain = await AppChainPresets
                .ForEnterprise("CorpChain", 420422, TestPrivateKey, AdminAddress)
                .BuildAsync();

            Assert.NotNull(chain.AppChain);
            Assert.Equal("CorpChain", chain.ChainName);
        }

        [Fact]
        public async Task Given_TestingPreset_When_BuildAsync_Then_ChainStartsWithZeroBaseFee()
        {
            await using var chain = await AppChainPresets
                .ForTesting("TestChain", 420423, TestPrivateKey)
                .BuildAsync();

            Assert.NotNull(chain.AppChain);
            Assert.Equal((ulong)0, chain.AppChain.Config.BaseFee);
        }

        [Fact]
        public async Task Given_GamingPresetWithIAccount_When_BuildAsync_Then_ChainStarts()
        {
            var account = new Account(TestPrivateKey, 420420);

            await using var chain = await AppChainPresets
                .ForGaming("GameChain", 420420, account)
                .BuildAsync();

            Assert.NotNull(chain.AppChain);
            Assert.Equal(account.Address.ToLower(), chain.OperatorAddress.ToLower());
        }
    }

    [Collection("Sequential")]
    public class AppChainBuilderValidationTests
    {
        [Fact]
        public async Task Given_NoOperator_When_BuildAsync_Then_ThrowsInvalidOperation()
        {
            var builder = new AppChainBuilder("TestChain", 420420);

            await Assert.ThrowsAsync<InvalidOperationException>(() => builder.BuildAsync());
        }

        [Fact]
        public void Given_NullName_When_Construct_Then_ThrowsArgumentNull()
        {
            Assert.Throws<ArgumentNullException>(() => new AppChainBuilder(null!, 420420));
        }

        [Fact]
        public void Given_EmptyPrivateKey_When_WithOperator_Then_ThrowsArgumentException()
        {
            var builder = new AppChainBuilder("TestChain", 420420);

            Assert.Throws<ArgumentException>(() => builder.WithOperator(""));
        }

        [Fact]
        public void Given_NullPrivateKey_When_WithOperator_Then_ThrowsArgumentException()
        {
            var builder = new AppChainBuilder("TestChain", 420420);

            Assert.Throws<ArgumentException>(() => builder.WithOperator((string)null!));
        }

        [Fact]
        public void Given_WrongTrustModelForMaxInvites_When_WithTrust_Then_ThrowsArgumentException()
        {
            var builder = new AppChainBuilder("TestChain", 420420);

            Assert.Throws<ArgumentException>(() => builder.WithTrust(TrustModel.Open, maxInvites: 3));
        }

        [Fact]
        public void Given_WrongTrustModelForAdmin_When_WithTrust_Then_ThrowsArgumentException()
        {
            var builder = new AppChainBuilder("TestChain", 420420);

            Assert.Throws<ArgumentException>(() => builder.WithTrust(TrustModel.Open, admin: "0x123"));
        }
    }
}
