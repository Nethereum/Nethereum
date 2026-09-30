using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.AppChain.Server;
using Nethereum.AppChain.Server.Configuration;
using Nethereum.ChainNode.Hosting.Configuration;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.AppChain.IntegrationTests
{
    [Collection("Sequential")]
    public class AppChainEphemeralDevKeyTests
    {
        [Fact]
        public async Task Given_NoIdentityFlagsAtAll_When_AppChainComposes_Then_ItGeneratesEphemeralKeysAndLogsDevMode()
        {
            var config = BareConfig();
            Assert.True(config.IsFullyUnconfigured);

            var recordingLoggerFactory = new RecordingLoggerFactory();
            var signRecoverableBeforeCompose = EthECKey.SignRecoverable;

            AppChainComposedNode composed = null;
            try
            {
                composed = await AppChainComposition.ComposeAsync(config, recordingLoggerFactory, CancellationToken.None);

                Assert.True(config.Genesis.Owner.CanSign);
                Assert.True(config.Consensus.Sequencer.CanSign);
                Assert.True(composed.Produces);
                Assert.Contains(recordingLoggerFactory.Messages,
                    m => m == "AppChain dev mode: generated ephemeral keys (NOT for production)");
            }
            finally
            {
                if (composed != null) await composed.DisposeAsync();
                EthECKey.SignRecoverable = signRecoverableBeforeCompose;
            }
        }

        [Fact]
        public async Task Given_AGenesisOwnerPrivateKeyButNoOtherIdentity_When_AppChainComposes_Then_ItFailsCleanlyInsteadOfMaskingWithEphemeralKeys()
        {
            var config = BareConfig();
            var suppliedOwnerKey = EthECKey.GenerateKey().GetPrivateKey();
            config.Genesis.Owner.PrivateKey = suppliedOwnerKey;

            Assert.False(config.IsFullyUnconfigured);

            var signRecoverableBeforeCompose = EthECKey.SignRecoverable;
            try
            {
                var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => AppChainComposition.ComposeAsync(config, NullLoggerFactory.Instance, CancellationToken.None));

                Assert.Contains("Sequencer address is required for follower mode", ex.Message);
                Assert.Equal(suppliedOwnerKey, config.Genesis.Owner.PrivateKey);
            }
            finally
            {
                EthECKey.SignRecoverable = signRecoverableBeforeCompose;
            }
        }

        private static AppChainServerConfig BareConfig()
        {
            var chainId = new BigInteger(420420_800 + Environment.TickCount % 1000);
            return new AppChainServerConfig
            {
                ChainId = chainId,
                ChainName = "EphemeralDevKeyTest",
                Node = new ChainNodeConfig
                {
                    Rpc = new ChainNodeRpcConfig { Port = 8546 },
                    Storage = new ChainNodeStorageConfig { InMemory = true },
                    Network = new ChainNodeNetworkConfig { Serve = false },
                    Sync = new ChainNodeSyncConfig { Mode = SyncMode.None },
                    Mempool = new ChainNodeMempoolConfig { EnableTrustedPeerAdmission = true },
                },
                Mud = new AppChainMudConfig { DeployWorld = false },
            };
        }

        private sealed class RecordingLogger : ILogger
        {
            private readonly List<string> _messages;

            public RecordingLogger(List<string> messages) => _messages = messages;

            public IDisposable BeginScope<TState>(TState state) where TState : notnull => NoopScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel, EventId eventId, TState state, Exception exception,
                Func<TState, Exception, string> formatter)
                => _messages.Add(formatter(state, exception));

            private sealed class NoopScope : IDisposable
            {
                public static readonly NoopScope Instance = new NoopScope();
                public void Dispose() { }
            }
        }

        private sealed class RecordingLoggerFactory : ILoggerFactory
        {
            public List<string> Messages { get; } = new();

            public ILogger CreateLogger(string categoryName) => new RecordingLogger(Messages);

            public void AddProvider(ILoggerProvider provider) { }

            public void Dispose() { }
        }
    }
}
