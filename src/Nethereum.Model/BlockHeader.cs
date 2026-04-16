using System;
using System.Collections.Generic;
using System.Text;
using Nethereum.Util;

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
        public EvmUInt256 BlockNumber { get; set; }
        public byte[] LogsBloom { get; set; }
        public EvmUInt256 Difficulty { get; set; }
        public long Timestamp { get; set; }
        public long GasLimit { get; set; }
        public long GasUsed { get; set; }
        public byte[] MixHash { get; set; }
        public byte[] ExtraData { get; set; }
        public byte[] Nonce { get; set; }
        public EvmUInt256? BaseFee { get; set; }
        public byte[] WithdrawalsRoot { get; set; }
        public long? BlobGasUsed { get; set; }
        public long? ExcessBlobGas { get; set; }
        public byte[] ParentBeaconBlockRoot { get; set; }
        public byte[] RequestsHash { get; set; }
    }
}
