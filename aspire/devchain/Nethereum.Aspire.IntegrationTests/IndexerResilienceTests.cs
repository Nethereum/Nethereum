using System.Numerics;
using Nethereum.Aspire.IntegrationTests.Infrastructure;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Xunit;

namespace Nethereum.Aspire.IntegrationTests;

[Collection("Aspire")]
public class IndexerResilienceTests
{
    private readonly AspireFixture _fixture;

    public IndexerResilienceTests(AspireFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Indexer_CatchesUp_AfterBurstOfTransactions()
    {
        var txHashes = new List<string>();
        for (int i = 0; i < 10; i++)
        {
            var txInput = new TransactionInput
            {
                From = _fixture.Web3.TransactionManager.Account.Address,
                To = "0x70997970C51812dc3A010C7d01b50e0d17dc79C8",
                Value = new HexBigInteger(Nethereum.Util.UnitConversion.Convert.ToWei(0.001m))
            };
            var txHash = await _fixture.Web3.Eth.TransactionManager.SendTransactionAsync(txInput);
            txHashes.Add(txHash);
        }

        var lastReceipt = await _fixture.Web3.Eth.Transactions.GetTransactionReceipt
            .SendRequestAsync(txHashes.Last());

        while (lastReceipt == null)
        {
            await Task.Delay(200);
            lastReceipt = await _fixture.Web3.Eth.Transactions.GetTransactionReceipt
                .SendRequestAsync(txHashes.Last());
        }

        var targetBlock = (long)lastReceipt.BlockNumber.Value;

        await using var connection = await _fixture.CreateDbConnectionAsync();
        await TestContractDeployer.WaitForIndexerCaughtUpAsync(
            connection, targetBlock);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"SELECT COUNT(*) FROM ""Transactions""";
        var txCount = (long)(await cmd.ExecuteScalarAsync())!;

        Assert.True(txCount >= 10,
            $"Expected at least 10 transactions indexed, got {txCount}");
    }

    [Fact]
    public async Task Indexer_BlockProgress_ConvergesToChainHead()
    {
        var chainHead = await _fixture.Web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();

        await using var connection = await _fixture.CreateDbConnectionAsync();
        await TestContractDeployer.WaitForIndexerCaughtUpAsync(
            connection, (long)chainHead.Value);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"SELECT lastblockprocessed FROM ""BlockProgress"" ORDER BY rowindex DESC LIMIT 1";
        var result = await cmd.ExecuteScalarAsync();

        long indexedBlock = -1;
        if (result is string s && BigInteger.TryParse(s, out var bigVal))
            indexedBlock = (long)bigVal;
        else if (result is long l)
            indexedBlock = l;
        else if (result is int i)
            indexedBlock = i;

        Assert.True(indexedBlock >= (long)chainHead.Value,
            $"Indexer block {indexedBlock} has not caught up to chain head {chainHead.Value}");
    }
}
