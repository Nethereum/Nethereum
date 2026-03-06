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
            sb.AppendLine($"\"{tx.Hash}\",\"{tx.BlockNumber}\",\"{tx.AddressFrom}\",\"{tx.AddressTo}\",\"{tx.Value}\",\"{tx.Gas}\",\"{tx.GasUsed}\",\"{tx.GasPrice}\",\"{tx.Nonce}\",\"{(tx.Failed ? "Failed" : "Success")}\",\"{ExplorerFormatUtils.FormatTimestamp(tx.TimeStamp)}\"");
        }
        return sb.ToString();
    }

    public static string ExportContracts(List<IContractView> contracts)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Address,Name,Creator,TransactionHash");
        foreach (var c in contracts)
        {
            sb.AppendLine($"\"{c.Address}\",\"{c.Name}\",\"{c.Creator}\",\"{c.TransactionHash}\"");
        }
        return sb.ToString();
    }

    public static string ExportBlocks(List<IBlockView> blocks)
    {
        var sb = new StringBuilder();
        sb.AppendLine("BlockNumber,Hash,Miner,GasUsed,GasLimit,TransactionCount,Timestamp");
        foreach (var block in blocks)
        {
            sb.AppendLine($"\"{block.BlockNumber}\",\"{block.Hash}\",\"{block.Miner}\",\"{block.GasUsed}\",\"{block.GasLimit}\",\"{block.TransactionCount}\",\"{ExplorerFormatUtils.FormatTimestamp(block.TimeStamp)}\"");
        }
        return sb.ToString();
    }
}
