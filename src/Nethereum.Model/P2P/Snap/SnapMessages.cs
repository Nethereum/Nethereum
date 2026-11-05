using System;
using System.Collections.Generic;
using Nethereum.RLP;

namespace Nethereum.Model.P2P.Snap
{
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

        /// <summary>
        /// snap/2 (EIP-8189, "BAL-Based State Healing"). Same message names
        /// as eth/71's EIP-8159 BlockAccessLists (0x08 request / 0x09
        /// response here, vs 0x12/0x13 on eth) — the EIP names these
        /// GetBlockAccessLists/BlockAccessLists, not geth's
        /// GetAccessLists/AccessLists.
        /// </summary>
        public const int GetBlockAccessLists = 0x08;
        public const int BlockAccessLists = 0x09;
    }

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
            var reader = new RlpReader(data);
            var outer = reader.ReadListHeader();
            var message = new GetAccountRangeMessage
            {
                RequestId = reader.ReadUInt64(),
                RootHash = reader.ReadFixed(PeerRlp.HashLength),
                StartingHash = reader.ReadFixed(PeerRlp.HashLength),
                LimitHash = reader.ReadFixed(PeerRlp.HashLength),
                ResponseBytes = reader.ReadUInt64()
            };
            reader.EnsureEnd(outer);
            reader.EnsureEnd();
            return message;
        }

        private static byte[] ULongToRlp(ulong v) => v == 0 ? new byte[0] : ((long)v).ToBytesForRLPEncoding();
    }

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
        private const int SlimAccountItemCount = 4;

        public static byte[] Encode(AccountRangeMessage m)
        {
            var accountElements = new byte[m.Accounts.Count][];
            for (int i = 0; i < m.Accounts.Count; i++)
            {
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
            var reader = new RlpReader(data);
            var outer = reader.ReadListHeader();
            var m = new AccountRangeMessage { RequestId = reader.ReadUInt64() };

            var accounts = reader.ReadListHeader();
            while (!reader.IsAtEnd(accounts))
            {
                var entry = reader.ReadListHeader();
                var hash = reader.ReadFixed(PeerRlp.HashLength);
                m.Accounts.Add(new AccountRangeMessage.AccountEntry { Hash = hash, Body = ReadSlimAccount(reader, data) });
                reader.EnsureEnd(entry);
            }
            reader.EnsureEnd(accounts);

            m.Proof = PeerRlp.ReadByteStrings(reader, SnapServingLimits.MaxResponseProofNodes);
            reader.EnsureEnd(outer);
            reader.EnsureEnd();
            return m;
        }

        private static byte[] ReadSlimAccount(RlpReader reader, byte[] data)
        {
            var start = reader.Position;
            var body = reader.ReadListHeader();
            reader.ReadUInt64();
            var balance = reader.ReadBytes();
            if (balance.Length > 32 || (balance.Length > 0 && balance[0] == 0))
                throw new InvalidOperationException("AccountRange slim account balance is not a canonical uint256");
            for (var i = 2; i < SlimAccountItemCount; i++)
            {
                if (reader.PeekKind() == RlpKind.List)
                    throw new InvalidOperationException("AccountRange slim account holds a list where a string is expected");
                reader.Skip();
            }
            reader.EnsureEnd(body);

            var slim = new byte[reader.Position - start];
            Array.Copy(data, start, slim, 0, slim.Length);
            return slim;
        }

        private static byte[] ULongToRlp(ulong v) => v == 0 ? new byte[0] : ((long)v).ToBytesForRLPEncoding();
    }

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
            var reader = new RlpReader(data);
            var outer = reader.ReadListHeader();
            var m = new GetStorageRangesMessage
            {
                RequestId = reader.ReadUInt64(),
                RootHash = reader.ReadFixed(PeerRlp.HashLength)
            };
            m.AccountHashes.AddRange(PeerRlp.ReadHashes(reader));
            m.StartingHash = reader.ReadBytes();
            m.LimitHash = reader.ReadBytes();
            m.ResponseBytes = reader.ReadUInt64();
            reader.EnsureEnd(outer);
            reader.EnsureEnd();
            return m;
        }

        private static byte[] ULongToRlp(ulong v) => v == 0 ? new byte[0] : ((long)v).ToBytesForRLPEncoding();
    }

    public class StorageRangesMessage
    {
        public ulong RequestId { get; set; }
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
            var reader = new RlpReader(data);
            var outer = reader.ReadListHeader();
            var m = new StorageRangesMessage { RequestId = reader.ReadUInt64() };

            var accounts = reader.ReadListHeader();
            while (!reader.IsAtEnd(accounts))
            {
                var slots = reader.ReadListHeader();
                var slotsForAccount = new List<StorageRangesMessage.SlotEntry>();
                while (!reader.IsAtEnd(slots))
                {
                    var slot = reader.ReadListHeader();
                    slotsForAccount.Add(new StorageRangesMessage.SlotEntry
                    {
                        Hash = reader.ReadFixed(PeerRlp.HashLength),
                        Data = reader.ReadBytes()
                    });
                    reader.EnsureEnd(slot);
                }
                reader.EnsureEnd(slots);
                m.Slots.Add(slotsForAccount);
            }
            reader.EnsureEnd(accounts);

            m.Proof = PeerRlp.ReadByteStrings(reader, SnapServingLimits.MaxResponseProofNodes);
            reader.EnsureEnd(outer);
            reader.EnsureEnd();
            return m;
        }

        private static byte[] ULongToRlp(ulong v) => v == 0 ? new byte[0] : ((long)v).ToBytesForRLPEncoding();
    }

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
            var reader = new RlpReader(data);
            var outer = reader.ReadListHeader();
            var m = new GetByteCodesMessage
            {
                RequestId = reader.ReadUInt64()
            };
            m.Hashes.AddRange(PeerRlp.ReadHashes(reader));
            m.ResponseBytes = reader.ReadUInt64();
            reader.EnsureEnd(outer);
            reader.EnsureEnd();
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
            var reader = new RlpReader(data);
            var outer = reader.ReadListHeader();
            var m = new ByteCodesMessage { RequestId = reader.ReadUInt64() };
            m.Codes = PeerRlp.ReadByteStrings(reader);
            reader.EnsureEnd(outer);
            reader.EnsureEnd();
            return m;
        }

        private static byte[] ULongToRlp(ulong v) => v == 0 ? new byte[0] : ((long)v).ToBytesForRLPEncoding();
    }

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
            var reader = new RlpReader(data);
            var outer = reader.ReadListHeader();
            var m = new GetTrieNodesMessage
            {
                RequestId = reader.ReadUInt64(),
                RootHash = reader.ReadFixed(PeerRlp.HashLength)
            };
            var groups = reader.ReadListHeader();
            var lookups = 0;
            while (!reader.IsAtEnd(groups))
            {
                var groupScope = reader.ReadListHeader();
                if (reader.IsAtEnd(groupScope))
                    throw new InvalidOperationException("GetTrieNodes holds a zero-item pathset");

                var budget = SnapServingLimits.MaxTrieNodeLookups - lookups;
                if (budget <= 0)
                {
                    SkipRest(reader, groupScope);
                    continue;
                }

                var group = new List<byte[]>();
                while (!reader.IsAtEnd(groupScope) && group.Count < budget)
                    group.Add(reader.ReadBytes());
                lookups += group.Count;
                SkipRest(reader, groupScope);
                m.Paths.Add(group);
            }
            reader.EnsureEnd(groups);
            m.ResponseBytes = reader.ReadUInt64();
            reader.EnsureEnd(outer);
            reader.EnsureEnd();
            return m;
        }

        private static void SkipRest(RlpReader reader, RlpListScope scope)
        {
            while (!reader.IsAtEnd(scope))
            {
                if (reader.PeekKind() == RlpKind.List)
                    throw new InvalidOperationException("GetTrieNodes path is not a byte string");
                reader.Skip();
            }
            reader.EnsureEnd(scope);
        }

        private static byte[] ULongToRlp(ulong v) => v == 0 ? new byte[0] : ((long)v).ToBytesForRLPEncoding();
    }

    [Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.CoreFoundation, "model-peer-limits", "SnapServingLimits")]
    public static class SnapServingLimits
    {
        public const int MaxTrieNodeLookups = 1024;

        public const int MaxResponseProofNodes = 128;
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
            var reader = new RlpReader(data);
            var outer = reader.ReadListHeader();
            var m = new TrieNodesMessage { RequestId = reader.ReadUInt64() };
            m.Nodes = PeerRlp.ReadByteStrings(reader);
            reader.EnsureEnd(outer);
            reader.EnsureEnd();
            return m;
        }

        private static byte[] ULongToRlp(ulong v) => v == 0 ? new byte[0] : ((long)v).ToBytesForRLPEncoding();
    }

    // ===== GetBlockAccessLists / BlockAccessLists (snap/2, EIP-8189) =====
    // Same EIP-7928 per-block access lists as eth/71 (EIP-8159), served over snap
    // instead. Message NAMES follow the EIP (GetBlockAccessLists/BlockAccessLists),
    // not geth's GetAccessLists/AccessLists. Type names match eth/71's
    // Nethereum.Model.P2P.BlockAccessListsMessage/BlockAccessListsMessageEncoder —
    // the two coexist because they live in different namespaces (P2P vs P2P.Snap);
    // a file that imports both must fully-qualify one of them.
    public class GetBlockAccessListsMessage
    {
        public ulong RequestId { get; set; }
        public List<byte[]> Hashes { get; set; } = new();

        public ulong Bytes { get; set; }
    }

    public static class GetBlockAccessListsMessageEncoder
    {
        public static byte[] Encode(GetBlockAccessListsMessage m)
        {
            var hashes = new byte[m.Hashes.Count][];
            for (int i = 0; i < m.Hashes.Count; i++)
                hashes[i] = RLP.RLP.EncodeElement(m.Hashes[i]);

            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(ULongToRlp(m.RequestId)),
                RLP.RLP.EncodeList(hashes),
                RLP.RLP.EncodeElement(ULongToRlp(m.Bytes))
            );
        }

        public static GetBlockAccessListsMessage Decode(byte[] data)
        {
            var reader = new RlpReader(data);
            var outer = reader.ReadListHeader();
            var m = new GetBlockAccessListsMessage
            {
                RequestId = reader.ReadUInt64()
            };
            m.Hashes.AddRange(PeerRlp.ReadHashes(reader));
            m.Bytes = reader.ReadUInt64();
            reader.EnsureEnd(outer);
            reader.EnsureEnd();
            return m;
        }

        private static byte[] ULongToRlp(ulong v) => v == 0 ? new byte[0] : ((long)v).ToBytesForRLPEncoding();
    }

    public class BlockAccessListsMessage
    {
        public ulong RequestId { get; set; }
        public List<byte[]> BlockAccessListsByBlock { get; set; } = new();
    }

    public static class BlockAccessListsMessageEncoder
    {
        public static byte[] Encode(BlockAccessListsMessage msg)
        {
            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(((long)msg.RequestId).ToBytesForRLPEncoding()),
                RLP.RLP.EncodeList(
                    Nethereum.Model.P2P.BlockAccessListWireEntries.ForWire(msg.BlockAccessListsByBlock))
            );
        }

        public static BlockAccessListsMessage Decode(byte[] data)
        {
            var reader = new RlpReader(data);
            var outer = reader.ReadListHeader();
            var msg = new BlockAccessListsMessage { RequestId = reader.ReadUInt64() };
            msg.BlockAccessListsByBlock = PeerRlp.ReadBlockAccessLists(reader);
            reader.EnsureEnd(outer);
            reader.EnsureEnd();
            return msg;
        }
    }
}
