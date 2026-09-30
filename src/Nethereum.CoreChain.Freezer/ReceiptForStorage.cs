using System;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Documentation;
using Nethereum.Model;

namespace Nethereum.CoreChain.Freezer
{
    [NethereumDocExample(DocSection.ChainInfrastructure, "chain-infrastructure", "ReceiptForStorage — geth's trimmed 3-field storage receipt")]
    public sealed class ReceiptForStorage
    {
        public byte[] PostStateOrStatus { get; }
        public BigInteger CumulativeGasUsed { get; }
        public IReadOnlyList<Log> Logs { get; }

        public ReceiptForStorage(byte[] postStateOrStatus, BigInteger cumulativeGasUsed, IReadOnlyList<Log> logs)
        {
            PostStateOrStatus = postStateOrStatus ?? Array.Empty<byte>();
            CumulativeGasUsed = cumulativeGasUsed;
            Logs = logs ?? new List<Log>();
        }
    }
}
