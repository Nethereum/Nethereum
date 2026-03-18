using System.Numerics;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using NethereumDapp.ContractServices.MyToken;
using NethereumDapp.ContractServices.MyToken.ContractDefinition;

namespace NethereumDapp.LoadGenerator;

public class LoadGeneratorService : BackgroundService
{
    private readonly ILogger<LoadGeneratorService> _logger;
    private readonly IConfiguration _configuration;

    private const string DefaultPrivateKey = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
    private const int DefaultChainId = CHAIN_ID_VALUE;

    public LoadGeneratorService(ILogger<LoadGeneratorService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(5000, stoppingToken);

        var rpcUrl = _configuration["services:devchain:http:0"]
            ?? _configuration.GetConnectionString("devchain")
            ?? "http://localhost:8545";

        var privateKey = _configuration["LoadGenerator:PrivateKey"] ?? DefaultPrivateKey;
        var chainId = _configuration.GetValue("LoadGenerator:ChainId", DefaultChainId);
        var delayMs = _configuration.GetValue("LoadGenerator:DelayMs", 2000);

        var account = new Account(privateKey, chainId);
        var web3 = new Web3(account, rpcUrl);

        _logger.LogInformation("Deploying MyToken contract for load generation...");

        MyTokenService tokenService;
        try
        {
            var deployment = new MyTokenDeployment
            {
                Name = "LoadToken",
                Symbol = "LOAD",
                InitialSupply = Web3.Convert.ToWei(1_000_000_000)
            };
            tokenService = await MyTokenService.DeployContractAndGetServiceAsync(web3, deployment);
            _logger.LogInformation("MyToken deployed at {Address}", tokenService.ContractHandler.ContractAddress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deploy contract. Stopping load generator.");
            return;
        }

        var recipientAddresses = Enumerable.Range(1, 10)
            .Select(i => $"0x{i:X40}")
            .ToArray();

        var txCount = 0L;
        var random = new Random();

        _logger.LogInformation("Starting load generation loop (delay: {DelayMs}ms)...", delayMs);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var recipient = recipientAddresses[random.Next(recipientAddresses.Length)];
                var action = random.Next(3);

                switch (action)
                {
                    case 0:
                        var mintAmount = Web3.Convert.ToWei(random.Next(1, 100));
                        await tokenService.MintRequestAndWaitForReceiptAsync(recipient, mintAmount);
                        Interlocked.Increment(ref txCount);
                        _logger.LogDebug("Mint #{Count} to {Recipient}", txCount, recipient);
                        break;

                    case 1:
                        var transferAmount = Web3.Convert.ToWei(random.Next(1, 10));
                        await tokenService.TransferRequestAndWaitForReceiptAsync(recipient, transferAmount);
                        Interlocked.Increment(ref txCount);
                        _logger.LogDebug("Transfer #{Count} to {Recipient}", txCount, recipient);
                        break;

                    case 2:
                        await web3.Eth.GetEtherTransferService()
                            .TransferEtherAndWaitForReceiptAsync(recipient, 0.001m);
                        Interlocked.Increment(ref txCount);
                        _logger.LogDebug("ETH transfer #{Count} to {Recipient}", txCount, recipient);
                        break;
                }

                if (txCount % 50 == 0)
                {
                    _logger.LogInformation("Load generator: {Count} transactions sent", txCount);
                }

                await Task.Delay(delayMs, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Transaction failed, retrying...");
                await Task.Delay(1000, stoppingToken);
            }
        }

        _logger.LogInformation("Load generator stopped. Total transactions: {Count}", txCount);
    }
}
