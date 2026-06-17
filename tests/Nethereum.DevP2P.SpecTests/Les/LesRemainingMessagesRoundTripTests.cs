using System.Collections.Generic;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Les;
using Xunit;

namespace Nethereum.DevP2P.SpecTests.Les
{
    public class LesRemainingMessagesRoundTripTests
    {
        [Fact]
        public void GetBlockHeaders_ByNumber_RoundTrip()
        {
            var m = new LesGetBlockHeadersMessage
            {
                RequestId = 1,
                StartBlock = 12345,
                MaxHeaders = 10,
                Skip = 0,
                Reverse = false
            };
            var d = LesGetBlockHeadersMessageEncoder.Decode(LesGetBlockHeadersMessageEncoder.Encode(m));
            Assert.Equal(m.RequestId, d.RequestId);
            Assert.Equal(m.StartBlock, d.StartBlock);
            Assert.Null(d.StartBlockHash);
            Assert.Equal(m.MaxHeaders, d.MaxHeaders);
            Assert.False(d.Reverse);
        }

        [Fact]
        public void GetBlockHeaders_ByHash_RoundTrip()
        {
            var m = new LesGetBlockHeadersMessage
            {
                RequestId = 2,
                StartBlockHash = Make32(0xAB),
                MaxHeaders = 5,
                Skip = 1,
                Reverse = true
            };
            var d = LesGetBlockHeadersMessageEncoder.Decode(LesGetBlockHeadersMessageEncoder.Encode(m));
            Assert.Equal(m.StartBlockHash.ToHex(), d.StartBlockHash.ToHex());
            Assert.Equal(5UL, d.MaxHeaders);
            Assert.True(d.Reverse);
        }

        [Fact]
        public void BlockHeaders_RoundTrip()
        {
            var m = new LesBlockHeadersMessage
            {
                RequestId = 7,
                BufferValue = 1_000_000,
                Headers = { Sample(1), Sample(2) }
            };
            var d = LesBlockHeadersMessageEncoder.Decode(LesBlockHeadersMessageEncoder.Encode(m));
            Assert.Equal(m.RequestId, d.RequestId);
            Assert.Equal(m.BufferValue, d.BufferValue);
            Assert.Equal(2, d.Headers.Count);
        }

        [Fact]
        public void GetBlockBodies_RoundTrip()
        {
            var m = new LesGetBlockBodiesMessage
            {
                RequestId = 11,
                BlockHashes = { Make32(0x01), Make32(0x02) }
            };
            var d = LesGetBlockBodiesMessageEncoder.Decode(LesGetBlockBodiesMessageEncoder.Encode(m));
            Assert.Equal(2, d.BlockHashes.Count);
        }

        [Fact]
        public void GetReceipts_RoundTrip()
        {
            var m = new LesGetReceiptsMessage
            {
                RequestId = 13,
                BlockHashes = { Make32(0xCC) }
            };
            var d = LesGetReceiptsMessageEncoder.Decode(LesGetReceiptsMessageEncoder.Encode(m));
            Assert.Equal(13UL, d.RequestId);
            Assert.Single(d.BlockHashes);
        }

        [Fact]
        public void Receipts_RoundTrip_LegacyAndTyped()
        {
            var legacy = new Receipt
            {
                PostStateOrStatus = new byte[] { 0x01 },
                CumulativeGasUsed = 21000,
                Bloom = new byte[256],
                Logs = new List<Log>(),
                TransactionType = 0
            };
            var typed = new Receipt
            {
                PostStateOrStatus = new byte[] { 0x01 },
                CumulativeGasUsed = 42000,
                Bloom = new byte[256],
                Logs = new List<Log>(),
                TransactionType = 2
            };
            var m = new LesReceiptsMessage
            {
                RequestId = 17,
                BufferValue = 500,
                ReceiptsByBlock = new List<List<Receipt>>
                {
                    new() { legacy, typed }
                }
            };
            var d = LesReceiptsMessageEncoder.Decode(LesReceiptsMessageEncoder.Encode(m));
            Assert.Single(d.ReceiptsByBlock);
            Assert.Equal(2, d.ReceiptsByBlock[0].Count);
            Assert.Equal(2, d.ReceiptsByBlock[0][1].TransactionType);
        }

        [Fact]
        public void GetContractCodes_RoundTrip()
        {
            var m = new LesGetContractCodesMessage
            {
                RequestId = 21,
                Requests =
                {
                    new() { BlockHash = Make32(0x10), AccountKey = Make32(0x20) },
                    new() { BlockHash = Make32(0x30), AccountKey = Make32(0x40) }
                }
            };
            var d = LesGetContractCodesMessageEncoder.Decode(LesGetContractCodesMessageEncoder.Encode(m));
            Assert.Equal(2, d.Requests.Count);
            Assert.Equal(m.Requests[1].AccountKey.ToHex(), d.Requests[1].AccountKey.ToHex());
        }

        [Fact]
        public void ContractCodes_RoundTrip()
        {
            var m = new LesContractCodesMessage
            {
                RequestId = 23,
                BufferValue = 100,
                Codes = { new byte[] { 0x60, 0x80 }, new byte[] { 0xFE } }
            };
            var d = LesContractCodesMessageEncoder.Decode(LesContractCodesMessageEncoder.Encode(m));
            Assert.Equal(2, d.Codes.Count);
            Assert.Equal(m.Codes[0].ToHex(), d.Codes[0].ToHex());
        }

        [Fact]
        public void GetHelperTrieProofs_RoundTrip()
        {
            var m = new LesGetHelperTrieProofsMessage
            {
                RequestId = 29,
                Requests =
                {
                    new() { SubType = 1, SectionIdx = 12, Key = Make32(0x10), FromLevel = 2, AuxReq = 0 }
                }
            };
            var d = LesGetHelperTrieProofsMessageEncoder.Decode(LesGetHelperTrieProofsMessageEncoder.Encode(m));
            Assert.Single(d.Requests);
            Assert.Equal(1UL, d.Requests[0].SubType);
            Assert.Equal(12UL, d.Requests[0].SectionIdx);
        }

        [Fact]
        public void HelperTrieProofs_RoundTrip()
        {
            var m = new LesHelperTrieProofsMessage
            {
                RequestId = 31,
                BufferValue = 200,
                Nodes = { new byte[] { 0xAA }, new byte[] { 0xBB, 0xCC } },
                AuxiliaryData = { new byte[] { 0x11 } }
            };
            var d = LesHelperTrieProofsMessageEncoder.Decode(LesHelperTrieProofsMessageEncoder.Encode(m));
            Assert.Equal(2, d.Nodes.Count);
            Assert.Single(d.AuxiliaryData);
        }

        [Fact]
        public void GetTxStatus_RoundTrip()
        {
            var m = new LesGetTxStatusMessage
            {
                RequestId = 37,
                TxHashes = { Make32(0x77), Make32(0x88) }
            };
            var d = LesGetTxStatusMessageEncoder.Decode(LesGetTxStatusMessageEncoder.Encode(m));
            Assert.Equal(2, d.TxHashes.Count);
        }

        [Fact]
        public void TxStatus_RoundTrip()
        {
            var m = new LesTxStatusMessage
            {
                RequestId = 41,
                BufferValue = 99,
                Statuses =
                {
                    new() { Code = LesTxStatusCode.Pending, Data = new byte[] { 0x00 } },
                    new() { Code = LesTxStatusCode.Included, Data = Make32(0xC1) }
                }
            };
            var d = LesTxStatusMessageEncoder.Decode(LesTxStatusMessageEncoder.Encode(m));
            Assert.Equal(2, d.Statuses.Count);
            Assert.Equal(LesTxStatusCode.Pending, d.Statuses[0].Code);
            Assert.Equal(LesTxStatusCode.Included, d.Statuses[1].Code);
            Assert.Equal(m.Statuses[1].Data.ToHex(), d.Statuses[1].Data.ToHex());
        }

        [Fact]
        public void Resume_RoundTrip()
        {
            var m = new LesResumeMessage { BufferValue = 1_234_567 };
            var d = LesResumeMessageEncoder.Decode(LesResumeMessageEncoder.Encode(m));
            Assert.Equal(m.BufferValue, d.BufferValue);
        }

        [Fact]
        public void Stop_Encoding_IsEmptyList()
        {
            var bytes = LesStopMessageEncoder.Encode();
            Assert.NotNull(bytes);
            Assert.True(bytes.Length >= 1);
        }

        private static byte[] Make32(byte fill)
        {
            var b = new byte[32];
            for (int i = 0; i < 32; i++) b[i] = (byte)(fill ^ i);
            return b;
        }

        private static BlockHeader Sample(long number) => new BlockHeader
        {
            ParentHash = new byte[32],
            UnclesHash = "1dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347".HexToByteArray(),
            Coinbase = "0x0000000000000000000000000000000000000000",
            StateRoot = new byte[32],
            TransactionsHash = "56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421".HexToByteArray(),
            ReceiptHash = "56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421".HexToByteArray(),
            LogsBloom = new byte[256],
            Difficulty = Nethereum.Util.EvmUInt256.One,
            BlockNumber = number,
            GasLimit = 30_000_000,
            GasUsed = 0,
            Timestamp = 1700000000,
            ExtraData = new byte[0],
            MixHash = new byte[32],
            Nonce = new byte[8]
        };
    }
}
