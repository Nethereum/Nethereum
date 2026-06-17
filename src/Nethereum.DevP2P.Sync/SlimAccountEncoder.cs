using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Util;

namespace Nethereum.DevP2P.Sync
{
    /// <summary>
    /// snap/1 response account body format — the "slim" form.
    /// Identical to the canonical state-trie account RLP except that
    ///   - StorageRoot equal to EMPTY_TRIE_HASH is emitted as an empty byte string
    ///   - CodeHash    equal to EMPTY_DATA_HASH is emitted as an empty byte string
    /// This saves 64 bytes per externally-owned account in AccountRange responses
    /// (the dominant account type), which is what go-ethereum's snap server
    /// emits and what its budget arithmetic assumes — without this, our 4000-byte
    /// AccountRange responses fit ~2.2x fewer accounts than Geth expects.
    ///
    /// Spec: https://github.com/ethereum/devp2p/blob/master/caps/snap.md
    /// Reference: go-ethereum core/state/snapshot.slimAccount
    /// </summary>
    public static class SlimAccountEncoder
    {
        /// <summary>
        /// Re-encode a canonical state-trie account body in slim form.
        /// Round-trips identically for any account where the hashes are
        /// non-default, so callers can apply unconditionally.
        /// </summary>
        public static byte[] ToSlim(byte[] canonicalAccountBody)
        {
            var account = new AccountEncoder().Decode(canonicalAccountBody);
            var storageRoot = ByteUtil.AreEqual(account.StateRoot, DefaultValues.EMPTY_TRIE_HASH)
                ? new byte[0]
                : account.StateRoot;
            var codeHash = ByteUtil.AreEqual(account.CodeHash, DefaultValues.EMPTY_DATA_HASH)
                ? new byte[0]
                : account.CodeHash;
            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(account.Nonce.ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(account.Balance.ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(storageRoot),
                RLP.RLP.EncodeElement(codeHash));
        }

        /// <summary>
        /// Inverse of <see cref="ToSlim"/>: rebuild the canonical state-trie
        /// account body from a slim one received over snap/1, substituting
        /// EMPTY_TRIE_HASH / EMPTY_DATA_HASH for the empty-byte stand-ins.
        /// Snap-sync clients must call this before inserting into a local
        /// trie or the rebuilt state root will diverge from the source.
        /// </summary>
        public static byte[] FromSlim(byte[] slimAccountBody)
        {
            var items = (Nethereum.RLP.RLPCollection)Nethereum.RLP.RLP.Decode(slimAccountBody);
            var nonceBytes = items[0].RLPData ?? new byte[0];
            var balanceBytes = items[1].RLPData ?? new byte[0];
            var storageRoot = items[2].RLPData;
            var codeHash = items[3].RLPData;
            if (storageRoot == null || storageRoot.Length == 0) storageRoot = DefaultValues.EMPTY_TRIE_HASH;
            if (codeHash == null || codeHash.Length == 0) codeHash = DefaultValues.EMPTY_DATA_HASH;
            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(nonceBytes),
                RLP.RLP.EncodeElement(balanceBytes),
                RLP.RLP.EncodeElement(storageRoot),
                RLP.RLP.EncodeElement(codeHash));
        }
    }
}
