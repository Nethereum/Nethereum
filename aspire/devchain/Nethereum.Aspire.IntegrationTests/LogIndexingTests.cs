using Nethereum.Aspire.IntegrationTests.Infrastructure;
using System.Numerics;
using Xunit;

namespace Nethereum.Aspire.IntegrationTests;

[Collection("Aspire")]
public class LogIndexingTests
{
    private readonly AspireFixture _fixture;

    public LogIndexingTests(AspireFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ERC20Transfer_LogsIndexedInPostgres()
    {
        var contractAddress = await ERC20TestHelper.DeployAsync(_fixture.Web3);

        var mintAmount = BigInteger.Parse("1000000000000000000000");
        await ERC20TestHelper.MintAsync(_fixture.Web3, contractAddress, AspireFixture.Address, mintAmount);

        var toAddress = "0x70997970C51812dc3A010C7d01b50e0d17dc79C8";
        var txHash = await ERC20TestHelper.TransferAsync(
            _fixture.Web3, contractAddress, toAddress, BigInteger.One);

        var receipt = await _fixture.Web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txHash);
        Assert.NotNull(receipt);

        await using var connection = await _fixture.CreateDbConnectionAsync();
        await TestContractDeployer.WaitForIndexerCaughtUpAsync(
            connection, (long)receipt.BlockNumber.Value);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"SELECT COUNT(*) FROM ""TransactionLogs""
            WHERE transactionhash = @hash
            AND eventhash = @eventHash";
        cmd.Parameters.AddWithValue("hash", txHash);
        cmd.Parameters.AddWithValue("eventHash", ERC20TestHelper.TransferEventSignature);
        var count = (long)(await cmd.ExecuteScalarAsync())!;

        Assert.True(count > 0,
            $"Transfer event log for tx {txHash} not found in Postgres");
    }

    [Fact]
    public async Task ERC20Mint_EmitsTransferFromZeroAddress()
    {
        var contractAddress = await ERC20TestHelper.DeployAsync(_fixture.Web3);

        var mintAmount = BigInteger.Parse("500000000000000000000");
        await ERC20TestHelper.MintAsync(_fixture.Web3, contractAddress, AspireFixture.Address, mintAmount);

        var blockNumber = await _fixture.Web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();

        await using var connection = await _fixture.CreateDbConnectionAsync();
        await TestContractDeployer.WaitForIndexerCaughtUpAsync(
            connection, (long)blockNumber.Value);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"SELECT COUNT(*) FROM ""TransactionLogs""
            WHERE address = @address
            AND eventhash = @eventHash";
        cmd.Parameters.AddWithValue("address", contractAddress.ToLower());
        cmd.Parameters.AddWithValue("eventHash", ERC20TestHelper.TransferEventSignature);
        var count = (long)(await cmd.ExecuteScalarAsync())!;

        Assert.True(count > 0,
            $"Mint Transfer event from contract {contractAddress} not found in Postgres");
    }
}
