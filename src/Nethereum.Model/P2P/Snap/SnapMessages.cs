using System.Collections.Generic;
using Nethereum.RLP;

namespace Nethereum.Model.P2P.Snap
{
    /// <summary>
    /// snap/1 message IDs per https://github.com/ethereum/devp2p/blob/master/caps/snap.md.
    /// Used for fast initial state sync — pulls state trie ranges instead of
    /// re-executing every historical block.
    /// </summary>
    public static class SnapMessageIds
    {
        public const int GetAccountRange = 0x00;
        public const int AccountRange = 0x01;
        public const int GetStorageRanges = 0x02;
        public const int StorageRanges = 0x03;
        public const int GetByteCodes = 0x04;
        public const int ByteCodes = 0x05;
        public const int GetTrieNodes = 0x06;
        public const int TrieNodes = 0x07;
    }

    // ===== GetAccountRange =====
    public class GetAccountRangeMessage
    {
        public ulong RequestId { get; set; }
        public byte[] RootHash { get; set; } = new byte[32];
        public byte[] StartingHash { get; set; } = new byte[32];
        public byte[] LimitHash { get; set; } = new byte[32];
        public ulong ResponseBytes { get; set; }
    }

    public static class GetAccountRangeMessageEncoder
    {
        public static byte[] Encode(GetAccountRangeMessage m) => RLP.RLP.EncodeList(
            RLP.RLP.EncodeElement(ULongToRlp(m.RequestId)),
            RLP.RLP.EncodeElement(m.RootHash),
            RLP.RLP.EncodeElement(m.StartingHash),
            RLP.RLP.EncodeElement(m.LimitHash),
            RLP.RLP.EncodeElement(ULongToRlp(m.ResponseBytes))
        );

        public static GetAccountRangeMessage Decode(byte[] data)
        {
            var o = (RLPCollection)RLP.RLP.Decode(data);
            return new GetAccountRangeMessage
            {
                RequestId = (ulong)o[0].RLPData.ToLongFromRLPDecoded(),
                RootHash = o[1].RLPData,
                StartingHash = o[2].RLPData,
                LimitHash = o[3].RLPData,
                ResponseBytes = (ulong)o[4].RLPData.ToLongFromRLPDecoded()
            };
        }

        private static byte[] ULongToRlp(ulong v) => v == 0 ? new byte[0] : ((long)v).ToBytesForRLPEncoding();
    }

    // ===== AccountRange =====
    public class AccountRangeMessage
    {
        public ulong RequestId { get; set; }
        public List<AccountEntry> Accounts { get; set; } = new();
        public List<byte[]> Proof { get; set; } = new();

        public class AccountEntry
        {
            public byte[] Hash { get; set; } = new byte[32];
            public byte[] Body { get; set; } = new byte[0];
        }
    }

    public static class AccountRangeMessageEncoder
    {
        public static byte[] Encode(AccountRangeMessage m)
        {
            var accountElements = new byte[m.Accounts.Count][];
            for (int i = 0; i < m.Accounts.Count; i++)
            {
                // accBody is rlp.RawValue per go-ethereum's AccountData struct
                // — it's already an RLP-encoded slim-account LIST and must be
                // embedded directly, not re-wrapped as a byte string.
                accountElements[i] = RLP.RLP.EncodeList(
                    RLP.RLP.EncodeElement(m.Accounts[i].Hash),
                    m.Accounts[i].Body);
            }
            var proofElements = new byte[m.Proof.Count][];
            for (int i = 0; i < m.Proof.Count; i++)
                proofElements[i] = RLP.RLP.EncodeElement(m.Proof[i]);

            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(ULongToRlp(m.RequestId)),
                RLP.RLP.EncodeList(accountElements),
                RLP.RLP.EncodeList(proofElements)
            );
        }

        public static AccountRangeMessage Decode(byte[] data)
        {
            var o = (RLPCollection)RLP.RLP.Decode(data);
            var m = new AccountRangeMessage
            {
                RequestId = (ulong)o[0].RLPData.ToLongFromRLPDecoded()
            };
            foreach (RLPCollection accRlp in (RLPCollection)o[1])
            {
                // accBody is rlp.RawValue — for a slim account that's an RLP
                // list. Reconstruct the wire bytes (length prefix + payload)
                // from the inner RLPCollection so callers can re-decode as a
                // slim-account list.
                var bodyElement = accRlp[1];
                byte[] bodyBytes;
                if (bodyElement is RLPCollection bodyList)
                {
                    var items = new byte[bodyList.Count][];
                    for (int i = 0; i < bodyList.Count; i++)
                        items[i] = RLP.RLP.EncodeElement(bodyList[i].RLPData);
                    bodyBytes = RLP.RLP.EncodeList(items);
                }
                else
                {
                    bodyBytes = bodyElement.RLPData ?? new byte[0];
                }
                m.Accounts.Add(new AccountRangeMessage.AccountEntry
                {
                    Hash = accRlp[0].RLPData,
                    Body = bodyBytes
                });
            }
            foreach (var pNode in (RLPCollection)o[2])
                m.Proof.Add(pNode.RLPData);
            return m;
        }

        private static byte[] ULongToRlp(ulong v) => v == 0 ? new byte[0] : ((long)v).ToBytesForRLPEncoding();
    }

    // ===== GetStorageRanges / StorageRanges =====
    public class GetStorageRangesMessage
    {
        public ulong RequestId { get; set; }
        public byte[] RootHash { get; set; } = new byte[32];
        public List<byte[]> AccountHashes { get; set; } = new();
        public byte[] StartingHash { get; set; } = new byte[0];
        public byte[] LimitHash { get; set; } = new byte[0];
        public ulong ResponseBytes { get; set; }
    }

    public static class GetStorageRangesMessageEncoder
    {
        public static byte[] Encode(GetStorageRangesMessage m)
        {
            var accountHashElements = new byte[m.AccountHashes.Count][];
            for (int i = 0; i < m.AccountHashes.Count; i++)
                accountHashElements[i] = RLP.RLP.EncodeElement(m.AccountHashes[i]);

            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(ULongToRlp(m.RequestId)),
                RLP.RLP.EncodeElement(m.RootHash),
                RLP.RLP.EncodeList(accountHashElements),
                RLP.RLP.EncodeElement(m.StartingHash),
                RLP.RLP.EncodeElement(m.LimitHash),
                RLP.RLP.EncodeElement(ULongToRlp(m.ResponseBytes))
            );
        }

        public static GetStorageRangesMessage Decode(byte[] data)
        {
            var o = (RLPCollection)RLP.RLP.Decode(data);
            var m = new GetStorageRangesMessage
            {
                RequestId = (ulong)o[0].RLPData.ToLongFromRLPDecoded(),
                RootHash = o[1].RLPData,
                StartingHash = o[3].RLPData ?? new byte[0],
                LimitHash = o[4].RLPData ?? new byte[0],
                ResponseBytes = (ulong)o[5].RLPData.ToLongFromRLPDecoded()
            };
            foreach (var h in (RLPCollection)o[2])
                m.AccountHashes.Add(h.RLPData);
            return m;
        }

        private static byte[] ULongToRlp(ulong v) => v == 0 ? new byte[0] : ((long)v).ToBytesForRLPEncoding();
    }

    public class StorageRangesMessage
    {
        public ulong RequestId { get; set; }
        /// <summary>
        /// Outer list: one entry per requested account.
        /// Inner list: consecutive slots for that account, each [slotHash, slotData].
        /// </summary>
        public List<List<SlotEntry>> Slots { get; set; } = new();
        public List<byte[]> Proof { get; set; } = new();

        public class SlotEntry
        {
            public byte[] Hash { get; set; } = new byte[32];
            public byte[] Data { get; set; } = new byte[0];
        }
    }

    public static class StorageRangesMessageEncoder
    {
        public static byte[] Encode(StorageRangesMessage m)
        {
            var accountSlotLists = new byte[m.Slots.Count][];
            for (int i = 0; i < m.Slots.Count; i++)
            {
                var slotList = m.Slots[i];
                var slotEncoded = new byte[slotList.Count][];
                for (int j = 0; j < slotList.Count; j++)
                {
                    slotEncoded[j] = RLP.RLP.EncodeList(
                        RLP.RLP.EncodeElement(slotList[j].Hash),
                        RLP.RLP.EncodeElement(slotList[j].Data)
                    );
                }
                accountSlotLists[i] = RLP.RLP.EncodeList(slotEncoded);
            }

            var proofElements = new byte[m.Proof.Count][];
            for (int i = 0; i < m.Proof.Count; i++)
                proofElements[i] = RLP.RLP.EncodeElement(m.Proof[i]);

            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(ULongToRlp(m.RequestId)),
                RLP.RLP.EncodeList(accountSlotLists),
                RLP.RLP.EncodeList(proofElements)
            );
        }

        public static StorageRangesMessage Decode(byte[] data)
        {
            var o = (RLPCollection)RLP.RLP.Decode(data);
            var m = new StorageRangesMessage
            {
                RequestId = (ulong)o[0].RLPData.ToLongFromRLPDecoded()
            };
            foreach (RLPCollection acctRlp in (RLPCollection)o[1])
            {
                var slotsForAccount = new List<StorageRangesMessage.SlotEntry>();
                foreach (RLPCollection slotRlp in acctRlp)
                {
                    slotsForAccount.Add(new StorageRangesMessage.SlotEntry
                    {
                        Hash = slotRlp[0].RLPData,
                        Data = slotRlp[1].RLPData ?? new byte[0]
                    });
                }
                m.Slots.Add(slotsForAccount);
            }
            foreach (var p in (RLPCollection)o[2])
                m.Proof.Add(p.RLPData);
            return m;
        }

        private static byte[] ULongToRlp(ulong v) => v == 0 ? new byte[0] : ((long)v).ToBytesForRLPEncoding();
    }

    // ===== GetByteCodes / ByteCodes =====
    public class GetByteCodesMessage
    {
        public ulong RequestId { get; set; }
        public List<byte[]> Hashes { get; set; } = new();
        public ulong ResponseBytes { get; set; }
    }

    public static class GetByteCodesMessageEncoder
    {
        public static byte[] Encode(GetByteCodesMessage m)
        {
            var hashes = new byte[m.Hashes.Count][];
            for (int i = 0; i < m.Hashes.Count; i++)
                hashes[i] = RLP.RLP.EncodeElement(m.Hashes[i]);

            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(ULongToRlp(m.RequestId)),
                RLP.RLP.EncodeList(hashes),
                RLP.RLP.EncodeElement(ULongToRlp(m.ResponseBytes))
            );
        }

        public static GetByteCodesMessage Decode(byte[] data)
        {
            var o = (RLPCollection)RLP.RLP.Decode(data);
            var m = new GetByteCodesMessage
            {
                RequestId = (ulong)o[0].RLPData.ToLongFromRLPDecoded(),
                ResponseBytes = (ulong)o[2].RLPData.ToLongFromRLPDecoded()
            };
            foreach (var h in (RLPCollection)o[1])
                m.Hashes.Add(h.RLPData);
            return m;
        }

        private static byte[] ULongToRlp(ulong v) => v == 0 ? new byte[0] : ((long)v).ToBytesForRLPEncoding();
    }

    public class ByteCodesMessage
    {
        public ulong RequestId { get; set; }
        public List<byte[]> Codes { get; set; } = new();
    }

    public static class ByteCodesMessageEncoder
    {
        public static byte[] Encode(ByteCodesMessage m)
        {
            var codes = new byte[m.Codes.Count][];
            for (int i = 0; i < m.Codes.Count; i++)
                codes[i] = RLP.RLP.EncodeElement(m.Codes[i]);

            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(ULongToRlp(m.RequestId)),
                RLP.RLP.EncodeList(codes)
            );
        }

        public static ByteCodesMessage Decode(byte[] data)
        {
            var o = (RLPCollection)RLP.RLP.Decode(data);
            var m = new ByteCodesMessage
            {
                RequestId = (ulong)o[0].RLPData.ToLongFromRLPDecoded()
            };
            foreach (var c in (RLPCollection)o[1])
                m.Codes.Add(c.RLPData);
            return m;
        }

        private static byte[] ULongToRlp(ulong v) => v == 0 ? new byte[0] : ((long)v).ToBytesForRLPEncoding();
    }

    // ===== GetTrieNodes / TrieNodes =====
    public class GetTrieNodesMessage
    {
        public ulong RequestId { get; set; }
        public byte[] RootHash { get; set; } = new byte[32];
        public List<List<byte[]>> Paths { get; set; } = new();
        public ulong ResponseBytes { get; set; }
    }

    public static class GetTrieNodesMessageEncoder
    {
        public static byte[] Encode(GetTrieNodesMessage m)
        {
            var pathGroups = new byte[m.Paths.Count][];
            for (int i = 0; i < m.Paths.Count; i++)
            {
                var group = m.Paths[i];
                var groupEncoded = new byte[group.Count][];
                for (int j = 0; j < group.Count; j++)
                    groupEncoded[j] = RLP.RLP.EncodeElement(group[j]);
                pathGroups[i] = RLP.RLP.EncodeList(groupEncoded);
            }

            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(ULongToRlp(m.RequestId)),
                RLP.RLP.EncodeElement(m.RootHash),
                RLP.RLP.EncodeList(pathGroups),
                RLP.RLP.EncodeElement(ULongToRlp(m.ResponseBytes))
            );
        }

        public static GetTrieNodesMessage Decode(byte[] data)
        {
            var o = (RLPCollection)RLP.RLP.Decode(data);
            var m = new GetTrieNodesMessage
            {
                RequestId = (ulong)o[0].RLPData.ToLongFromRLPDecoded(),
                RootHash = o[1].RLPData,
                ResponseBytes = (ulong)o[3].RLPData.ToLongFromRLPDecoded()
            };
            foreach (RLPCollection groupRlp in (RLPCollection)o[2])
            {
                var group = new List<byte[]>();
                foreach (var p in groupRlp)
                    group.Add(p.RLPData);
                m.Paths.Add(group);
            }
            return m;
        }

        private static byte[] ULongToRlp(ulong v) => v == 0 ? new byte[0] : ((long)v).ToBytesForRLPEncoding();
    }

    public class TrieNodesMessage
    {
        public ulong RequestId { get; set; }
        public List<byte[]> Nodes { get; set; } = new();
    }

    public static class TrieNodesMessageEncoder
    {
        public static byte[] Encode(TrieNodesMessage m)
        {
            var nodes = new byte[m.Nodes.Count][];
            for (int i = 0; i < m.Nodes.Count; i++)
                nodes[i] = RLP.RLP.EncodeElement(m.Nodes[i]);

            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(ULongToRlp(m.RequestId)),
                RLP.RLP.EncodeList(nodes)
            );
        }

        public static TrieNodesMessage Decode(byte[] data)
        {
            var o = (RLPCollection)RLP.RLP.Decode(data);
            var m = new TrieNodesMessage
            {
                RequestId = (ulong)o[0].RLPData.ToLongFromRLPDecoded()
            };
            foreach (var n in (RLPCollection)o[1])
                m.Nodes.Add(n.RLPData);
            return m;
        }

        private static byte[] ULongToRlp(ulong v) => v == 0 ? new byte[0] : ((long)v).ToBytesForRLPEncoding();
    }
}
