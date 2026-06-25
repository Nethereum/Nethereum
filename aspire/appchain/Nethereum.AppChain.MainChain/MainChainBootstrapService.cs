using System.Security.Cryptography;
using System.Text;
using Nethereum.AppChain.Anchoring.AppChainAnchor;
using Nethereum.AppChain.Anchoring.AppChainAnchor.ContractDefinition;
using Nethereum.AppChain.Anchoring.SimpleAuthority;
using Nethereum.AppChain.Anchoring.SimpleAuthority.ContractDefinition;
using Nethereum.Web3.Accounts;

namespace Nethereum.AppChain.MainChain;

public class MainChainBootstrapService : BackgroundService
{
    private readonly ContractAddresses _addresses;
    private readonly IConfiguration _config;
    private readonly ILogger<MainChainBootstrapService> _logger;

    public MainChainBootstrapService(
        ContractAddresses addresses,
        IConfiguration config,
        ILogger<MainChainBootstrapService> logger)
    {
        _addresses = addresses;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var operatorKey = _config["MainChain:OperatorKey"]
            ?? "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        var chainId = int.TryParse(_config["MainChain:ChainId"], out var cid) ? cid : 1337;
        var appChainId = ulong.TryParse(_config["MainChain:AppChainId"], out var acid) ? acid : 420420UL;

        var rpcUrl = _config["ASPNETCORE_URLS"]?.Split(';').FirstOrDefault(u => u.StartsWith("http://"))
            ?? $"http://localhost:{_config["MainChain:Port"] ?? "53500"}";

        for (int attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                await Task.Delay(3000, ct);

                var account = new Account(operatorKey, chainId);
                var web3 = new Nethereum.Web3.Web3(account, rpcUrl);
                web3.TransactionManager.UseLegacyAsDefault = true;

                var blockNumber = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
                _logger.LogInformation("Connected to mainchain RPC at {Url}, block={Block}", rpcUrl, blockNumber.Value);

                _logger.LogInformation("Deploying SimpleAuthority...");
                var authorityService = await SimpleAuthorityService.DeployContractAndGetServiceAsync(
                    web3, new SimpleAuthorityDeployment { Owner = account.Address });
                _addresses.Authority = authorityService.ContractAddress;

                _logger.LogInformation("Deploying AppChainAnchor...");
                var anchorReceipt = await AppChainAnchorService.DeployContractAndWaitForReceiptAsync(
                    web3, new AppChainAnchorDeployment());
                _addresses.Anchor = anchorReceipt.ContractAddress;

                var anchorService = new AppChainAnchorService(web3, _addresses.Anchor);

                var schemaHash = new Nethereum.Util.Sha3Keccack()
                    .CalculateHash(Encoding.UTF8.GetBytes("keccak256"));
                await anchorService.RegisterSchemaRequestAndWaitForReceiptAsync(
                    new RegisterSchemaFunction { Version = 1, HashFunction = schemaHash, TrieType = 0, StateModel = 0 });
                await anchorService.RegisterProofSystemRequestAndWaitForReceiptAsync(
                    new RegisterProofSystemFunction
                    { ProofSystem = 0, Verifier = "0x0000000000000000000000000000000000000000", RequiresProof = false });
                await anchorService.RegisterProofSystemRequestAndWaitForReceiptAsync(
                    new RegisterProofSystemFunction
                    { ProofSystem = 1, Verifier = "0x0000000000000000000000000000000000000000", RequiresProof = false });

                var genesisHash = SHA256.HashData(
                    Encoding.UTF8.GetBytes($"appchain-{appChainId}"));
                await anchorService.RegisterAppChainRequestAndWaitForReceiptAsync(
                    new RegisterAppChainFunction
                    {
                        ChainId = appChainId, GenesisHash = genesisHash, GenesisBlock = 1,
                        GenesisStateRoot = new byte[32],
                        MinimumProofSystem = 0, MinimumAnchorVersion = 1,
                        Authority = authorityService.ContractAddress
                    });

                await authorityService.SetOperatorRequestAndWaitForReceiptAsync(
                    new SetOperatorFunction { ChainId = appChainId, NewOperator = account.Address });

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
            }
        }

        _logger.LogError("MainChain bootstrap failed after 10 attempts");
    }
}
