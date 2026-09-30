using System;
using System.Collections.Generic;

namespace Nethereum.CoreChain.Storage.History
{
    public interface IHistoryStore
    {
        void AppendBlock(HistoryBlockWrite block);

        TxLocation? FindTransaction(byte[] txHash);

        ulong? FindBlock(byte[] blockHash);

        byte[] GetHeader(ulong blockNumber);
        byte[] GetBlockMeta(ulong blockNumber);
        byte[] GetTransaction(ulong blockNumber, uint txIndex);
        byte[] GetReceipt(ulong blockNumber, uint txIndex);

        IReadOnlyList<byte[]> GetBlockTransactions(ulong blockNumber);

        ulong? GetTip();

        void HandleReorg(ulong revertToBlock);
    }

    public readonly struct TxLocation : IEquatable<TxLocation>
    {
        public ulong BlockNumber { get; }
        public uint TxIndex { get; }
        public TxLocation(ulong blockNumber, uint txIndex) { BlockNumber = blockNumber; TxIndex = txIndex; }
        public bool Equals(TxLocation other) => BlockNumber == other.BlockNumber && TxIndex == other.TxIndex;
        public override bool Equals(object obj) => obj is TxLocation o && Equals(o);
        public override int GetHashCode() => (BlockNumber, TxIndex).GetHashCode();
        public override string ToString() => $"({BlockNumber},{TxIndex})";
    }

    public sealed class HistoryBlockWrite
    {
        public ulong BlockNumber { get; set; }
        public byte[] BlockHash { get; set; }
        public byte[] Header { get; set; }
        public byte[] Meta { get; set; }
        public IReadOnlyList<HistoryTxWrite> Transactions { get; set; } = Array.Empty<HistoryTxWrite>();

        public IReadOnlyList<(string Cf, byte[] Key, byte[] Value)> SequentialExtra { get; set; }
        public IReadOnlyList<(string Cf, byte[] Key, byte[] Value)> IndexExtra { get; set; }
    }

    public sealed class HistoryTxWrite
    {
        public byte[] TxHash { get; set; }
        public byte[] Tx { get; set; }
        public byte[] Receipt { get; set; }
    }
}
