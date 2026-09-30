using System;
using System.Collections.Generic;
using Nethereum.Merkle.Patricia.ProofVerification;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;

namespace Nethereum.DevP2P.Sync.Snap.Storage
{
    internal static class SnapProofVerifier
    {
        internal static Nethereum.Merkle.Patricia.ProofVerification.RangeProofResult VerifyAccountRangeResponse(
            byte[] stateRoot, byte[] origin, AccountRangeMessage resp, out List<byte[]> canonicalValues)
        {
            var proof = (IList<byte[]>)(resp?.Proof ?? new List<byte[]>());
            if (resp?.Accounts == null || resp.Accounts.Count == 0)
            {
                canonicalValues = new List<byte[]>();
                return ProofVerification.Current.Range.Verify(
                    stateRoot, origin, Array.Empty<byte[]>(), Array.Empty<byte[]>(), proof);
            }

            var keys = new List<byte[]>(resp.Accounts.Count);
            canonicalValues = new List<byte[]>(resp.Accounts.Count);
            foreach (var entry in resp.Accounts)
            {
                keys.Add(entry.Hash);
                canonicalValues.Add(SlimAccountEncoder.FromSlim(entry.Body));
            }
            return ProofVerification.Current.Range.Verify(stateRoot, origin, keys, canonicalValues, proof);
        }

        internal static bool VerifyStorageRangeSlots(
            byte[] storageRoot, byte[] startingHash,
            IList<StorageRangesMessage.SlotEntry> slots, IList<byte[]> proof)
        {
            proof ??= new List<byte[]>();
            if (slots == null || slots.Count == 0)
            {
                return ProofVerification.Current.Range.Verify(
                    storageRoot, startingHash, Array.Empty<byte[]>(), Array.Empty<byte[]>(), proof).Valid;
            }
            var keys = new List<byte[]>(slots.Count);
            var values = new List<byte[]>(slots.Count);
            foreach (var slot in slots)
            {
                keys.Add(slot.Hash);
                values.Add(slot.Data);
            }
            return ProofVerification.Current.Range.Verify(
                storageRoot, startingHash, keys, values, proof).Valid;
        }

        internal static bool VerifyStorageRangeResponse(byte[] storageRoot, byte[] startingHash, StorageRangesMessage resp)
        {
            var slots = resp?.Slots != null && resp.Slots.Count > 0 ? resp.Slots[0] : null;
            var proof = (IList<byte[]>)(resp?.Proof ?? new List<byte[]>());
            return VerifyStorageRangeSlots(storageRoot, startingHash, slots, proof);
        }

        internal static bool VerifyBatchStorageResponse(IReadOnlyList<(byte[] Hash, byte[] Root)> dispatched, StorageRangesMessage resp)
        {
            var slotSets = resp?.Slots;
            var proof = (IList<byte[]>)(resp?.Proof ?? new List<byte[]>());
            if (dispatched == null || dispatched.Count == 0) return true;

            if (slotSets == null || slotSets.Count == 0)
            {
                if (proof.Count == 0) return true;
                return VerifyStorageRangeSlots(dispatched[0].Root, new byte[32], null, proof);
            }

            if (slotSets.Count > dispatched.Count) return false;

            int last = slotSets.Count - 1;
            for (int i = 0; i <= last; i++)
            {
                var accountProof = i == last ? proof : (IList<byte[]>)new List<byte[]>();
                if (!VerifyStorageRangeSlots(dispatched[i].Root, new byte[32], slotSets[i], accountProof))
                    return false;
            }
            return true;
        }

        internal static bool VerifyByteCodesResponse(IReadOnlyList<byte[]> requestedHashes, ByteCodesMessage resp)
        {
            if (resp?.Codes == null || resp.Codes.Count == 0) return true;
            if (requestedHashes == null || requestedHashes.Count == 0) return false;
            var keccak = Sha3Keccack.Current;
            var wanted = new Dictionary<byte[], int>(ByteArrayComparer.Current);
            foreach (var h in requestedHashes)
                wanted[h] = wanted.TryGetValue(h, out var c) ? c + 1 : 1;
            foreach (var code in resp.Codes)
            {
                if (code == null || code.Length == 0) continue;
                var hash = keccak.CalculateHash(code);
                if (wanted.TryGetValue(hash, out var remaining) && remaining > 0)
                    wanted[hash] = remaining - 1;
                else
                    return false;
            }
            return true;
        }
    }
}
