using Nethereum.Aspire.LoadGenerator.Scenarios.Mud;
using Nethereum.Aspire.LoadGenerator.Services;
using Nethereum.Mud.Contracts;

namespace Nethereum.Aspire.LoadGenerator.Scenarios;

public class MudWorldScenario : ILoadScenario
{
    private AccountManager _accountManager = null!;
    private string _worldAddress = "";
    private static string? _deployedWorldAddress;
    private static readonly object _lock = new();

    public string Name => "mud-world";
    public static string? DeployedWorldAddress => _deployedWorldAddress;

    public async Task InitializeAsync(AccountManager accountManager, string rpcUrl, ILogger logger)
    {
        _accountManager = accountManager;
        var (_, web3) = accountManager.GetAccountForWorker(0);

        logger.LogInformation("MudWorldScenario: deploying MUD World...");

        var random = new Random();
        var salt = Nethereum.Util.Sha3Keccack.Current.CalculateHash(random.Next(0, 1000000).ToString());

        var create2Service = web3.Eth.Create2DeterministicDeploymentProxyService;
        var proxyDeployment = await create2Service.GenerateEIP155DeterministicDeploymentUsingPreconfiguredSignatureAsync();
        var deployerAddress = await create2Service.DeployProxyAndGetContractAddressAsync(proxyDeployment);

        logger.LogInformation("MudWorldScenario: Create2 proxy at {Address}", deployerAddress);

        var worldFactoryService = new WorldFactoryDeployService();
        var worldFactoryAddresses = await worldFactoryService.DeployWorldFactoryContractAndSystemDependenciesAsync(web3, deployerAddress, salt);

        logger.LogInformation("MudWorldScenario: WorldFactory at {Address}", worldFactoryAddresses.WorldFactoryAddress);

        var worldEvent = await worldFactoryService.DeployWorldAsync(web3, salt, worldFactoryAddresses);
        _worldAddress = worldEvent.NewContract;

        logger.LogInformation("MudWorldScenario: World at {Address}", _worldAddress);

        var mudNamespace = new MudLoadGenNamespace(web3, _worldAddress);
        await mudNamespace.RegisterNamespaceRequestAndWaitForReceiptAsync();

        logger.LogInformation("MudWorldScenario: namespace registered");

        await mudNamespace.Tables.BatchRegisterAllTablesRequestAndWaitForReceiptAsync();

        logger.LogInformation("MudWorldScenario: Counter and Item tables registered");

        await mudNamespace.Systems.DeployAllCreate2ContractSystemsRequestAndWaitForReceiptAsync(deployerAddress, salt);
        await mudNamespace.Systems.BatchRegisterAllSystemsRequestAndWaitForReceiptAsync(deployerAddress, salt);

        logger.LogInformation("MudWorldScenario: IncrementSystem deployed and registered");

        lock (_lock)
        {
            _deployedWorldAddress = _worldAddress;
        }

        logger.LogInformation("MudWorldScenario: initialization complete — World at {Address}", _worldAddress);
    }

    public async Task ExecuteAsync(int workerIndex)
    {
        var (_, web3) = _accountManager.GetAccountForWorker(workerIndex);
        var incrementService = new MudLoadGenIncrementSystemService(web3, _worldAddress);
        await incrementService.IncrementRequestAndWaitForReceiptAsync();
    }
}
