using System.Numerics;
using Nethereum.DevChain.IntegrationDemo.Contracts;
using Nethereum.DevChain.IntegrationDemo.Helpers;
using Nethereum.DevChain.Server.Configuration;
using Nethereum.Hex.HexTypes;
using Nethereum.Util;

namespace Nethereum.DevChain.IntegrationDemo.Tests;

public static class ContractInteractionTests
{
    public static async Task RunAsync()
    {
        Console.WriteLine("=== Contract Interaction Tests ===");
        Console.WriteLine();

        await TestMintAndBalanceAsync();
        await TestTransferAsync();

        Console.WriteLine("Contract Interaction Tests: PASSED");
        Console.WriteLine();
    }

    private static async Task TestMintAndBalanceAsync()
    {
        Console.Write("  Testing mint and balanceOf... ");

        await using var server = new DevChainTestServer();
        await server.StartAsync(new DevChainServerConfig
        {
            Port = 8549,
            ChainId = TestConfiguration.ChainId
        });

        var account = new Nethereum.Web3.Accounts.Account(TestConfiguration.DefaultPrivateKey, TestConfiguration.ChainId);
        var web3 = new Nethereum.Web3.Web3(account, server.Url);

        var receipt = await web3.Eth.DeployContract.SendRequestAndWaitForReceiptAsync(
            ERC20Contract.BYTECODE,
            account.Address,
            new HexBigInteger(3000000)
        );

        var contractAddress = receipt.ContractAddress!;

        var mintHandler = web3.Eth.GetContractTransactionHandler<MintFunction>();
        var mintAmount = UnitConversion.Convert.ToWei(1000);

        var mintReceipt = await mintHandler.SendRequestAndWaitForReceiptAsync(
            contractAddress,
            new MintFunction { To = account.Address, Amount = mintAmount }
        );

        var balanceHandler = web3.Eth.GetContractQueryHandler<BalanceOfFunction>();
        var balance = await balanceHandler.QueryAsync<BigInteger>(
            contractAddress,
            new BalanceOfFunction { Account = account.Address }
        );

        if (balance != mintAmount)
            throw new Exception($"Balance mismatch: expected {mintAmount}, got {balance}");

        Console.WriteLine($"OK (Balance: {UnitConversion.Convert.FromWei(balance)} tokens)");
    }

    private static async Task TestTransferAsync()
    {
        Console.Write("  Testing transfer... ");

        await using var server = new DevChainTestServer();
        await server.StartAsync(new DevChainServerConfig
        {
            Port = 8550,
            ChainId = TestConfiguration.ChainId
        });

        var account = new Nethereum.Web3.Accounts.Account(TestConfiguration.DefaultPrivateKey, TestConfiguration.ChainId);
        var web3 = new Nethereum.Web3.Web3(account, server.Url);

        var receipt = await web3.Eth.DeployContract.SendRequestAndWaitForReceiptAsync(
            ERC20Contract.BYTECODE,
            account.Address,
            new HexBigInteger(3000000)
        );

        var contractAddress = receipt.ContractAddress!;

        var mintHandler = web3.Eth.GetContractTransactionHandler<MintFunction>();
        var mintAmount = UnitConversion.Convert.ToWei(1000);
        await mintHandler.SendRequestAndWaitForReceiptAsync(
            contractAddress,
            new MintFunction { To = account.Address, Amount = mintAmount }
        );

        var transferHandler = web3.Eth.GetContractTransactionHandler<TransferFunction>();
        var transferAmount = UnitConversion.Convert.ToWei(100);
        var transferReceipt = await transferHandler.SendRequestAndWaitForReceiptAsync(
            contractAddress,
            new TransferFunction { To = TestConfiguration.SecondaryAddress, Value = transferAmount }
        );

        var balanceHandler = web3.Eth.GetContractQueryHandler<BalanceOfFunction>();

        var senderBalance = await balanceHandler.QueryAsync<BigInteger>(
            contractAddress,
            new BalanceOfFunction { Account = account.Address }
        );

        var recipientBalance = await balanceHandler.QueryAsync<BigInteger>(
            contractAddress,
            new BalanceOfFunction { Account = TestConfiguration.SecondaryAddress }
        );

        if (senderBalance != mintAmount - transferAmount)
            throw new Exception($"Sender balance wrong: expected {mintAmount - transferAmount}, got {senderBalance}");

        if (recipientBalance != transferAmount)
            throw new Exception($"Recipient balance wrong: expected {transferAmount}, got {recipientBalance}");

        Console.WriteLine($"OK (Transferred {UnitConversion.Convert.FromWei(transferAmount)} tokens)");
        Console.WriteLine($"    Sender Balance: {UnitConversion.Convert.FromWei(senderBalance)} tokens");
        Console.WriteLine($"    Recipient Balance: {UnitConversion.Convert.FromWei(recipientBalance)} tokens");
    }
}
