using System.Collections.Generic;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model.P2P.Snap;
using Xunit;

namespace Nethereum.DevP2P.SpecTests.Snap
{
    public class SnapMessageRoundTripTests
    {
        [Fact]
        public void GetAccountRange_RoundTrip()
        {
            var m = new GetAccountRangeMessage
            {
                RequestId = 1,
                RootHash = Make32(0xAA),
                StartingHash = Make32(0xBB),
                LimitHash = Make32(0xCC),
                ResponseBytes = 1024
            };
            var b = GetAccountRangeMessageEncoder.Encode(m);
            var d = GetAccountRangeMessageEncoder.Decode(b);

            Assert.Equal(m.RequestId, d.RequestId);
            Assert.Equal(m.RootHash.ToHex(), d.RootHash.ToHex());
            Assert.Equal(m.StartingHash.ToHex(), d.StartingHash.ToHex());
            Assert.Equal(m.LimitHash.ToHex(), d.LimitHash.ToHex());
            Assert.Equal(m.ResponseBytes, d.ResponseBytes);
        }

        [Fact]
        public void AccountRange_RoundTrip_WithAccountsAndProof()
        {
            // Body is rlp.RawValue per go-ethereum's AccountData → must already
            // be a valid RLP-encoded slim account list. Construct two minimal
            // slim accounts: rlp([nonce, balance, storageRoot, codeHash]) where
            // both hash fields are empty (EOA).
            var slimA = Nethereum.RLP.RLP.EncodeList(
                Nethereum.RLP.RLP.EncodeElement(new byte[] { 0x07 }),
                Nethereum.RLP.RLP.EncodeElement(new byte[] { 0x42 }),
                Nethereum.RLP.RLP.EncodeElement(new byte[0]),
                Nethereum.RLP.RLP.EncodeElement(new byte[0]));
            var slimB = Nethereum.RLP.RLP.EncodeList(
                Nethereum.RLP.RLP.EncodeElement(new byte[0]),
                Nethereum.RLP.RLP.EncodeElement(new byte[] { 0x99 }),
                Nethereum.RLP.RLP.EncodeElement(new byte[0]),
                Nethereum.RLP.RLP.EncodeElement(new byte[0]));

            var m = new AccountRangeMessage
            {
                RequestId = 7,
                Accounts =
                {
                    new() { Hash = Make32(0x11), Body = slimA },
                    new() { Hash = Make32(0x22), Body = slimB }
                },
                Proof = new List<byte[]>
                {
                    new byte[] { 0x01, 0x02, 0x03 },
                    new byte[] { 0x04, 0x05 }
                }
            };
            var b = AccountRangeMessageEncoder.Encode(m);
            var d = AccountRangeMessageEncoder.Decode(b);

            Assert.Equal(2, d.Accounts.Count);
            Assert.Equal(m.Accounts[0].Hash.ToHex(), d.Accounts[0].Hash.ToHex());
            Assert.Equal(slimA.ToHex(), d.Accounts[0].Body.ToHex());
            Assert.Equal(slimB.ToHex(), d.Accounts[1].Body.ToHex());
            Assert.Equal(2, d.Proof.Count);
            Assert.Equal(m.Proof[0].ToHex(), d.Proof[0].ToHex());
        }

        [Fact]
        public void GetStorageRanges_RoundTrip()
        {
            var m = new GetStorageRangesMessage
            {
                RequestId = 99,
                RootHash = Make32(0x11),
                AccountHashes = { Make32(0x33), Make32(0x44) },
                StartingHash = new byte[] { 0x01, 0x02 },
                LimitHash = new byte[] { 0xFF },
                ResponseBytes = 4096
            };
            var b = GetStorageRangesMessageEncoder.Encode(m);
            var d = GetStorageRangesMessageEncoder.Decode(b);

            Assert.Equal(m.RequestId, d.RequestId);
            Assert.Equal(m.RootHash.ToHex(), d.RootHash.ToHex());
            Assert.Equal(2, d.AccountHashes.Count);
            Assert.Equal(m.AccountHashes[1].ToHex(), d.AccountHashes[1].ToHex());
            Assert.Equal(m.StartingHash.ToHex(), d.StartingHash.ToHex());
            Assert.Equal(m.LimitHash.ToHex(), d.LimitHash.ToHex());
            Assert.Equal(m.ResponseBytes, d.ResponseBytes);
        }

        [Fact]
        public void StorageRanges_RoundTrip_NestedSlotsAndProof()
        {
            var m = new StorageRangesMessage
            {
                RequestId = 42,
                Slots = new List<List<StorageRangesMessage.SlotEntry>>
                {
                    new()
                    {
                        new() { Hash = Make32(0x01), Data = new byte[] { 0xAA } },
                        new() { Hash = Make32(0x02), Data = new byte[] { 0xBB, 0xCC } }
                    },
                    new()
                    {
                        new() { Hash = Make32(0x03), Data = new byte[] { 0xDD, 0xEE, 0xFF } }
                    }
                },
                Proof = new List<byte[]> { new byte[] { 0x10, 0x20 }, new byte[] { 0x30 } }
            };
            var b = StorageRangesMessageEncoder.Encode(m);
            var d = StorageRangesMessageEncoder.Decode(b);

            Assert.Equal(m.RequestId, d.RequestId);
            Assert.Equal(2, d.Slots.Count);
            Assert.Equal(2, d.Slots[0].Count);
            Assert.Equal(1, d.Slots[1].Count);
            Assert.Equal(m.Slots[0][1].Data.ToHex(), d.Slots[0][1].Data.ToHex());
            Assert.Equal(m.Slots[1][0].Hash.ToHex(), d.Slots[1][0].Hash.ToHex());
            Assert.Equal(2, d.Proof.Count);
            Assert.Equal(m.Proof[0].ToHex(), d.Proof[0].ToHex());
        }

        [Fact]
        public void GetByteCodes_RoundTrip()
        {
            var m = new GetByteCodesMessage
            {
                RequestId = 42,
                Hashes = { Make32(0x77), Make32(0x88), Make32(0x99) },
                ResponseBytes = 65536
            };
            var b = GetByteCodesMessageEncoder.Encode(m);
            var d = GetByteCodesMessageEncoder.Decode(b);

            Assert.Equal(m.RequestId, d.RequestId);
            Assert.Equal(3, d.Hashes.Count);
            Assert.Equal(m.Hashes[2].ToHex(), d.Hashes[2].ToHex());
            Assert.Equal(m.ResponseBytes, d.ResponseBytes);
        }

        [Fact]
        public void ByteCodes_RoundTrip()
        {
            var m = new ByteCodesMessage
            {
                RequestId = 5,
                Codes = { new byte[] { 0x60, 0x80, 0x60, 0x40 }, new byte[] { 0xFE } }
            };
            var b = ByteCodesMessageEncoder.Encode(m);
            var d = ByteCodesMessageEncoder.Decode(b);

            Assert.Equal(m.RequestId, d.RequestId);
            Assert.Equal(2, d.Codes.Count);
            Assert.Equal(m.Codes[0].ToHex(), d.Codes[0].ToHex());
            Assert.Equal(m.Codes[1].ToHex(), d.Codes[1].ToHex());
        }

        [Fact]
        public void GetTrieNodes_RoundTrip_PreservesNestedPaths()
        {
            var m = new GetTrieNodesMessage
            {
                RequestId = 10,
                RootHash = Make32(0xFE),
                Paths = new List<List<byte[]>>
                {
                    new() { new byte[] { 0x01 }, new byte[] { 0x02 } },
                    new() { new byte[] { 0x03 } }
                },
                ResponseBytes = 2048
            };
            var b = GetTrieNodesMessageEncoder.Encode(m);
            var d = GetTrieNodesMessageEncoder.Decode(b);

            Assert.Equal(m.RequestId, d.RequestId);
            Assert.Equal(m.RootHash.ToHex(), d.RootHash.ToHex());
            Assert.Equal(2, d.Paths.Count);
            Assert.Equal(2, d.Paths[0].Count);
            Assert.Equal(1, d.Paths[1].Count);
            Assert.Equal(m.Paths[0][0].ToHex(), d.Paths[0][0].ToHex());
        }

        [Fact]
        public void TrieNodes_RoundTrip()
        {
            var m = new TrieNodesMessage
            {
                RequestId = 11,
                Nodes = { new byte[] { 0xAA, 0xBB }, new byte[] { 0xCC, 0xDD, 0xEE } }
            };
            var b = TrieNodesMessageEncoder.Encode(m);
            var d = TrieNodesMessageEncoder.Decode(b);

            Assert.Equal(m.RequestId, d.RequestId);
            Assert.Equal(2, d.Nodes.Count);
            Assert.Equal(m.Nodes[0].ToHex(), d.Nodes[0].ToHex());
        }

        [Fact]
        public void MessageIds_MatchSpec()
        {
            Assert.Equal(0x00, SnapMessageIds.GetAccountRange);
            Assert.Equal(0x01, SnapMessageIds.AccountRange);
            Assert.Equal(0x02, SnapMessageIds.GetStorageRanges);
            Assert.Equal(0x03, SnapMessageIds.StorageRanges);
            Assert.Equal(0x04, SnapMessageIds.GetByteCodes);
            Assert.Equal(0x05, SnapMessageIds.ByteCodes);
            Assert.Equal(0x06, SnapMessageIds.GetTrieNodes);
            Assert.Equal(0x07, SnapMessageIds.TrieNodes);
        }

        private static byte[] Make32(byte fill)
        {
            var b = new byte[32];
            for (int i = 0; i < 32; i++) b[i] = (byte)(fill ^ i);
            return b;
        }
    }
}
