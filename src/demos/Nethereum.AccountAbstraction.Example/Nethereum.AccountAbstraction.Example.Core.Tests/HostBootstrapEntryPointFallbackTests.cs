using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.AccountAbstraction.Example.Hosting;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Eth.AccountAbstraction;
using Xunit;

namespace Nethereum.AccountAbstraction.Example.Core.Tests
{
    [Collection(ExampleCollection.COLLECTION_NAME)]
    public class HostBootstrapEntryPointFallbackTests
    {
        private readonly ExampleFixture _fixture;

        public HostBootstrapEntryPointFallbackTests(ExampleFixture fixture)
        {
            _fixture = fixture;
        }

        private sealed class FixedSupportedEntryPointsBundler : IAccountAbstractionBundlerService
        {
            private readonly IAccountAbstractionBundlerService _inner;

            public FixedSupportedEntryPointsBundler(IAccountAbstractionBundlerService inner, string[] entryPoints)
            {
                _inner = inner ?? throw new ArgumentNullException(nameof(inner));
                SupportedEntryPoints = new FixedEntryPoints(entryPoints);
            }

            public IEthChainId ChainId => _inner.ChainId;
            public IEthEstimateUserOperationGas EstimateUserOperationGas => _inner.EstimateUserOperationGas;
            public IEthGetUserOperationByHash GetUserOperationByHash => _inner.GetUserOperationByHash;
            public IEthGetUserOperationReceipt GetUserOperationReceipt => _inner.GetUserOperationReceipt;
            public IEthSendUserOperation SendUserOperation => _inner.SendUserOperation;
            public IEthSupportedEntryPoints SupportedEntryPoints { get; }

            private sealed class FixedEntryPoints : IEthSupportedEntryPoints
            {
                private readonly string[] _entryPoints;

                public FixedEntryPoints(string[] entryPoints) => _entryPoints = entryPoints;

                public RpcRequest BuildRequest(object id = null) =>
                    throw new NotSupportedException("Test double - no RPC request is ever built.");

                public Task<string[]> SendRequestAsync(object id = null) => Task.FromResult(_entryPoints);
            }
        }

        [Fact]
        [Trait("UseCase", "InfrastructureSetup")]
        public async Task Given_a_bundler_reporting_an_undeployed_EntryPoint_When_DeployStackAsync_runs_Then_it_deploys_a_fallback_and_logs_it()
        {
            const string undeployedEntryPoint = "0x000000000000000000000000000000DeaDbeef";
            var stubBundler = new FixedSupportedEntryPointsBundler(_fixture.Bundler, new[] { undeployedEntryPoint });
            var log = new List<string>();

            var stack = await HostBootstrap.DeployStackAsync(_fixture.Web3, stubBundler, line => log.Add(line));

            Assert.False(string.IsNullOrEmpty(stack.Addresses.EntryPointAddress));
            Assert.NotEqual(undeployedEntryPoint.ToLowerInvariant(), stack.Addresses.EntryPointAddress.ToLowerInvariant());
            Assert.True(stack.EntryPointWasFreshlyDeployed);
            Assert.Contains(log, line => line.Contains("has no code on this chain"));
            Assert.Contains(log, line => line.Contains("Fallback EntryPoint deployed at"));
        }
    }
}
