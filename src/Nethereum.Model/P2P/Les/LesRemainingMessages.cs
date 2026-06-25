using System.Collections.Generic;
using Nethereum.RLP;

namespace Nethereum.Model.P2P.Les
{
    // ===== Helpers used across all les/4 messages =====
    internal static class LesRlpHelpers
    {
        public static byte[] ULongToRlp(ulong v) => v == 0 ? new byte[0] : ((long)v).ToBytesForRLPEncoding();
    }

    // ===== GetBlockHeaders (0x02) / BlockHeaders (0x03) =====
    /// <summary>
    /// les/4 GetBlockHeaders: [reqID, [block, maxHeaders, skip, reverse]]
    /// block can be a 32-byte hash or an integer block number.
    /// </summary>
    public class LesGetBlockHeadersMessage
    {
        public ulong RequestId { get; set; }
        public byte[] StartBlockHash { get; set; }
        public ulong StartBlock { get; set; }
        public ulong MaxHeaders { get; set; }
        public ulong Skip { get; set; }
        public bool Reverse { get; set; }
    }

    public static class LesGetBlockHeadersMessageEncoder
    {
        public static byte[] Encode(LesGetBlockHeadersMessage m)
        {
            var startEncoded = m.StartBlockHash != null
                ? RLP.RLP.EncodeElement(m.StartBlockHash)
                : RLP.RLP.EncodeElement(LesRlpHelpers.ULongToRlp(m.StartBlock));

            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(LesRlpHelpers.ULongToRlp(m.RequestId)),
                RLP.RLP.EncodeList(
                    startEncoded,
                    RLP.RLP.EncodeElement(LesRlpHelpers.ULongToRlp(m.MaxHeaders)),
                    RLP.RLP.EncodeElement(LesRlpHelpers.ULongToRlp(m.Skip)),
                    RLP.RLP.EncodeElement(m.Reverse ? new byte[] { 0x01 } : new byte[0])
                )
            );
        }

        public static LesGetBlockHeadersMessage Decode(byte[] data)
        {
            var outer = (RLPCollection)RLP.RLP.Decode(data);
            var inner = (RLPCollection)outer[1];
            var m = new LesGetBlockHeadersMessage
            {
                RequestId = (ulong)outer[0].RLPData.ToLongFromRLPDecoded(),
                MaxHeaders = (ulong)inner[1].RLPData.ToLongFromRLPDecoded(),
                Skip = (ulong)inner[2].RLPData.ToLongFromRLPDecoded(),
                Reverse = inner[3].RLPData != null && inner[3].RLPData.Length > 0 && inner[3].RLPData[0] != 0
            };
            var startData = inner[0].RLPData ?? new byte[0];
            if (startData.Length == 32) m.StartBlockHash = startData;
            else m.StartBlock = (ulong)startData.ToLongFromRLPDecoded();
            return m;
        }
    }

    /// <summary>
    /// les/4 BlockHeaders: [reqID, BV, headers]
    /// </summary>
    public class LesBlockHeadersMessage
    {
        public ulong RequestId { get; set; }
        public ulong BufferValue { get; set; }
        public List<BlockHeader> Headers { get; set; } = new();
    }

    public static class LesBlockHeadersMessageEncoder
    {
        public static byte[] Encode(LesBlockHeadersMessage m)
        {
            var headerEncoder = BlockHeaderEncoder.Current;
            var encodedHeaders = new byte[m.Headers.Count][];
            for (int i = 0; i < m.Headers.Count; i++)
                encodedHeaders[i] = headerEncoder.Encode(m.Headers[i]);

            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(LesRlpHelpers.ULongToRlp(m.RequestId)),
                RLP.RLP.EncodeElement(LesRlpHelpers.ULongToRlp(m.BufferValue)),
                RLP.RLP.EncodeList(encodedHeaders)
            );
        }

        public static LesBlockHeadersMessage Decode(byte[] data)
        {
            var outer = (RLPCollection)RLP.RLP.Decode(data);
            var m = new LesBlockHeadersMessage
            {
                RequestId = (ulong)outer[0].RLPData.ToLongFromRLPDecoded(),
                BufferValue = (ulong)outer[1].RLPData.ToLongFromRLPDecoded()
            };
            foreach (RLPCollection h in (RLPCollection)outer[2])
                m.Headers.Add(BlockHeaderEncoder.Current.Decode(h.RLPData));
            return m;
        }
    }

    // ===== GetBlockBodies (0x04) / BlockBodies (0x05) =====
    public class LesGetBlockBodiesMessage
    {
        public ulong RequestId { get; set; }
        public List<byte[]> BlockHashes { get; set; } = new();
    }

    public static class LesGetBlockBodiesMessageEncoder
    {
        public static byte[] Encode(LesGetBlockBodiesMessage m)
        {
            var hashes = new byte[m.BlockHashes.Count][];
            for (int i = 0; i < m.BlockHashes.Count; i++)
                hashes[i] = RLP.RLP.EncodeElement(m.BlockHashes[i]);

            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(LesRlpHelpers.ULongToRlp(m.RequestId)),
                RLP.RLP.EncodeList(hashes)
            );
        }

        public static LesGetBlockBodiesMessage Decode(byte[] data)
        {
            var outer = (RLPCollection)RLP.RLP.Decode(data);
            var m = new LesGetBlockBodiesMessage
            {
                RequestId = (ulong)outer[0].RLPData.ToLongFromRLPDecoded()
            };
            foreach (var h in (RLPCollection)outer[1])
                m.BlockHashes.Add(h.RLPData);
            return m;
        }
    }

    public class LesBlockBodiesMessage
    {
        public ulong RequestId { get; set; }
        public ulong BufferValue { get; set; }
        public List<BlockBody> Bodies { get; set; } = new();
    }

    public static class LesBlockBodiesMessageEncoder
    {
        public static byte[] Encode(LesBlockBodiesMessage m)
        {
            var encodedBodies = new byte[m.Bodies.Count][];
            for (int i = 0; i < m.Bodies.Count; i++)
            {
                var body = m.Bodies[i];
                var encodedTxs = new byte[body.Transactions.Count][];
                for (int j = 0; j < body.Transactions.Count; j++)
                    encodedTxs[j] = EncodeTxForBody(body.Transactions[j]);

                var encodedUncles = new byte[body.Uncles.Count][];
                for (int j = 0; j < body.Uncles.Count; j++)
                    encodedUncles[j] = BlockHeaderEncoder.Current.Encode(body.Uncles[j]);

                encodedBodies[i] = RLP.RLP.EncodeList(
                    RLP.RLP.EncodeList(encodedTxs),
                    RLP.RLP.EncodeList(encodedUncles));
            }

            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(LesRlpHelpers.ULongToRlp(m.RequestId)),
                RLP.RLP.EncodeElement(LesRlpHelpers.ULongToRlp(m.BufferValue)),
                RLP.RLP.EncodeList(encodedBodies)
            );
        }

        public static LesBlockBodiesMessage Decode(byte[] data)
        {
            var outer = (RLPCollection)RLP.RLP.Decode(data);
            var m = new LesBlockBodiesMessage
            {
                RequestId = (ulong)outer[0].RLPData.ToLongFromRLPDecoded(),
                BufferValue = (ulong)outer[1].RLPData.ToLongFromRLPDecoded()
            };
            foreach (RLPCollection bodyRlp in (RLPCollection)outer[2])
            {
                var body = new BlockBody();
                foreach (var txRlp in (RLPCollection)bodyRlp[0])
                    body.Transactions.Add(TransactionFactory.CreateTransaction(txRlp.RLPData));
                foreach (RLPCollection uncleRlp in (RLPCollection)bodyRlp[1])
                    body.Uncles.Add(BlockHeaderEncoder.Current.Decode(uncleRlp.RLPData));
                m.Bodies.Add(body);
            }
            return m;
        }

        private static byte[] EncodeTxForBody(ISignedTransaction tx)
        {
            var raw = tx.GetRLPEncoded();
            return (raw.Length > 0 && raw[0] < 0xc0)
                ? RLP.RLP.EncodeElement(raw)
                : raw;
        }
    }

    // ===== GetReceipts (0x06) / Receipts (0x07) =====
    public class LesGetReceiptsMessage
    {
        public ulong RequestId { get; set; }
        public List<byte[]> BlockHashes { get; set; } = new();
    }

    public static class LesGetReceiptsMessageEncoder
    {
        public static byte[] Encode(LesGetReceiptsMessage m)
        {
            var hashes = new byte[m.BlockHashes.Count][];
            for (int i = 0; i < m.BlockHashes.Count; i++)
                hashes[i] = RLP.RLP.EncodeElement(m.BlockHashes[i]);

            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(LesRlpHelpers.ULongToRlp(m.RequestId)),
                RLP.RLP.EncodeList(hashes)
            );
        }

        public static LesGetReceiptsMessage Decode(byte[] data)
        {
            var outer = (RLPCollection)RLP.RLP.Decode(data);
            var m = new LesGetReceiptsMessage
            {
                RequestId = (ulong)outer[0].RLPData.ToLongFromRLPDecoded()
            };
            foreach (var h in (RLPCollection)outer[1])
                m.BlockHashes.Add(h.RLPData);
            return m;
        }
    }

    public class LesReceiptsMessage
    {
        public ulong RequestId { get; set; }
        public ulong BufferValue { get; set; }
        public List<List<Receipt>> ReceiptsByBlock { get; set; } = new();
    }

    public static class LesReceiptsMessageEncoder
    {
        public static byte[] Encode(LesReceiptsMessage m)
        {
            var receiptEncoder = ReceiptEncoder.Current;
            var encodedBlocks = new byte[m.ReceiptsByBlock.Count][];
            for (int i = 0; i < m.ReceiptsByBlock.Count; i++)
            {
                var blockReceipts = m.ReceiptsByBlock[i];
                var encodedReceipts = new byte[blockReceipts.Count][];
                for (int j = 0; j < blockReceipts.Count; j++)
                {
                    var r = blockReceipts[j];
                    encodedReceipts[j] = r.TransactionType > 0
                        ? RLP.RLP.EncodeElement(receiptEncoder.EncodeTyped(r, r.TransactionType))
                        : receiptEncoder.Encode(r);
                }
                encodedBlocks[i] = RLP.RLP.EncodeList(encodedReceipts);
            }

            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(LesRlpHelpers.ULongToRlp(m.RequestId)),
                RLP.RLP.EncodeElement(LesRlpHelpers.ULongToRlp(m.BufferValue)),
                RLP.RLP.EncodeList(encodedBlocks)
            );
        }

        public static LesReceiptsMessage Decode(byte[] data)
        {
            var outer = (RLPCollection)RLP.RLP.Decode(data);
            var m = new LesReceiptsMessage
            {
                RequestId = (ulong)outer[0].RLPData.ToLongFromRLPDecoded(),
                BufferValue = (ulong)outer[1].RLPData.ToLongFromRLPDecoded()
            };
            var receiptEncoder = ReceiptEncoder.Current;
            foreach (RLPCollection blockReceipts in (RLPCollection)outer[2])
            {
                var receipts = new List<Receipt>();
                foreach (var r in blockReceipts)
                    receipts.Add(receiptEncoder.Decode(r.RLPData));
                m.ReceiptsByBlock.Add(receipts);
            }
            return m;
        }
    }

    // ===== GetContractCodes (0x0a) / ContractCodes (0x0b) =====
    public class LesGetContractCodesMessage
    {
        public ulong RequestId { get; set; }
        public List<ContractCodeRequest> Requests { get; set; } = new();
        public class ContractCodeRequest
        {
            public byte[] BlockHash { get; set; } = new byte[32];
            public byte[] AccountKey { get; set; } = new byte[0];
        }
    }

    public static class LesGetContractCodesMessageEncoder
    {
        public static byte[] Encode(LesGetContractCodesMessage m)
        {
            var encoded = new byte[m.Requests.Count][];
            for (int i = 0; i < m.Requests.Count; i++)
            {
                encoded[i] = RLP.RLP.EncodeList(
                    RLP.RLP.EncodeElement(m.Requests[i].BlockHash),
                    RLP.RLP.EncodeElement(m.Requests[i].AccountKey)
                );
            }
            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(LesRlpHelpers.ULongToRlp(m.RequestId)),
                RLP.RLP.EncodeList(encoded)
            );
        }

        public static LesGetContractCodesMessage Decode(byte[] data)
        {
            var outer = (RLPCollection)RLP.RLP.Decode(data);
            var m = new LesGetContractCodesMessage
            {
                RequestId = (ulong)outer[0].RLPData.ToLongFromRLPDecoded()
            };
            foreach (RLPCollection r in (RLPCollection)outer[1])
            {
                m.Requests.Add(new LesGetContractCodesMessage.ContractCodeRequest
                {
                    BlockHash = r[0].RLPData,
                    AccountKey = r[1].RLPData ?? new byte[0]
                });
            }
            return m;
        }
    }

    public class LesContractCodesMessage
    {
        public ulong RequestId { get; set; }
        public ulong BufferValue { get; set; }
        public List<byte[]> Codes { get; set; } = new();
    }

    public static class LesContractCodesMessageEncoder
    {
        public static byte[] Encode(LesContractCodesMessage m)
        {
            var codes = new byte[m.Codes.Count][];
            for (int i = 0; i < m.Codes.Count; i++)
                codes[i] = RLP.RLP.EncodeElement(m.Codes[i]);

            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(LesRlpHelpers.ULongToRlp(m.RequestId)),
                RLP.RLP.EncodeElement(LesRlpHelpers.ULongToRlp(m.BufferValue)),
                RLP.RLP.EncodeList(codes)
            );
        }

        public static LesContractCodesMessage Decode(byte[] data)
        {
            var outer = (RLPCollection)RLP.RLP.Decode(data);
            var m = new LesContractCodesMessage
            {
                RequestId = (ulong)outer[0].RLPData.ToLongFromRLPDecoded(),
                BufferValue = (ulong)outer[1].RLPData.ToLongFromRLPDecoded()
            };
            foreach (var c in (RLPCollection)outer[2])
                m.Codes.Add(c.RLPData);
            return m;
        }
    }

    // ===== GetHelperTrieProofs (0x11) / HelperTrieProofs (0x12) =====
    public class LesGetHelperTrieProofsMessage
    {
        public ulong RequestId { get; set; }
        public List<HelperTrieRequest> Requests { get; set; } = new();
        public class HelperTrieRequest
        {
            public ulong SubType { get; set; }
            public ulong SectionIdx { get; set; }
            public byte[] Key { get; set; } = new byte[0];
            public ulong FromLevel { get; set; }
            public ulong AuxReq { get; set; }
        }
    }

    public static class LesGetHelperTrieProofsMessageEncoder
    {
        public static byte[] Encode(LesGetHelperTrieProofsMessage m)
        {
            var encoded = new byte[m.Requests.Count][];
            for (int i = 0; i < m.Requests.Count; i++)
            {
                var r = m.Requests[i];
                encoded[i] = RLP.RLP.EncodeList(
                    RLP.RLP.EncodeElement(LesRlpHelpers.ULongToRlp(r.SubType)),
                    RLP.RLP.EncodeElement(LesRlpHelpers.ULongToRlp(r.SectionIdx)),
                    RLP.RLP.EncodeElement(r.Key),
                    RLP.RLP.EncodeElement(LesRlpHelpers.ULongToRlp(r.FromLevel)),
                    RLP.RLP.EncodeElement(LesRlpHelpers.ULongToRlp(r.AuxReq))
                );
            }
            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(LesRlpHelpers.ULongToRlp(m.RequestId)),
                RLP.RLP.EncodeList(encoded)
            );
        }

        public static LesGetHelperTrieProofsMessage Decode(byte[] data)
        {
            var outer = (RLPCollection)RLP.RLP.Decode(data);
            var m = new LesGetHelperTrieProofsMessage
            {
                RequestId = (ulong)outer[0].RLPData.ToLongFromRLPDecoded()
            };
            foreach (RLPCollection r in (RLPCollection)outer[1])
            {
                m.Requests.Add(new LesGetHelperTrieProofsMessage.HelperTrieRequest
                {
                    SubType = (ulong)r[0].RLPData.ToLongFromRLPDecoded(),
                    SectionIdx = (ulong)r[1].RLPData.ToLongFromRLPDecoded(),
                    Key = r[2].RLPData ?? new byte[0],
                    FromLevel = (ulong)r[3].RLPData.ToLongFromRLPDecoded(),
                    AuxReq = (ulong)r[4].RLPData.ToLongFromRLPDecoded()
                });
            }
            return m;
        }
    }

    public class LesHelperTrieProofsMessage
    {
        public ulong RequestId { get; set; }
        public ulong BufferValue { get; set; }
        public List<byte[]> Nodes { get; set; } = new();
        public List<byte[]> AuxiliaryData { get; set; } = new();
    }

    public static class LesHelperTrieProofsMessageEncoder
    {
        public static byte[] Encode(LesHelperTrieProofsMessage m)
        {
            var nodes = new byte[m.Nodes.Count][];
            for (int i = 0; i < m.Nodes.Count; i++)
                nodes[i] = RLP.RLP.EncodeElement(m.Nodes[i]);
            var aux = new byte[m.AuxiliaryData.Count][];
            for (int i = 0; i < m.AuxiliaryData.Count; i++)
                aux[i] = RLP.RLP.EncodeElement(m.AuxiliaryData[i]);

            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(LesRlpHelpers.ULongToRlp(m.RequestId)),
                RLP.RLP.EncodeElement(LesRlpHelpers.ULongToRlp(m.BufferValue)),
                RLP.RLP.EncodeList(nodes),
                RLP.RLP.EncodeList(aux)
            );
        }

        public static LesHelperTrieProofsMessage Decode(byte[] data)
        {
            var outer = (RLPCollection)RLP.RLP.Decode(data);
            var m = new LesHelperTrieProofsMessage
            {
                RequestId = (ulong)outer[0].RLPData.ToLongFromRLPDecoded(),
                BufferValue = (ulong)outer[1].RLPData.ToLongFromRLPDecoded()
            };
            foreach (var n in (RLPCollection)outer[2]) m.Nodes.Add(n.RLPData);
            foreach (var a in (RLPCollection)outer[3]) m.AuxiliaryData.Add(a.RLPData);
            return m;
        }
    }

    // ===== SendTxV2 (0x13) =====
    public class LesSendTxV2Message
    {
        public ulong RequestId { get; set; }
        public List<ISignedTransaction> Transactions { get; set; } = new();
    }

    public static class LesSendTxV2MessageEncoder
    {
        public static byte[] Encode(LesSendTxV2Message m)
        {
            var encoded = new byte[m.Transactions.Count][];
            for (int i = 0; i < m.Transactions.Count; i++)
            {
                var raw = m.Transactions[i].GetRLPEncoded();
                encoded[i] = (raw.Length > 0 && raw[0] < 0xc0)
                    ? RLP.RLP.EncodeElement(raw)
                    : raw;
            }
            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(LesRlpHelpers.ULongToRlp(m.RequestId)),
                RLP.RLP.EncodeList(encoded)
            );
        }

        public static LesSendTxV2Message Decode(byte[] data)
        {
            var outer = (RLPCollection)RLP.RLP.Decode(data);
            var m = new LesSendTxV2Message
            {
                RequestId = (ulong)outer[0].RLPData.ToLongFromRLPDecoded()
            };
            foreach (var txRlp in (RLPCollection)outer[1])
                m.Transactions.Add(TransactionFactory.CreateTransaction(txRlp.RLPData));
            return m;
        }
    }

    // ===== GetTxStatus (0x14) / TxStatus (0x15) =====
    public class LesGetTxStatusMessage
    {
        public ulong RequestId { get; set; }
        public List<byte[]> TxHashes { get; set; } = new();
    }

    public static class LesGetTxStatusMessageEncoder
    {
        public static byte[] Encode(LesGetTxStatusMessage m)
        {
            var hashes = new byte[m.TxHashes.Count][];
            for (int i = 0; i < m.TxHashes.Count; i++)
                hashes[i] = RLP.RLP.EncodeElement(m.TxHashes[i]);
            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(LesRlpHelpers.ULongToRlp(m.RequestId)),
                RLP.RLP.EncodeList(hashes)
            );
        }

        public static LesGetTxStatusMessage Decode(byte[] data)
        {
            var outer = (RLPCollection)RLP.RLP.Decode(data);
            var m = new LesGetTxStatusMessage
            {
                RequestId = (ulong)outer[0].RLPData.ToLongFromRLPDecoded()
            };
            foreach (var h in (RLPCollection)outer[1])
                m.TxHashes.Add(h.RLPData);
            return m;
        }
    }

    public enum LesTxStatusCode : byte
    {
        Unknown = 0x00,
        Queued = 0x01,
        Pending = 0x02,
        Included = 0x03,
        Error = 0x04
    }

    public class LesTxStatusMessage
    {
        public ulong RequestId { get; set; }
        public ulong BufferValue { get; set; }
        public List<TxStatus> Statuses { get; set; } = new();
        public class TxStatus
        {
            public LesTxStatusCode Code { get; set; }
            public byte[] Data { get; set; } = new byte[0];
        }
    }

    public static class LesTxStatusMessageEncoder
    {
        public static byte[] Encode(LesTxStatusMessage m)
        {
            var encoded = new byte[m.Statuses.Count][];
            for (int i = 0; i < m.Statuses.Count; i++)
            {
                encoded[i] = RLP.RLP.EncodeList(
                    RLP.RLP.EncodeElement(new[] { (byte)m.Statuses[i].Code }),
                    RLP.RLP.EncodeElement(m.Statuses[i].Data)
                );
            }
            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(LesRlpHelpers.ULongToRlp(m.RequestId)),
                RLP.RLP.EncodeElement(LesRlpHelpers.ULongToRlp(m.BufferValue)),
                RLP.RLP.EncodeList(encoded)
            );
        }

        public static LesTxStatusMessage Decode(byte[] data)
        {
            var outer = (RLPCollection)RLP.RLP.Decode(data);
            var m = new LesTxStatusMessage
            {
                RequestId = (ulong)outer[0].RLPData.ToLongFromRLPDecoded(),
                BufferValue = (ulong)outer[1].RLPData.ToLongFromRLPDecoded()
            };
            foreach (RLPCollection s in (RLPCollection)outer[2])
            {
                var codeBytes = s[0].RLPData ?? new byte[0];
                m.Statuses.Add(new LesTxStatusMessage.TxStatus
                {
                    Code = codeBytes.Length > 0 ? (LesTxStatusCode)codeBytes[0] : LesTxStatusCode.Unknown,
                    Data = s[1].RLPData ?? new byte[0]
                });
            }
            return m;
        }
    }

    // ===== Stop (0x16) / Resume (0x17) — server flow-control signals =====
    /// <summary>
    /// les/4 Stop: signals that the server cannot serve more requests right now.
    /// Payload is empty (RLP empty list).
    /// </summary>
    public static class LesStopMessageEncoder
    {
        public static byte[] Encode() => RLP.RLP.EncodeList();
    }

    public class LesResumeMessage
    {
        public ulong BufferValue { get; set; }
    }

    /// <summary>
    /// les/4 Resume: announces the client may resume; payload [bv].
    /// </summary>
    public static class LesResumeMessageEncoder
    {
        public static byte[] Encode(LesResumeMessage m) =>
            RLP.RLP.EncodeList(RLP.RLP.EncodeElement(LesRlpHelpers.ULongToRlp(m.BufferValue)));

        public static LesResumeMessage Decode(byte[] data)
        {
            var outer = (RLPCollection)RLP.RLP.Decode(data);
            return new LesResumeMessage
            {
                BufferValue = outer.Count > 0 && outer[0].RLPData != null
                    ? (ulong)outer[0].RLPData.ToLongFromRLPDecoded()
                    : 0
            };
        }
    }
}
