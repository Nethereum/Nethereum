using Nethereum.Model;
using Nethereum.Util.HashProviders;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;
using System.Collections.Generic;
using System.Linq;

using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Storage;
namespace Nethereum.Merkle.Patricia.ProofVerification
{
    public interface IAccountProofVerifier
    {
        bool Verify(byte[] stateRoot, IEnumerable<byte[]> proof, string accountAddress, Account account);
    }

    public class AccountProofVerification : IAccountProofVerifier
    {
        public static AccountProofVerification Current { get; } = new AccountProofVerification();

        public bool Verify(byte[] stateRoot, IEnumerable<byte[]> proof, string accountAddress, Account account)
        {
            if (stateRoot == null || accountAddress == null || account == null) return false;

            var addressBytes = accountAddress.HexToByteArray();
            if (addressBytes.Length != 20) return false;

            var key = Sha3KeccackHashProvider.Instance.ComputeHash(addressBytes);
            var proofNodes = proof?.ToList() ?? new List<byte[]>();

            if (!PatriciaProofVerifier.TryVerify(stateRoot, key, proofNodes, out var value)) return false;

            if (value == null)
            {
                return IsAbsentAccount(account);
            }

            return AccountEncoder.Current.Encode(account).AreTheSame(value);
        }

        private static bool IsAbsentAccount(Account account)
            => account.Nonce.IsZero
               && account.Balance.IsZero
               && IsEmptyOrZeroHash(account.CodeHash, DefaultValues.EMPTY_DATA_HASH)
               && IsEmptyOrZeroHash(account.StateRoot, DefaultValues.EMPTY_TRIE_HASH);

        private static bool IsEmptyOrZeroHash(byte[] hash, byte[] emptyHash)
            => hash != null && hash.Length == 32 && (hash.AreTheSame(emptyHash) || hash.All(b => b == 0));
    }
}
