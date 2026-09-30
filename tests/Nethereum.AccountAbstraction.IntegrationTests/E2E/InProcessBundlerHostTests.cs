using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AccountAbstraction.Bundler.InProcess;
using Nethereum.AccountAbstraction.EntryPoint;
using Nethereum.AccountAbstraction.EntryPoint.ContractDefinition;
using Nethereum.Documentation;
using Nethereum.Signer;
using Xunit;
using Web3Account = Nethereum.Web3.Accounts.Account;

namespace Nethereum.AccountAbstraction.IntegrationTests.E2E
{
    public class InProcessBundlerHostTests
    {
        [Fact]
        [NethereumDocExample(DocSection.AccountAbstraction, "run-bundler", "Embedded bundler: bring up an in-process devchain + bundler for testing", Order = 5)]
        public async Task StartAsyncThenStartBundler_GivenDeployedEntryPoint_BringsUpLiveInProcessBundler()
        {
            var chainId = new BigInteger(31337);
            var operatorAccount = new Web3Account(EthECKey.GenerateKey(), chainId);
            var bundlerAccount = new Web3Account(EthECKey.GenerateKey(), chainId);

            await using var host = await InProcessBundlerHost.StartAsync(
                operatorAccount,
                chainId,
                new[] { operatorAccount.Address, bundlerAccount.Address },
                Nethereum.Web3.Web3.Convert.ToWei(10000));

            var entryPointService = await EntryPointService.DeployContractAndGetServiceAsync(
                host.OperatorWeb3, new EntryPointDeployment());

            var bundler = host.StartBundler(entryPointService.ContractAddress, bundlerAccount);

            Assert.Same(bundler, host.Bundler);
            Assert.NotNull(host.BundlerService);

            var supportedEntryPoints = await bundler.SupportedEntryPoints.SendRequestAsync();
            Assert.Single(supportedEntryPoints);
            Assert.Equal(
                entryPointService.ContractAddress.ToLowerInvariant(),
                supportedEntryPoints[0].ToLowerInvariant());

            var reportedChainId = await bundler.ChainId.SendRequestAsync();
            Assert.Equal(chainId, reportedChainId.Value);
        }
    }
}
