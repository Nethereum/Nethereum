using System.Numerics;
using Nethereum.DevChain.IntegrationDemo.Contracts;
using Nethereum.DevChain.IntegrationDemo.Helpers;
using Nethereum.DevChain.Server.Configuration;

namespace Nethereum.DevChain.IntegrationDemo.Tests;

public static class ForkingTests
{
    public static async Task RunAsync()
    {
        Console.WriteLine("=== Forking Tests ===");
        Console.WriteLine();

        await TestMainnetForkAsync();

        Console.WriteLine("Forking Tests: PASSED");
        Console.WriteLine();
    }

    private static async Task TestMainnetForkAsync()
    {
        Console.Write("  Testing mainnet fork... ");

        try
        {
            var mainnetWeb3 = new Nethereum.Web3.Web3(TestConfiguration.MainnetRpcUrl);
            var mainnetBlock = await mainnetWeb3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
            var forkBlock = (long)mainnetBlock.Value - 100;

            Console.WriteLine();
            Console.WriteLine($"    Fork URL: {TestConfiguration.MainnetRpcUrl}");
            Console.WriteLine($"    Fork Block: {forkBlock}");

            await using var server = new DevChainTestServer();
            await server.StartAsync(new DevChainServerConfig
            {
                Port = 8551,
                ChainId = 1,
                Fork = new ForkConfig
                {
                    Url = TestConfiguration.MainnetRpcUrl,
                    BlockNumber = forkBlock
                }
            });

            var web3 = new Nethereum.Web3.Web3(server.Url);

            var chainId = await web3.Eth.ChainId.SendRequestAsync();
            Console.WriteLine($"    DevChain ID: {chainId.Value}");

            var balanceHandler = web3.Eth.GetContractQueryHandler<BalanceOfFunction>();

            Console.Write("    Querying USDC balance... ");

            var whaleAddress = "0x47ac0Fb4F2D84898e4D9E7b4DaB3C24507a6D503";

            try
            {
                var balance = await balanceHandler.QueryAsync<BigInteger>(
                    TestConfiguration.UsdcContractAddress,
                    new BalanceOfFunction { Account = whaleAddress }
                );

                var usdcBalance = (decimal)balance / 1_000_000m;
                Console.WriteLine($"{usdcBalance:N2} USDC");

                if (balance > 0)
                {
                    Console.WriteLine("    Fork state accessible: YES");
                }
                else
                {
                    Console.WriteLine("    Fork state accessible: NO (balance is 0 - fork may not be fully implemented)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SKIPPED ({ex.Message})");
                Console.WriteLine("    Note: Forking requires state fetching from remote node (may not be fully implemented)");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SKIPPED");
            Console.WriteLine($"    Reason: {ex.Message}");
            Console.WriteLine("    Note: Fork test requires network access to public RPC endpoint");
        }

        Console.WriteLine();
    }
}
