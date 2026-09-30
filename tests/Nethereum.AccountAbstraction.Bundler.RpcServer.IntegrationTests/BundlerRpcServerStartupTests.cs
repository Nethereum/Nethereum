using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AccountAbstraction.Bundler.RpcServer.Configuration;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;
using Xunit;

namespace Nethereum.AccountAbstraction.Bundler.RpcServer.IntegrationTests
{
    public class BundlerRpcServerConfigParityTests
    {
        private static BundlerRpcServerConfig Minimal() => new()
        {
            BeneficiaryAddress = "0x0000000000000000000000000000000000dEaD",
            SupportedEntryPoints = new[] { "0x0000000000000000000000000000000000dEaD" }
        };

        [Fact]
        public void ToBundlerConfig_ForwardsEveryNewlySurfacedField()
        {
            var config = Minimal();
            config.SkipUnderpricedOpsInAutoBundle = false;
            config.BundleReceiptTimeoutSeconds = 42;
            config.ReceiptLogLookbackBlocks = 777;
            config.MaxUnstakedSenderMempoolCount = 9;
            config.WhitelistedAddresses = new HashSet<string> { "0xabc" };
            config.BlacklistedAddresses = new HashSet<string> { "0xdef" };
            config.Hardfork = "prague";

            var bundlerConfig = config.ToBundlerConfig();

            Assert.False(bundlerConfig.SkipUnderpricedOpsInAutoBundle);
            Assert.Equal(42, bundlerConfig.BundleReceiptTimeoutSeconds);
            Assert.Equal(new BigInteger(777), bundlerConfig.ReceiptLogLookbackBlocks);
            Assert.Equal(9, bundlerConfig.MaxUnstakedSenderMempoolCount);
            Assert.Contains("0xabc", bundlerConfig.WhitelistedAddresses);
            Assert.Contains("0xdef", bundlerConfig.BlacklistedAddresses);
            Assert.Equal("prague", bundlerConfig.Hardfork);
        }

        [Fact]
        public void Defaults_MatchBundlerConfigDefaults_SoBehaviourIsUnchangedWhenUnset()
        {
            var server = Minimal();
            var engineDefaults = new BundlerConfig();
            var mapped = server.ToBundlerConfig();

            Assert.Equal(engineDefaults.SkipUnderpricedOpsInAutoBundle, mapped.SkipUnderpricedOpsInAutoBundle);
            Assert.Equal(engineDefaults.BundleReceiptTimeoutSeconds, mapped.BundleReceiptTimeoutSeconds);
            Assert.Equal(engineDefaults.ReceiptLogLookbackBlocks, mapped.ReceiptLogLookbackBlocks);
            Assert.Equal(engineDefaults.MaxUnstakedSenderMempoolCount, mapped.MaxUnstakedSenderMempoolCount);
            Assert.Equal(engineDefaults.Hardfork, mapped.Hardfork);
            Assert.Empty(mapped.WhitelistedAddresses);
            Assert.Empty(mapped.BlacklistedAddresses);
        }

        [Fact]
        public void WhitelistBlacklist_MatchOperatorAddressesCaseInsensitively()
        {
            // Operators naturally supply EIP-55 checksummed addresses; the engine compares against a
            // lowercased sender, so the mapped sets must match regardless of the operator's casing.
            var config = Minimal();
            config.WhitelistedAddresses = new System.Collections.Generic.HashSet<string> { "0xAbCdEf0000000000000000000000000000000001" };
            config.BlacklistedAddresses = new System.Collections.Generic.HashSet<string> { "0xFeDcBa0000000000000000000000000000000002" };

            var mapped = config.ToBundlerConfig();

            Assert.Contains("0xabcdef0000000000000000000000000000000001", mapped.WhitelistedAddresses);
            Assert.Contains("0xfedcba0000000000000000000000000000000002", mapped.BlacklistedAddresses);
        }
    }

    public class BundlerRpcServerStartupTests
    {
        private const string EntryPoint = "0x0000000000000000000000000000000000000007";

        private static BundlerRpcServerConfig ConfigWithSigner(BigInteger chainId) => new()
        {
            BeneficiaryAddress = "0x0000000000000000000000000000000000dEaD",
            SupportedEntryPoints = new[] { EntryPoint },
            PrivateKey = "0x0000000000000000000000000000000000000000000000000000000000000001",
            ChainId = chainId
        };

        private static Web3.IWeb3 Web3For(string chainIdHex) =>
            new Web3.Web3(new StubClient(chainIdHex));

        [Fact]
        public async Task Startup_ChainIdMismatch_ThrowsNamingBothIds()
        {
            var config = ConfigWithSigner(chainId: 1);
            var web3 = Web3For("0x2105");

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                BundlerRpcServerStartup.ValidateAndResolveChainIdAsync(config, web3));

            Assert.Contains("1", ex.Message);
            Assert.Contains("8453", ex.Message);
        }

        [Fact]
        public async Task Startup_ChainIdMatches_Succeeds()
        {
            var config = ConfigWithSigner(chainId: 8453);
            var web3 = Web3For("0x2105");

            await BundlerRpcServerStartup.ValidateAndResolveChainIdAsync(config, web3);

            Assert.Equal(new BigInteger(8453), config.ChainId);
        }

        [Fact]
        public async Task Startup_ChainIdUnset_AdoptsNodeChainId()
        {
            var config = ConfigWithSigner(chainId: 0);
            var web3 = Web3For("0x2105");

            await BundlerRpcServerStartup.ValidateAndResolveChainIdAsync(config, web3);

            Assert.Equal(new BigInteger(8453), config.ChainId);
        }

        [Fact]
        public void RequireSigner_True_NoSigner_Throws()
        {
            var config = new BundlerRpcServerConfig
            {
                BeneficiaryAddress = "0x0000000000000000000000000000000000dEaD",
                SupportedEntryPoints = new[] { EntryPoint },
                RequireSigner = true,
                PrivateKey = null
            };

            var ex = Assert.Throws<InvalidOperationException>(() =>
                BundlerRpcServerStartup.EnsureSignerConfigured(config));

            Assert.Contains("signer", ex.Message);
        }

        [Fact]
        public void RequireSigner_True_WithSigner_DoesNotThrow()
        {
            var config = ConfigWithSigner(chainId: 1);

            BundlerRpcServerStartup.EnsureSignerConfigured(config);
        }

        [Fact]
        public void RequireSigner_False_NoSigner_DoesNotThrow()
        {
            var config = new BundlerRpcServerConfig
            {
                BeneficiaryAddress = "0x0000000000000000000000000000000000dEaD",
                SupportedEntryPoints = new[] { EntryPoint },
                RequireSigner = false,
                PrivateKey = null
            };

            BundlerRpcServerStartup.EnsureSignerConfigured(config);
        }

        [Fact]
        public void RequireSigner_DefaultsToTrue()
        {
            Assert.True(new BundlerRpcServerConfig().RequireSigner);
        }

        private sealed class StubClient : ClientBase
        {
            private readonly string _chainIdHex;

            public StubClient(string chainIdHex) => _chainIdHex = chainIdHex;

            public override Task<RpcResponseMessage> SendAsync(RpcRequestMessage rpcRequestMessage, string? route = null) =>
                Task.FromResult(new RpcResponseMessage(rpcRequestMessage.Id, _chainIdHex));

            protected override Task<RpcResponseMessage[]> SendAsync(RpcRequestMessage[] requests) =>
                throw new System.NotSupportedException();
        }
    }
}
