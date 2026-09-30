using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Util;

namespace Nethereum.DevP2P.Sync.Snap.Storage
{
    public static class SlimAccountEncoder
    {
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
