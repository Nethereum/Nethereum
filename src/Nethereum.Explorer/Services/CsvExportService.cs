using System.Text;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;

namespace Nethereum.Explorer.Services;

public static class CsvExportService
{
    public static string ExportTransactions(List<ITransactionView> transactions)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Hash,BlockNumber,From,To,Value,Gas,GasUsed,GasPrice,Nonce,Status,Timestamp");
        foreach (var tx in transactions)
        {
            sb.AppendLine($"{Esc(tx.Hash)},{Esc(tx.BlockNumber)},{Esc(tx.AddressFrom)},{Esc(tx.AddressTo)},{Esc(tx.Value)},{Esc(tx.Gas)},{Esc(tx.GasUsed)},{Esc(tx.GasPrice)},{Esc(tx.Nonce)},{Esc(tx.Failed ? "Failed" : "Success")},{Esc(ExplorerFormatUtils.FormatTimestamp(tx.TimeStamp))}");
        }
        return sb.ToString();
    }

    public static string ExportContracts(List<IContractView> contracts)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Address,Name,Creator,TransactionHash");
        foreach (var c in contracts)
        {
            sb.AppendLine($"{Esc(c.Address)},{Esc(c.Name)},{Esc(c.Creator)},{Esc(c.TransactionHash)}");
        }
        return sb.ToString();
    }

    public static string ExportBlocks(List<IBlockView> blocks)
    {
        var sb = new StringBuilder();
        sb.AppendLine("BlockNumber,Hash,Miner,GasUsed,GasLimit,TransactionCount,Timestamp");
        foreach (var block in blocks)
        {
            sb.AppendLine($"{Esc(block.BlockNumber)},{Esc(block.Hash)},{Esc(block.Miner)},{Esc(block.GasUsed)},{Esc(block.GasLimit)},{Esc(block.TransactionCount)},{Esc(ExplorerFormatUtils.FormatTimestamp(block.TimeStamp))}");
        }
        return sb.ToString();
    }

    private static string Esc(object? value)
    {
        var s = value?.ToString() ?? "";
        s = s.Replace("\"", "\"\"");
        if (s.Length > 0 && (s[0] == '=' || s[0] == '+' || s[0] == '-' || s[0] == '@'))
            s = "'" + s;
        return $"\"{s}\"";
    }
}
