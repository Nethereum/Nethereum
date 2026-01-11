using Nethereum.DevChain.IntegrationDemo.Helpers;
using Nethereum.DevChain.Server.Configuration;
using Nethereum.Util;

namespace Nethereum.DevChain.IntegrationDemo.Tests;

public static class ServerConnectionTests
{
    public static async Task RunAsync()
    {
        Console.WriteLine("=== Server Connection Tests ===");
        Console.WriteLine();

        await TestServerStartupAsync();
        await TestWeb3ConnectionAsync();
        await TestAccountBalanceAsync();

        Console.WriteLine("Server Connection Tests: PASSED");
        Console.WriteLine();
    }

    private static async Task TestServerStartupAsync()
    {
        Console.Write("  Starting DevChain server... ");

        await using var server = new DevChainTestServer();
        await server.StartAsync(new DevChainServerConfig
        {
            Port = TestConfiguration.DefaultPort,
            ChainId = TestConfiguration.ChainId
        });

        Console.WriteLine($"OK (listening on {server.Url})");
    }

    private static async Task TestWeb3ConnectionAsync()
    {
        Console.Write("  Connecting Web3 client... ");

        await using var server = new DevChainTestServer();
        await server.StartAsync(new DevChainServerConfig
        {
            Port = 8546,
            ChainId = TestConfiguration.ChainId
        });

        var web3 = new Nethereum.Web3.Web3(server.Url);

        var chainId = await web3.Eth.ChainId.SendRequestAsync();
        if (chainId.Value != TestConfiguration.ChainId)
            throw new Exception($"Chain ID mismatch: expected {TestConfiguration.ChainId}, got {chainId.Value}");

        var blockNumber = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
        Console.WriteLine($"OK (Chain ID: {chainId.Value}, Block: {blockNumber.Value})");
    }

    private static async Task TestAccountBalanceAsync()
    {
        Console.Write("  Checking account balance... ");

        await using var server = new DevChainTestServer();
        await server.StartAsync(new DevChainServerConfig
        {
            Port = 8547,
            ChainId = TestConfiguration.ChainId
        });

        var web3 = new Nethereum.Web3.Web3(server.Url);

        var balance = await web3.Eth.GetBalance.SendRequestAsync(TestConfiguration.DefaultAddress);
        var balanceEth = UnitConversion.Convert.FromWei(balance);

        if (balanceEth < 1000)
            throw new Exception($"Balance too low: {balanceEth} ETH");

        Console.WriteLine($"OK ({balanceEth} ETH)");
    }
}
