using System.Collections.Generic;
using Nethereum.RLP;

namespace Nethereum.Model.P2P.Les
{
    /// <summary>
    /// les/4 GetProofsV2 (0x0f): request state proofs for [block_hash, account_key, storage_key, fromLevel].
    /// Format: [requestId, BV, [[block_hash, key1, key2, fromLevel], ...]]
    /// key1 = account hash; key2 = storage key (32 bytes) or empty for account proof.
    /// </summary>
    public class GetProofsV2Message
    {
        public ulong RequestId { get; set; }
        public List<ProofRequest> Requests { get; set; } = new();

        public class ProofRequest
        {
            public byte[] BlockHash { get; set; } = new byte[32];
            public byte[] AccountKey { get; set; } = new byte[0];
            public byte[] StorageKey { get; set; } = new byte[0];
            public uint FromLevel { get; set; }
        }
    }

    public static class GetProofsV2MessageEncoder
    {
        public static byte[] Encode(GetProofsV2Message msg)
        {
            var encodedRequests = new byte[msg.Requests.Count][];
            for (int i = 0; i < msg.Requests.Count; i++)
            {
                var r = msg.Requests[i];
                encodedRequests[i] = RLP.RLP.EncodeList(
                    RLP.RLP.EncodeElement(r.BlockHash),
                    RLP.RLP.EncodeElement(r.AccountKey),
                    RLP.RLP.EncodeElement(r.StorageKey),
                    RLP.RLP.EncodeElement(LongToRlp(r.FromLevel))
                );
            }
            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(LongToRlp((long)msg.RequestId)),
                RLP.RLP.EncodeList(encodedRequests)
            );
        }

        public static GetProofsV2Message Decode(byte[] data)
        {
            var outer = (RLPCollection)RLP.RLP.Decode(data);
            var msg = new GetProofsV2Message
            {
                RequestId = (ulong)outer[0].RLPData.ToLongFromRLPDecoded()
            };
            var requests = (RLPCollection)outer[1];
            foreach (RLPCollection rRlp in requests)
            {
                msg.Requests.Add(new GetProofsV2Message.ProofRequest
                {
                    BlockHash = rRlp[0].RLPData,
                    AccountKey = rRlp[1].RLPData ?? new byte[0],
                    StorageKey = rRlp[2].RLPData ?? new byte[0],
                    FromLevel = (uint)rRlp[3].RLPData.ToLongFromRLPDecoded()
                });
            }
            return msg;
        }

        private static byte[] LongToRlp(long value)
        {
            if (value == 0) return new byte[0];
            return value.ToBytesForRLPEncoding();
        }
    }

    /// <summary>
    /// les/4 ProofsV2 (0x10): response with Merkle proof nodes.
    /// Format: [requestId, BV, [trie_node_list]] where trie_node_list is the
    /// concatenated list of RLP-encoded trie nodes proving each requested key.
    /// </summary>
    public class ProofsV2Message
    {
        public ulong RequestId { get; set; }
        public ulong BufferValue { get; set; }
        public List<byte[]> Nodes { get; set; } = new();
    }

    public static class ProofsV2MessageEncoder
    {
        public static byte[] Encode(ProofsV2Message msg)
        {
            var encodedNodes = new byte[msg.Nodes.Count][];
            for (int i = 0; i < msg.Nodes.Count; i++)
                encodedNodes[i] = RLP.RLP.EncodeElement(msg.Nodes[i]);

            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(LongToRlp((long)msg.RequestId)),
                RLP.RLP.EncodeElement(LongToRlp((long)msg.BufferValue)),
                RLP.RLP.EncodeList(encodedNodes)
            );
        }

        public static ProofsV2Message Decode(byte[] data)
        {
            var outer = (RLPCollection)RLP.RLP.Decode(data);
            var msg = new ProofsV2Message
            {
                RequestId = (ulong)outer[0].RLPData.ToLongFromRLPDecoded(),
                BufferValue = (ulong)outer[1].RLPData.ToLongFromRLPDecoded()
            };
            var nodeList = (RLPCollection)outer[2];
            foreach (var n in nodeList)
                msg.Nodes.Add(n.RLPData);
            return msg;
        }

        private static byte[] LongToRlp(long value)
        {
            if (value == 0) return new byte[0];
            return value.ToBytesForRLPEncoding();
        }
    }
}
