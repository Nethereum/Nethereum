using Nethereum.Aspire.IntegrationTests.Infrastructure;
using Xunit;

namespace Nethereum.Aspire.IntegrationTests;

[Collection("Aspire")]
public class BlockIndexingTests
{
    private readonly AspireFixture _fixture;

    public BlockIndexingTests(AspireFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task DevChain_IsRunning_ReturnsChainId()
    {
        var chainId = await _fixture.Web3.Eth.ChainId.SendRequestAsync();
        Assert.Equal(AspireFixture.ChainId, (int)chainId.Value);
    }

    [Fact]
    public async Task SendTransaction_IndexedInPostgres()
    {
        var receipt = await TestContractDeployer.SendEthTransferAsync(
            _fixture.Web3,
            "0x70997970C51812dc3A010C7d01b50e0d17dc79C8",
            1.0m);

        Assert.NotNull(receipt);
        Assert.Equal(1, (int)receipt.Status.Value);

        var chainHead = await _fixture.Web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();

        await using var connection = await _fixture.CreateDbConnectionAsync();
        await TestContractDeployer.WaitForIndexerCaughtUpAsync(
            connection,
            (long)chainHead.Value);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"SELECT COUNT(*) FROM ""Transactions"" WHERE LOWER(hash) = LOWER(@hash)";
        cmd.Parameters.AddWithValue("hash", receipt.TransactionHash);
        var count = (long)(await cmd.ExecuteScalarAsync())!;

        Assert.True(count > 0, $"Transaction {receipt.TransactionHash} not found in Postgres");
    }

    [Fact]
    public async Task BlocksAreIndexed_AfterTransactions()
    {
        var receipt = await TestContractDeployer.SendEthTransferAsync(
            _fixture.Web3,
            "0x70997970C51812dc3A010C7d01b50e0d17dc79C8",
            0.001m);
        Assert.NotNull(receipt);

        var blockNumber = (long)receipt.BlockNumber.Value;
        Assert.True(blockNumber > 0);

        await using var connection = await _fixture.CreateDbConnectionAsync();
        await TestContractDeployer.WaitForIndexerCaughtUpAsync(
            connection,
            blockNumber);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"SELECT COUNT(*) FROM ""Blocks""";
        var count = (long)(await cmd.ExecuteScalarAsync())!;

        Assert.True(count > 0, "No blocks found in Postgres after indexing");
    }
}
