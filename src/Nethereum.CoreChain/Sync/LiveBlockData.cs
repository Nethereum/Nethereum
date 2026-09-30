using System;
using System.Collections.Generic;
using Nethereum.Model;

namespace Nethereum.CoreChain.Sync
{
    public class LiveBlockData
    {
        public BlockHeader Header { get; set; } = null!;
        public List<ISignedTransaction> Transactions { get; set; } = new();
        public List<Receipt> Receipts { get; set; } = new();
        public byte[] BlockHash { get; set; } = Array.Empty<byte>();
        public bool IsSoft { get; set; } = true;
    }
}
