namespace Nethereum.Model.P2P
{
    public static class P2PMessageIds
    {
        public const int Hello = 0x00;
        public const int Disconnect = 0x01;
        public const int Ping = 0x02;
        public const int Pong = 0x03;
    }

    /// <summary>
    /// eth protocol message IDs. These are stable across eth/66, eth/67, eth/68,
    /// eth/69, eth/70. Higher versions add new messages; lower versions ignore
    /// IDs they don't recognize.
    ///
    /// - eth/68 introduced typed announcements in NewPooledTransactionHashes (0x08).
    /// - eth/69 added BlockRangeUpdate (0x11) and block-range fields in Status.
    /// - eth/70 added partial receipt requests in GetReceipts (0x0f).
    /// </summary>
    public static class EthMessageIds
    {
        public const int Status = 0x00;
        public const int NewBlockHashes = 0x01;
        public const int Transactions = 0x02;
        public const int GetBlockHeaders = 0x03;
        public const int BlockHeaders = 0x04;
        public const int GetBlockBodies = 0x05;
        public const int BlockBodies = 0x06;
        public const int NewBlock = 0x07;
        public const int NewPooledTransactionHashes = 0x08;
        public const int GetPooledTransactions = 0x09;
        public const int PooledTransactions = 0x0a;
        public const int GetReceipts = 0x0f;
        public const int Receipts = 0x10;
        public const int BlockRangeUpdate = 0x11;
    }

    public static class Eth68MessageIds
    {
        public const int Status = EthMessageIds.Status;
        public const int NewBlockHashes = EthMessageIds.NewBlockHashes;
        public const int Transactions = EthMessageIds.Transactions;
        public const int GetBlockHeaders = EthMessageIds.GetBlockHeaders;
        public const int BlockHeaders = EthMessageIds.BlockHeaders;
        public const int GetBlockBodies = EthMessageIds.GetBlockBodies;
        public const int BlockBodies = EthMessageIds.BlockBodies;
        public const int NewBlock = EthMessageIds.NewBlock;
        public const int NewPooledTransactionHashes = EthMessageIds.NewPooledTransactionHashes;
        public const int GetPooledTransactions = EthMessageIds.GetPooledTransactions;
        public const int PooledTransactions = EthMessageIds.PooledTransactions;
        public const int GetReceipts = EthMessageIds.GetReceipts;
        public const int Receipts = EthMessageIds.Receipts;
    }

    public enum DisconnectReason : byte
    {
        Requested = 0x00,
        TcpError = 0x01,
        ProtocolBreach = 0x02,
        UselessPeer = 0x03,
        TooManyPeers = 0x04,
        AlreadyConnected = 0x05,
        IncompatibleVersion = 0x06,
        NullIdentity = 0x07,
        ClientQuitting = 0x08,
        UnexpectedIdentity = 0x09,
        ConnectedToSelf = 0x0a,
        PingTimeout = 0x0b,
        SubprotocolReason = 0x10
    }
}
