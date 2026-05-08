using System.Security.Cryptography;
using System.Text;
using Nethereum.AppChain.Anchoring.AppChainAnchor;
using Nethereum.AppChain.Anchoring.AppChainAnchor.ContractDefinition;
using Nethereum.CoreChain.Rpc;
using Nethereum.DevChain;
using Nethereum.Web3.Accounts;

namespace Nethereum.AppChain.MainChain;

public class MainChainBootstrapService : BackgroundService
{
    private readonly ContractAddresses _addresses;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _config;
    private readonly ILogger<MainChainBootstrapService> _logger;

    public MainChainBootstrapService(
        ContractAddresses addresses,
        IServiceProvider serviceProvider,
        IConfiguration config,
        ILogger<MainChainBootstrapService> logger)
    {
        _addresses = addresses;
        _serviceProvider = serviceProvider;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var operatorKey = _config["MainChain:OperatorKey"]
            ?? "0x8da4ef21b864d2cc526dbdb2a120bd2874c36c9d0a1fb7f8c63d7f7a8b41de8f";
        var chainId = int.TryParse(_config["MainChain:ChainId"], out var cid) ? cid : 1337;
        var appChainId = ulong.TryParse(_config["MainChain:AppChainId"], out var acid) ? acid : 420420UL;

        for (int attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                await Task.Delay(3000, ct);

                var dispatcher = _serviceProvider.GetRequiredService<RpcDispatcher>();
                var rpcClient = new DevChainRpcClient(dispatcher);
                var account = new Account(operatorKey, chainId);
                var web3 = new Nethereum.Web3.Web3(account, rpcClient);
                web3.TransactionManager.UseLegacyAsDefault = true;

                _logger.LogInformation("Deploying AppChainAnchor contract...");
                var anchorReceipt = await AppChainAnchorService.DeployContractAndWaitForReceiptAsync(
                    web3, new AppChainAnchorDeployment());
                _addresses.Anchor = anchorReceipt.ContractAddress;

                var anchorService = new AppChainAnchorService(web3, _addresses.Anchor);

                await anchorService.RegisterSchemaRequestAndWaitForReceiptAsync(
                    1, new byte[32], 0, 0);
                await anchorService.RegisterProofSystemRequestAndWaitForReceiptAsync(
                    0, "0x0000000000000000000000000000000000000000", false);
                await anchorService.RegisterProofSystemRequestAndWaitForReceiptAsync(
                    1, "0x0000000000000000000000000000000000000000", false);

                var genesisHash = SHA256.HashData(
                    Encoding.UTF8.GetBytes($"appchain-{appChainId}"));
                await anchorService.RegisterAppChainRequestAndWaitForReceiptAsync(
                    appChainId, genesisHash, 1, new byte[32], 0, 1,
                    account.Address);

                _addresses.AppChainId = appChainId;

                _logger.LogInformation(
                    "MainChain bootstrap complete: anchor={Anchor}, appChainId={AppChainId}",
                    _addresses.Anchor, appChainId);
                return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Bootstrap attempt {Attempt} failed, retrying...", attempt + 1);
                await Task.Delay(3000, ct);
            }
        }

        _logger.LogError("MainChain bootstrap failed after 5 attempts");
    }
}
