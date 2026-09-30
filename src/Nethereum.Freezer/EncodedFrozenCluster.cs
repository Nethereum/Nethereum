using System;

namespace Nethereum.Freezer
{
    public readonly struct EncodedFrozenCluster
    {
        public byte[] Header { get; }
        public byte[] Hash { get; }
        public byte[] Body { get; }
        public byte[] Receipts { get; }
        public byte[] Bal { get; }

        public EncodedFrozenCluster(byte[] header, byte[] hash, byte[] body, byte[] receipts, byte[] bal)
        {
            Header = header ?? throw new ArgumentNullException(nameof(header));
            Hash = hash ?? throw new ArgumentNullException(nameof(hash));
            Body = body ?? throw new ArgumentNullException(nameof(body));
            Receipts = receipts ?? throw new ArgumentNullException(nameof(receipts));
            Bal = bal ?? throw new ArgumentNullException(nameof(bal));
        }
    }
}
