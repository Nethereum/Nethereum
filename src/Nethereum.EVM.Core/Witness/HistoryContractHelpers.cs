using System.Collections.Generic;
using Nethereum.Util;

namespace Nethereum.EVM.Witness
{
    /// <summary>
    /// Helpers for populating the EIP-2935 history contract (address
    /// <c>0x0000F90827F1C53a10cb7A02335B175320002935</c>, storage slot =
    /// blockNumber % 8191) so BLOCKHASH reads resolve during witness-driven
    /// execution. Use when building a witness from block-hash metadata that is
    /// not already stored as system-contract state.
    /// </summary>
    public static class HistoryContractHelpers
    {
        public const string HISTORY_STORAGE_ADDRESS = "0x0000F90827F1C53a10cb7A02335B175320002935";
        public const int HISTORY_SERVE_WINDOW = 8191;

        /// <summary>
        /// Merge a (blockNumber → hash) map into the history contract's storage
        /// section of the given witness account list. Creates the account if it
        /// isn't there; collision on (blockNumber % 8191) overwrites.
        /// </summary>
        public static void PopulateFromBlockHashes(
            List<WitnessAccount> accounts,
            IDictionary<long, byte[]> blockHashes)
        {
            if (accounts == null || blockHashes == null || blockHashes.Count == 0) return;

            WitnessAccount hist = null;
            foreach (var a in accounts)
            {
                if (string.Equals(a.Address, HISTORY_STORAGE_ADDRESS, System.StringComparison.OrdinalIgnoreCase))
                { hist = a; break; }
            }
            if (hist == null)
            {
                hist = new WitnessAccount
                {
                    Address = HISTORY_STORAGE_ADDRESS,
                    Balance = EvmUInt256.Zero,
                    Nonce = 0,
                    Code = new byte[0],
                    Storage = new List<WitnessStorageSlot>()
                };
                accounts.Add(hist);
            }
            if (hist.Storage == null) hist.Storage = new List<WitnessStorageSlot>();

            foreach (var kv in blockHashes)
            {
                if (kv.Value == null) continue;
                var slot = new EvmUInt256((ulong)(kv.Key % HISTORY_SERVE_WINDOW));
                var value = EvmUInt256.FromBigEndian(kv.Value);
                // Replace existing slot if present
                int existing = -1;
                for (int i = 0; i < hist.Storage.Count; i++)
                {
                    if (hist.Storage[i].Key.Equals(slot)) { existing = i; break; }
                }
                var entry = new WitnessStorageSlot { Key = slot, Value = value };
                if (existing >= 0) hist.Storage[existing] = entry;
                else hist.Storage.Add(entry);
            }
        }
    }
}
