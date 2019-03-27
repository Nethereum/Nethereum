using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Nethereum.Model
{
    public class BlockHeader
    {
        public byte[] ParentHash { get; set; }
        public byte[] UnclesHash { get; set; }

        public string Coinbase { get; set; }

        public byte[] StateRoot { get; set; }
        //Trie root
        public byte[] TransactionsHash { get; set; }
        //Trie root
        public byte[] ReceiptHash { get; set; }
        public BigInteger BlockNumber { get; set; }
        public byte[] LogsBloom { get; set; }
        public BigInteger Difficulty { get; set; }
        public long Timestamp { get; set; }
        public long GasLimit { get; set; }
        public long GasUsed { get; set; }
        public byte[] MixHash { get; set; }
        public byte[] ExtraData { get; set; }
        public byte[] Nonce { get; set; }
    }
}
