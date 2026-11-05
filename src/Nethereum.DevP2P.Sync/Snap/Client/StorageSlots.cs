using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Merkle.Patricia.ProofVerification;
using Nethereum.Model.P2P.Snap;
using static Nethereum.DevP2P.Sync.Snap.Client.SnapSyncClient;

namespace Nethereum.DevP2P.Sync.Snap.Client
{
    internal static class StorageSlots
    {
        internal readonly record struct FlattenedStoragePage(
            List<byte[]> SlotHashes, List<byte[]> ValuesRlp, IList<byte[]> ProofNodes);

        internal static FlattenedStoragePage Flatten(StorageRangesMessage resp)
        {
            var slots = resp.Slots.Count > 0 ? resp.Slots[0] : new List<StorageRangesMessage.SlotEntry>();
            var slotHashes = new List<byte[]>(slots.Count);
            var valuesRlp = new List<byte[]>(slots.Count);
            foreach (var slot in slots)
            {
                slotHashes.Add(slot.Hash);
                valuesRlp.Add(slot.Data);
            }
            var proofNodes = (IList<byte[]>)(resp.Proof ?? new List<byte[]>());
            return new FlattenedStoragePage(slotHashes, valuesRlp, proofNodes);
        }

        internal static RangeProofResult? VerifyChunk(byte[] storageRoot, byte[] startingHash, StorageRangesMessage resp)
        {
            var result = SnapProofVerifier.VerifyStorageSlots(storageRoot, startingHash, resp.Slots[0], resp.Proof);
            return result.Valid ? result : (RangeProofResult?)null;
        }

        internal static async Task WriteAsync(
            IStorageScope scope,
            IReadOnlyList<StorageRangesMessage.SlotEntry> slots,
            AccountWorkerResult page,
            Action markProductive,
            CancellationToken ct)
        {
            foreach (var slot in slots)
            {
                await scope.WriteSlotAsync(slot.Hash, slot.Data, ct).ConfigureAwait(false);
                markProductive();
                page.AddStorageSlot(slot.Data);
            }
        }
    }
}
