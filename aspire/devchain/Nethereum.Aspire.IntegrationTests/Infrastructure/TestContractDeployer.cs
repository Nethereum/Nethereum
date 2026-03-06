using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Npgsql;

namespace Nethereum.Aspire.IntegrationTests.Infrastructure;

public static class TestContractDeployer
{
    public static async Task<TransactionReceipt> SendEthTransferAsync(
        Nethereum.Web3.Web3 web3,
        string toAddress,
        decimal ethAmount)
    {
        var txInput = new TransactionInput
        {
            From = web3.TransactionManager.Account.Address,
            To = toAddress,
            Value = new HexBigInteger(Nethereum.Util.UnitConversion.Convert.ToWei(ethAmount))
        };

        var receipt = await web3.Eth.TransactionManager
            .SendTransactionAndWaitForReceiptAsync(txInput);
        return receipt;
    }

    public static async Task WaitForIndexerCaughtUpAsync(
        NpgsqlConnection connection,
        long targetBlockNumber,
        TimeSpan? timeout = null)
    {
        timeout ??= TimeSpan.FromSeconds(120);
        var deadline = DateTime.UtcNow + timeout.Value;
        long lastSeenBlock = -1;
        string lastError = "no data";

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                await using var cmd = connection.CreateCommand();
                cmd.CommandText = @"SELECT lastblockprocessed FROM ""BlockProgress"" ORDER BY rowindex DESC LIMIT 1";
                var result = await cmd.ExecuteScalarAsync();

                if (result == null || result == DBNull.Value)
                {
                    lastError = "BlockProgress table empty";
                }
                else
                {
                    long parsedBlock = -1;
                    if (result is string blockStr)
                    {
                        if (BigInteger.TryParse(blockStr, out var bigVal))
                            parsedBlock = (long)bigVal;
                        else
                            lastError = $"unparseable value: '{blockStr}'";
                    }
                    else if (result is long l)
                        parsedBlock = l;
                    else if (result is int i)
                        parsedBlock = i;
                    else
                        lastError = $"unexpected type: {result.GetType().Name}={result}";

                    if (parsedBlock >= 0)
                    {
                        lastSeenBlock = parsedBlock;
                        lastError = null;
                        if (parsedBlock >= targetBlockNumber)
                            return;
                    }
                }
            }
            catch (NpgsqlException ex)
            {
                lastError = $"DB error: {ex.Message}";
            }

            await Task.Delay(500);
        }

        throw new TimeoutException(
            $"Indexer did not catch up to block {targetBlockNumber} within {timeout.Value.TotalSeconds}s. " +
            $"Last indexed block: {lastSeenBlock}. Detail: {lastError ?? "indexer processing but behind"}");
    }
}
