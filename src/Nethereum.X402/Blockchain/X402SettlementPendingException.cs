using Nethereum.Documentation;
using System;

namespace Nethereum.X402.Blockchain;

[NethereumDocExample(DocSection.DeFi, "x402-payments", "X402SettlementPendingException - broadcast without a receipt")]
public class X402SettlementPendingException : Exception
{
    public X402SettlementPendingException(string transactionHash, Exception innerException)
        : base($"Settlement transaction {transactionHash} was broadcast but its receipt was not obtained.", innerException)
    {
        TransactionHash = transactionHash;
    }

    public string TransactionHash { get; }
}
