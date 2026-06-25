namespace Nethereum.Model.P2P.Les
{
    /// <summary>
    /// les/4 message IDs per https://github.com/ethereum/devp2p/blob/master/caps/les.md.
    /// Servers reply to client requests; clients announce new heads via Announce.
    /// Some legacy IDs (GetProofs, Proofs, SendTx, etc.) were deprecated in les/2.
    /// </summary>
    public static class LesMessageIds
    {
        public const int Status = 0x00;
        public const int Announce = 0x01;
        public const int GetBlockHeaders = 0x02;
        public const int BlockHeaders = 0x03;
        public const int GetBlockBodies = 0x04;
        public const int BlockBodies = 0x05;
        public const int GetReceipts = 0x06;
        public const int Receipts = 0x07;
        public const int GetProofs_Deprecated = 0x08;
        public const int Proofs_Deprecated = 0x09;
        public const int GetContractCodes = 0x0a;
        public const int ContractCodes = 0x0b;
        public const int SendTx_Deprecated = 0x0c;
        public const int GetHeaderProofs_Deprecated = 0x0d;
        public const int HeaderProofs_Deprecated = 0x0e;
        public const int GetProofsV2 = 0x0f;
        public const int ProofsV2 = 0x10;
        public const int GetHelperTrieProofs = 0x11;
        public const int HelperTrieProofs = 0x12;
        public const int SendTxV2 = 0x13;
        public const int GetTxStatus = 0x14;
        public const int TxStatus = 0x15;
        public const int Stop = 0x16;
        public const int Resume = 0x17;
    }
}
