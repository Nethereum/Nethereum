using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.AppChain.Genesis;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Mud.Contracts;
using Nethereum.Web3.Accounts;

namespace Nethereum.AppChain.Sequencer
{
    public class MudWorldDeployer
    {
        private readonly WorldFactoryDeployService _worldFactoryService = new WorldFactoryDeployService();
        private readonly ILogger? _logger;

        public MudWorldDeployer(ILogger? logger = null)
        {
            _logger = logger;
        }

        public async Task<MudGenesisResult> DeployMudWorldAsync(
            AppChainNode node,
            string deployerPrivateKey,
            byte[]? salt = null)
        {
            var chainId = (long)node.Config.ChainId;
            salt ??= new byte[32];
            var saltHex = salt.ToHex(true);

            var deployerKey = new Nethereum.Signer.EthECKey(deployerPrivateKey);
            var deployerAddress = deployerKey.GetPublicAddress();

            _logger?.LogInformation("Deploying MUD World with deployer: {Address}", deployerAddress);

            var account = new Account(deployerPrivateKey, chainId);
            var rpcClient = new AppChainRpcClient(node, chainId, _logger);
            var web3 = new Web3.Web3(account, rpcClient);
            web3.TransactionManager.UseLegacyAsDefault = true;

            var create2Address = Create2FactoryGenesisBuilder.CREATE2_FACTORY_ADDRESS;

            var create2Service = web3.Eth.Create2DeterministicDeploymentProxyService;
            var hasProxy = await create2Service.HasProxyBeenDeployedAsync(create2Address);
            if (!hasProxy)
            {
                throw new InvalidOperationException(
                    $"CREATE2 factory not found at {create2Address}. Ensure genesis was initialized with DeployCreate2Factory=true");
            }

            _logger?.LogInformation("CREATE2 factory verified at: {Address}", create2Address);
            _logger?.LogInformation("Deploying MUD system contracts...");

            var factoryAddresses = await _worldFactoryService.DeployWorldFactoryContractAndSystemDependenciesAsync(
                web3, create2Address, saltHex);

            _logger?.LogInformation("WorldFactory deployed at: {Address}", factoryAddresses.WorldFactoryAddress);
            _logger?.LogInformation("Deploying World instance...");

            var worldEvent = await _worldFactoryService.DeployWorldAsync(web3, saltHex, factoryAddresses);

            _logger?.LogInformation("World deployed at: {Address}", worldEvent.NewContract);

            return MudGenesisResult.FromWorldFactoryAddresses(
                factoryAddresses,
                worldEvent.NewContract,
                create2Address,
                deployerAddress);
        }
    }
}
