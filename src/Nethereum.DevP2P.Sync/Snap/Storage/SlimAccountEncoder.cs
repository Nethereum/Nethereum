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
            var reader = new RlpReader(slimAccountBody);
            var scope = reader.ReadListHeader();
            var nonceBytes = reader.ReadBytes();
            var balanceBytes = reader.ReadBytes();
            var storageRoot = reader.ReadBytes();
            var codeHash = reader.ReadBytes();
            reader.EnsureEnd(scope);
            reader.EnsureEnd();
            if (storageRoot.Length == 0) storageRoot = DefaultValues.EMPTY_TRIE_HASH;
            if (codeHash.Length == 0) codeHash = DefaultValues.EMPTY_DATA_HASH;
            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(nonceBytes),
                RLP.RLP.EncodeElement(balanceBytes),
                RLP.RLP.EncodeElement(storageRoot),
                RLP.RLP.EncodeElement(codeHash));
        }
    }
}
