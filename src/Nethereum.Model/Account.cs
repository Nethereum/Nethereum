using System.Numerics;

namespace Nethereum.Model
{
    public class Account
    {

        //TODO: https://github.com/ethereum/EIPs/issues/1186

        /// <summary>
        /// YP: 4.1
        /// A scalar value equal to the number of transactions sent from this address or, in the case
        // of accounts with associated code, the number of
        // contract-creations made by this account
        /// </summary>
        public BigInteger Nonce { get; set; }
        /// <summary>
        /// YP:4.1 A scalar value equal to the number of We owned by this address.
        /// </summary>
        public BigInteger Balance { get; set; }

        /// <summary>
        /// / YP:4.1 A 256-bit hash of the root node of a
        /// Merkle Patricia tree that encodes the storage contents of the account(a mapping between 256-bit
        /// integer values), encoded into the trie as a mapping from the Keccak 256-bit hash of the 256-bit
        /// Integer keys to the RLP-encoded 256-bit integer
        /// values.
        /// </summary>
        public byte[] StateRoot { get; set; } = DefaultValues.EMPTY_TRIE_HASH;
        /// <summary>
        /// The hash of the EVM code of this
        /// account—this is the code that gets executed
        ///    should this address receive a message call; it is
        /// immutable and thus, unlike all other fields, cannot be changed after construction.All such code
        /// fragments are contained in the state database under their corresponding hashes for later retrieval
        /// </summary>
        public byte[] CodeHash { get; set; } = DefaultValues.EMPTY_DATA_HASH;
    }
}