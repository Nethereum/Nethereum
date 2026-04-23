namespace Nethereum.Model
{
    /// <summary>
    /// How an <see cref="Account"/> is laid out in the state store. Two
    /// shapes ship today:
    ///
    ///   <see cref="RlpAccountLayout"/> — mainnet MPT shape. The full
    ///   Account (nonce, balance, storageRoot, codeHash) is RLP-encoded into
    ///   a single blob keyed by the account address (or its hash). No
    ///   side-channel.
    ///
    ///   <c>BinaryPackedAccountLayout</c> (EIP-7864, in
    ///   <see cref="N:Nethereum.Merkle.Binary"/>) — binary-trie shape. The
    ///   32-byte Basic Data Leaf packs version + code_size + nonce +
    ///   balance. Code hash and per-storage slots live in separate trie
    ///   keys; the state store must write those under a different sub-index
    ///   when <see cref="HasExternalCodeHash"/> is <c>true</c>.
    ///
    /// An AppChain picks one at genesis via
    /// <see cref="Nethereum.AppChain.AppChainFork"/>. The state store reads
    /// <see cref="HasExternalCodeHash"/> to decide whether to perform the
    /// extra write/read for the code-hash slot.
    /// </summary>
    public interface IAccountLayoutStrategy
    {
        byte[] EncodeAccount(Account account);
        Account DecodeAccount(byte[] data);

        /// <summary>
        /// <c>true</c> when the encoded blob does NOT contain the account's
        /// code hash and the state store must persist it in a separate slot.
        /// Matches EIP-7864's binary-trie Basic Data Leaf semantics.
        /// </summary>
        bool HasExternalCodeHash { get; }
    }
}
