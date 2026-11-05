using System.Linq;
using System.Numerics;
using Nethereum.Contracts;
using Nethereum.Contracts.Standards.ERC20.ContractDefinition;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;

namespace Nethereum.X402.Blockchain;

internal static class SettlementTransferCheck
{
    public static bool HasExactlyOneMatchingTransfer(
        TransactionReceipt receipt, string asset, string payer, string payTo, BigInteger amount)
    {
        var matching = receipt.DecodeAllEvents<TransferEventDTO>()
            .Where(transfer => transfer.Log.Address.IsTheSameAddress(asset)
                && transfer.Event.From.IsTheSameAddress(payer)
                && transfer.Event.To.IsTheSameAddress(payTo))
            .ToList();

        return matching.Count == 1 && matching[0].Event.Value == amount;
    }
}
