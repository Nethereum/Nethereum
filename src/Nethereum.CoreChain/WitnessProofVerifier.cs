using System.Collections.Generic;
using Nethereum.EVM.Witness;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Util;
using Nethereum.Util.HashProviders;

namespace Nethereum.CoreChain
{
    /// <summary>
    /// Verifies Merkle proofs in the witness against the pre-state root.
    /// Ensures the witness is not forged — every claimed pre-state value
    /// must be provably anchored to the pre-state root.
    /// </summary>
    public static class WitnessProofVerifier
    {
        private static readonly byte[] EMPTY_TRIE_ROOT = "56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421".HexToByteArray();
        private static readonly byte[] EMPTY_CODE_HASH = Sha3Keccack.Current.CalculateHash(new byte[0]);
        private static readonly Sha3KeccackHashProvider HashProvider = new Sha3KeccackHashProvider();

        public static WitnessVerificationResult VerifyAll(byte[] preStateRoot, List<WitnessAccount> accounts)
        {
            if (preStateRoot == null || preStateRoot.Length != 32)
                return WitnessVerificationResult.Fail("Missing or invalid PreStateRoot");

            if (accounts == null || accounts.Count == 0)
                return WitnessVerificationResult.Ok();

            foreach (var acc in accounts)
            {
                if (acc.AccountProof == null || acc.AccountProof.Length == 0)
                    return WitnessVerificationResult.Fail($"Account {acc.Address}: missing proof");

                var result = VerifyAccountProof(preStateRoot, acc);
                if (!result.IsValid)
                    return result;

                // Verify storage proofs against account's storage root
                if (acc.StorageProofs != null && acc.StorageProofs.Count > 0)
                {
                    var storageRoot = acc.StorageRoot ?? EMPTY_TRIE_ROOT;
                    foreach (var sp in acc.StorageProofs)
                    {
                        var storageResult = VerifyStorageProof(storageRoot, sp);
                        if (!storageResult.IsValid)
                            return storageResult;
                    }
                }
            }

            return WitnessVerificationResult.Ok();
        }

        private static WitnessVerificationResult VerifyAccountProof(byte[] preStateRoot, WitnessAccount acc)
        {
            // Load proof nodes
            var proofStorage = new InMemoryTrieStorage();
            foreach (var node in acc.AccountProof)
            {
                if (node != null && node.Length > 0)
                    proofStorage.Put(HashProvider.ComputeHash(node), node);
            }

            // Build expected account RLP
            var nonceBytes = acc.Nonce == 0 ? new byte[0] : TrimLeadingZeros(new EvmUInt256(acc.Nonce).ToBigEndian());
            var balanceBytes = acc.Balance.IsZero ? new byte[0] : TrimLeadingZeros(acc.Balance.ToBigEndian());
            var storageRoot = acc.StorageRoot ?? EMPTY_TRIE_ROOT;
            var codeHash = acc.Code != null && acc.Code.Length > 0
                ? Sha3Keccack.Current.CalculateHash(acc.Code)
                : EMPTY_CODE_HASH;

            var expectedRlp = RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(nonceBytes),
                RLP.RLP.EncodeElement(balanceBytes),
                RLP.RLP.EncodeElement(storageRoot),
                RLP.RLP.EncodeElement(codeHash));

            // Verify against pre-state root
            var trie = new PatriciaTrie(preStateRoot);
            var addressKey = Sha3Keccack.Current.CalculateHash(
                AddressUtil.Current.ConvertToValid20ByteAddress(acc.Address).HexToByteArray());

            var value = trie.Get(addressKey, proofStorage);

            if (value == null || value.Length == 0)
            {
                // Account not found — this is an exclusion proof (new account)
                // Verify the trie root is consistent with the proof
                if (!trie.Root.GetHash().AreTheSame(preStateRoot))
                    return WitnessVerificationResult.Fail($"Account {acc.Address}: exclusion proof invalid — root mismatch");

                // For exclusion: account must be empty in the witness
                if (acc.Balance.IsZero && acc.Nonce == 0 &&
                    (acc.Code == null || acc.Code.Length == 0))
                    return WitnessVerificationResult.Ok();

                return WitnessVerificationResult.Fail($"Account {acc.Address}: claimed non-empty but proof shows exclusion");
            }

            // Inclusion proof — verify value matches
            if (!value.AreTheSame(expectedRlp))
                return WitnessVerificationResult.Fail($"Account {acc.Address}: proof value mismatch");

            return WitnessVerificationResult.Ok();
        }

        private static WitnessVerificationResult VerifyStorageProof(byte[] storageRoot, WitnessStorageProof sp)
        {
            if (sp.Proof == null || sp.Proof.Length == 0)
                return WitnessVerificationResult.Fail($"Storage slot {sp.Key}: missing proof");

            var proofStorage = new InMemoryTrieStorage();
            foreach (var node in sp.Proof)
            {
                if (node != null && node.Length > 0)
                    proofStorage.Put(HashProvider.ComputeHash(node), node);
            }

            var trie = new PatriciaTrie(storageRoot);
            var slotKey = Sha3Keccack.Current.CalculateHash(sp.Key.ToBigEndian());
            var value = trie.Get(slotKey, proofStorage);

            var expectedRlp = sp.Value.IsZero
                ? null
                : RLP.RLP.EncodeElement(TrimLeadingZeros(sp.Value.ToBigEndian()));

            if (expectedRlp == null && (value == null || value.Length == 0))
                return WitnessVerificationResult.Ok(); // Both empty

            if (expectedRlp != null && value != null && value.AreTheSame(expectedRlp))
                return WitnessVerificationResult.Ok(); // Values match

            return WitnessVerificationResult.Fail($"Storage slot {sp.Key}: proof value mismatch");
        }

        private static byte[] TrimLeadingZeros(byte[] bytes)
        {
            int start = 0;
            while (start < bytes.Length && bytes[start] == 0) start++;
            if (start == bytes.Length) return new byte[0];
            if (start == 0) return bytes;
            var result = new byte[bytes.Length - start];
            System.Array.Copy(bytes, start, result, 0, result.Length);
            return result;
        }
    }

    public class WitnessVerificationResult
    {
        public bool IsValid { get; set; }
        public string Error { get; set; }

        public static WitnessVerificationResult Ok() => new WitnessVerificationResult { IsValid = true };
        public static WitnessVerificationResult Fail(string error) => new WitnessVerificationResult { IsValid = false, Error = error };
    }
}
