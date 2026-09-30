using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AccountAbstraction.EntryPoint;
using Nethereum.AccountAbstraction.EntryPoint.ContractDefinition;
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccountFactory;
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccountFactory.ContractDefinition;
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter;
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter.ContractDefinition;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Extensions;
using Nethereum.Signer;
using Nethereum.XUnitEthereumClients;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.E2E.MainnetFork
{
    [CollectionDefinition(StrictBundlerFixture.COLLECTION_NAME)]
    public class StrictBundlerCollection : ICollectionFixture<StrictBundlerFixture> { }

    [Collection(StrictBundlerFixture.COLLECTION_NAME)]
    [Trait("Category", "MainnetFork")]
    [Trait("ERC", "4337")]
    public class StrictBundlerInteropTests
    {
        private readonly StrictBundlerFixture _fixture;

        public StrictBundlerInteropTests(StrictBundlerFixture fixture)
        {
            _fixture = fixture;
        }

        private async Task<string> DeployV08FactoryAsync(Web3.Web3 web3)
        {
            var artifactPath = System.IO.Path.Combine(_fixture.BundlerRepoPath,
                "node_modules", "@account-abstraction", "contracts", "artifacts", "SimpleAccountFactory.json");
            var artifact = Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText(artifactPath));
            var deployData = artifact["bytecode"].ToString()
                + StrictBundlerFixture.EntryPointAddress.Substring(2).PadLeft(64, '0');

            var receipt = await web3.Eth.DeployContract.SendRequestAndWaitForReceiptAsync(
                deployData, web3.TransactionManager.Account.Address, new HexBigInteger(4_000_000));
            return receipt.ContractAddress;
        }

        private async Task<string> DeployAcceptAllPaymasterAsync(Web3.Web3 web3)
        {
            var deployData = MinimalAcceptAllPaymaster.Bytecode
                + StrictBundlerFixture.EntryPointAddress.Substring(2).PadLeft(64, '0');

            var receipt = await web3.Eth.DeployContract.SendRequestAndWaitForReceiptAsync(
                deployData, web3.TransactionManager.Account.Address, new HexBigInteger(4_000_000));
            return receipt.ContractAddress;
        }

        [SkippableFact]
        public async Task FullSend_ThroughStrictBundler_AgainstCanonicalEntryPoint()
        {
            Skip.If(!_fixture.IsAvailable, _fixture.UnavailableReason);

            var web3 = _fixture.Fork.GetWeb3();

            var factoryAddress = await DeployV08FactoryAsync(web3);
            var factory = new SimpleAccountFactoryService(web3, factoryAddress);
            var counter = await TestCounterService.DeployContractAndGetServiceAsync(
                web3, new TestCounterDeployment());

            var accountKey = EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();
            ulong salt = 1;
            var accountAddress = await factory.GetAddressQueryAsync(ownerAddress, salt);

            await web3.Eth.Anvil().SetBalance.SendRequestAsync(
                accountAddress, new HexBigInteger(Web3.Web3.Convert.ToWei(1)));

            var counterService = new TestCounterService(web3, counter.ContractAddress);
            counterService.ChangeContractHandlerToAA(
                accountAddress,
                accountKey,
                new Nethereum.RPC.AccountAbstractionBundlerService(new Nethereum.JsonRpc.Client.RpcClient(new System.Uri(_fixture.BundlerUrl))),
                StrictBundlerFixture.EntryPointAddress,
                factory: new FactoryConfig(factoryAddress, ownerAddress, salt));

            var receipt = await counterService.CountRequestAndWaitForReceiptAsync(
                new CountFunction { Gas = 150_000 });

            var aaReceipt = Assert.IsType<AATransactionReceipt>(receipt);
            Assert.True(aaReceipt.UserOpSuccess,
                $"UserOperation should succeed through the strict bundler. Revert: {aaReceipt.RevertReason}");

            var count = await counter.CountersQueryAsync(accountAddress);
            Assert.Equal(BigInteger.One, count);

            var handler = (AAContractHandler)counterService.ContractHandler;
            var bundlerService = new Nethereum.RPC.AccountAbstractionBundlerService(
                new Nethereum.JsonRpc.Client.RpcClient(new System.Uri(_fixture.BundlerUrl)));
            var byHash = await bundlerService.GetUserOperationByHash.SendRequestAsync(aaReceipt.UserOpHash);
            Assert.NotNull(byHash);
            Assert.NotNull(byHash.UserOperation);
            Assert.True(accountAddress.Equals(byHash.UserOperation.Sender, System.StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(byHash.TransactionHash);
        }

        [SkippableFact]
        public async Task PaymasterSponsoredOp_ThroughStrictBundler_EstimatesAndSimulates()
        {
            Skip.If(!_fixture.IsAvailable, _fixture.UnavailableReason);

            var web3 = _fixture.Fork.GetWeb3();

            var factoryAddress = await DeployV08FactoryAsync(web3);
            var factory = new SimpleAccountFactoryService(web3, factoryAddress);
            var counter = await TestCounterService.DeployContractAndGetServiceAsync(
                web3, new TestCounterDeployment());

            var paymasterAddress = await DeployAcceptAllPaymasterAsync(web3);
            var entryPoint = new EntryPointService(web3, StrictBundlerFixture.EntryPointAddress);

            await entryPoint.DepositToRequestAndWaitForReceiptAsync(new DepositToFunction
            {
                Account = paymasterAddress,
                AmountToSend = Web3.Web3.Convert.ToWei(1)
            });

            var paymasterHandler = web3.Eth.GetContractHandler(paymasterAddress);
            await paymasterHandler.SendRequestAndWaitForReceiptAsync(new AddStakeFunction
            {
                UnstakeDelaySec = 1,
                AmountToSend = Web3.Web3.Convert.ToWei((decimal)0.1)
            });

            var depositInfo = await entryPoint.GetDepositInfoQueryAsync(paymasterAddress);
            Assert.True(depositInfo.Info.Staked, "Paymaster should be staked on the EntryPoint.");
            Assert.True(depositInfo.Info.Deposit >= Web3.Web3.Convert.ToWei(1),
                $"Paymaster deposit should cover gas, got {depositInfo.Info.Deposit}.");

            var accountKey = EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();
            ulong salt = 3;
            var accountAddress = await factory.GetAddressQueryAsync(ownerAddress, salt);

            var accountBalance = await web3.Eth.GetBalance.SendRequestAsync(accountAddress);
            Assert.Equal(BigInteger.Zero, accountBalance.Value);

            var counterService = new TestCounterService(web3, counter.ContractAddress);
            var handler = counterService.ChangeContractHandlerToAA(
                accountAddress,
                accountKey,
                new Nethereum.RPC.AccountAbstractionBundlerService(new Nethereum.JsonRpc.Client.RpcClient(new System.Uri(_fixture.BundlerUrl))),
                StrictBundlerFixture.EntryPointAddress,
                factory: new FactoryConfig(factoryAddress, ownerAddress, salt));

            handler.WithPaymaster(paymasterAddress);

            var totalGas = await handler.EstimateGasAsync(new CountFunction());

            Assert.True(totalGas.Value > 100_000,
                $"The foreign bundler should return a usable estimate for the paymaster-sponsored, " +
                $"zero-balance-sender op, proving the paymaster sponsors it; got {totalGas.Value}.");

        }

        [SkippableFact]
        public async Task Estimate_ThroughStrictBundler_ReturnsUsableTotal()
        {
            Skip.If(!_fixture.IsAvailable, _fixture.UnavailableReason);

            var web3 = _fixture.Fork.GetWeb3();

            var factoryAddress = await DeployV08FactoryAsync(web3);
            var factory = new SimpleAccountFactoryService(web3, factoryAddress);
            var counter = await TestCounterService.DeployContractAndGetServiceAsync(
                web3, new TestCounterDeployment());

            var accountKey = EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();
            ulong salt = 2;
            var accountAddress = await factory.GetAddressQueryAsync(ownerAddress, salt);
            await web3.Eth.Anvil().SetBalance.SendRequestAsync(
                accountAddress, new HexBigInteger(Web3.Web3.Convert.ToWei(1)));

            var counterService = new TestCounterService(web3, counter.ContractAddress);
            var handler = counterService.ChangeContractHandlerToAA(
                accountAddress,
                accountKey,
                new Nethereum.RPC.AccountAbstractionBundlerService(new Nethereum.JsonRpc.Client.RpcClient(new System.Uri(_fixture.BundlerUrl))),
                StrictBundlerFixture.EntryPointAddress,
                factory: new FactoryConfig(factoryAddress, ownerAddress, salt));

            var totalGas = await handler.EstimateGasAsync(new CountFunction());

            Assert.True(totalGas.Value > 100_000,
                $"Strict bundler estimate should cover deployment + execution, got {totalGas.Value}");
        }
    }
}
