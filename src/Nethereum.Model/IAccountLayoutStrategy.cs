namespace Nethereum.Model
{
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
