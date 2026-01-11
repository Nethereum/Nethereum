using Nethereum.DevChain.IntegrationDemo.Contracts;
using Nethereum.DevChain.IntegrationDemo.Helpers;
using Nethereum.DevChain.Server.Configuration;
using Nethereum.Hex.HexTypes;

namespace Nethereum.DevChain.IntegrationDemo.Tests;

public static class ContractDeploymentTests
{
    public static async Task RunAsync()
    {
        Console.WriteLine("=== Contract Deployment Tests ===");
        Console.WriteLine();

        await TestERC20DeploymentAsync();

        Console.WriteLine("Contract Deployment Tests: PASSED");
        Console.WriteLine();
    }

    private static async Task TestERC20DeploymentAsync()
    {
        Console.Write("  Deploying ERC20 contract... ");

        await using var server = new DevChainTestServer();
        await server.StartAsync(new DevChainServerConfig
        {
            Port = 8548,
            ChainId = TestConfiguration.ChainId
        });

        var account = new Nethereum.Web3.Accounts.Account(TestConfiguration.DefaultPrivateKey, TestConfiguration.ChainId);
        var web3 = new Nethereum.Web3.Web3(account, server.Url);

        var receipt = await web3.Eth.DeployContract.SendRequestAndWaitForReceiptAsync(
            ERC20Contract.BYTECODE,
            account.Address,
            new HexBigInteger(3000000)
        );

        if (string.IsNullOrEmpty(receipt.ContractAddress))
            throw new Exception("Contract address not returned");

        var code = await web3.Eth.GetCode.SendRequestAsync(receipt.ContractAddress);
        if (code == null || code.Length < 10)
            throw new Exception("No code at contract address");

        Console.WriteLine($"OK (Address: {receipt.ContractAddress})");
        Console.WriteLine($"    Gas Used: {receipt.GasUsed.Value}");
        Console.WriteLine($"    Tx Hash: {receipt.TransactionHash}");
    }
}
