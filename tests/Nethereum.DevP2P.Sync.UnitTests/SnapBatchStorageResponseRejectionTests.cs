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
    public class SnapBatchStorageResponseRejectionTests
    {
        private static readonly Sha3Keccack Keccak = new();

        private sealed record OwnerStorage(byte[] AccountHash, byte[] StorageRoot, List<StorageRangesMessage.SlotEntry> Slots);

        private static OwnerStorage BuildOwner(byte seed, int slotCount)
        {
            var trie = new PatriciaTrie(new InMemoryContentNodeStore());
            var slots = new List<StorageRangesMessage.SlotEntry>();
            for (int i = 0; i < slotCount; i++)
            {
                var key = Keccak.CalculateHash(new byte[] { seed, (byte)i, 0x5C });
                var value = Nethereum.RLP.RLP.EncodeElement(new byte[] { seed, (byte)(i + 1), 0xE1 });
                trie.Put(key, value);
                slots.Add(new StorageRangesMessage.SlotEntry { Hash = key, Data = value });
            }
            slots.Sort((a, b) => ByteArrayComparer.Current.Compare(a.Hash, b.Hash));
            var accountHash = Keccak.CalculateHash(new byte[] { 0xAC, seed });
            return new OwnerStorage(accountHash, trie.Root.GetHash(), slots);
        }

        private static List<StorageRangesMessage.SlotEntry> WithTamperedFirstValue(List<StorageRangesMessage.SlotEntry> slots)
        {
            var tampered = slots.Select(s => new StorageRangesMessage.SlotEntry { Hash = s.Hash, Data = s.Data }).ToList();
            tampered[0] = new StorageRangesMessage.SlotEntry
            {
                Hash = tampered[0].Hash,
                Data = Nethereum.RLP.RLP.EncodeElement(new byte[] { 0xBA, 0xD0 }),
            };
            return tampered;
        }

        private static StorageRangesMessage Response(params List<StorageRangesMessage.SlotEntry>[] perAccount)
            => new StorageRangesMessage
            {
                RequestId = 0,
                Slots = perAccount.ToList(),
                Proof = new List<byte[]>(),
            };

        [Fact]
        public void Given_ABatchResponseWhoseUntruncatedAccountIsEmptyOrRootMismatched_When_Verified_Then_TheVerifierRejectsIt()
        {
            var first = BuildOwner(0x01, 5);
            var last = BuildOwner(0x02, 4);
            var dispatched = new List<(byte[] Hash, byte[] Root)>
            {
                (first.AccountHash, first.StorageRoot),
                (last.AccountHash, last.StorageRoot),
            };
            var empty = new List<StorageRangesMessage.SlotEntry>();

            Assert.False(SnapProofVerifier.VerifyBatchStorageResponse(dispatched, Response(empty, last.Slots)),
                "a non-last account with a non-empty root and no slots must be rejected (D1 NonEmptyRootReturnedNoSlots shape)");
            Assert.False(SnapProofVerifier.VerifyBatchStorageResponse(dispatched, Response(WithTamperedFirstValue(first.Slots), last.Slots)),
                "a non-last account whose slots do not rebuild its root must be rejected (D2 StorageRootVerifyMismatch shape)");
            Assert.False(SnapProofVerifier.VerifyBatchStorageResponse(dispatched, Response(first.Slots, empty)),
                "an unproven last account with a non-empty root and no slots must be rejected (D1 shape)");
            Assert.False(SnapProofVerifier.VerifyBatchStorageResponse(dispatched, Response(first.Slots, WithTamperedFirstValue(last.Slots))),
                "an unproven last account whose slots do not rebuild its root must be rejected (D2 shape)");
            Assert.True(SnapProofVerifier.VerifyBatchStorageResponse(dispatched, Response(first.Slots, last.Slots)),
                "the honest complete batch must pass");
        }
    }
}
