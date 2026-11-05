using System.Collections.Generic;
using System.Linq;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Xunit;
using Nethereum.DevP2P.Sync.Snap.Storage;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class SnapProofVerifierBatchStorageTests
    {
        private static readonly Sha3Keccack Keccak = new();

        private sealed record OwnerStorage(byte[] AccountHash, byte[] StorageRoot, List<StorageRangesMessage.SlotEntry> Slots);

        private static OwnerStorage BuildOwner(byte seed, int slotCount)
        {
            var trie = new PatriciaTrie(new InMemoryContentNodeStore());
            var slots = new List<StorageRangesMessage.SlotEntry>();
            for (int i = 0; i < slotCount; i++)
            {
                var key = Keccak.CalculateHash(new byte[] { seed, (byte)i, 0x6D });
                var value = Nethereum.RLP.RLP.EncodeElement(new byte[] { seed, (byte)(i + 1), 0xE2 });
                trie.Put(key, value);
                slots.Add(new StorageRangesMessage.SlotEntry { Hash = key, Data = value });
            }
            slots.Sort((a, b) => ByteArrayComparer.Current.Compare(a.Hash, b.Hash));
            var accountHash = Keccak.CalculateHash(new byte[] { 0xAD, seed });
            return new OwnerStorage(accountHash, trie.Root.GetHash(), slots);
        }

        private static StorageRangesMessage Reply(params List<StorageRangesMessage.SlotEntry>[] perAccount)
            => new StorageRangesMessage
            {
                RequestId = 0,
                Slots = perAccount.ToList(),
                Proof = new List<byte[]>(),
            };

        [Fact]
        public void Given_ABatchReplyWithNoSlotsForANonEmptyRoot_When_Verified_Then_ItIsRejected()
        {
            var first = BuildOwner(0x11, 6);
            var last = BuildOwner(0x12, 5);
            var dispatched = new List<(byte[] Hash, byte[] Root)>
            {
                (first.AccountHash, first.StorageRoot),
                (last.AccountHash, last.StorageRoot),
            };
            var empty = new List<StorageRangesMessage.SlotEntry>();

            var notLast = SnapProofVerifier.VerifyBatchStorageResponse(dispatched, Reply(empty, last.Slots));
            var lastWithoutProof = SnapProofVerifier.VerifyBatchStorageResponse(dispatched, Reply(first.Slots, empty));
            var complete = SnapProofVerifier.VerifyBatchStorageResponse(dispatched, Reply(first.Slots, last.Slots));

            Assert.False(notLast);
            Assert.False(lastWithoutProof);
            Assert.True(complete);
        }
    }
}
