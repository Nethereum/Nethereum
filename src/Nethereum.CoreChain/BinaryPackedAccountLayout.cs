using System;
using Nethereum.Merkle.Binary.Keys;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.CoreChain
{
    /// <summary>
    /// EIP-7864 binary-trie account layout. Packs the 32-byte Basic Data Leaf
    /// (version + code_size + nonce + balance); code hash is stored out of band
    /// in a separate trie slot (see <see cref="IAccountLayoutStrategy.HasExternalCodeHash"/>)
    /// and is NOT round-tripped through <see cref="EncodeAccount"/> /
    /// <see cref="DecodeAccount"/>.
    ///
    /// Round-trip preserves <see cref="Account.Nonce"/> and
    /// <see cref="Account.Balance"/>. <see cref="Account.StateRoot"/> is not
    /// part of the EIP-7864 model (per-contract storage lives directly in the
    /// global binary trie, not under a per-account sub-trie). <see cref="Account.CodeHash"/>
    /// is intentionally dropped on encode and returned as <c>null</c> on decode —
    /// callers that need it must read the separate code-hash slot.
    ///
    /// <see cref="Account"/> does not carry a code-size field; encode uses
    /// <c>0</c>. Callers that need the binary-trie code-size slot populated
    /// must pack via <see cref="BasicDataLeaf.Pack"/> directly or extend the
    /// Account model.
    /// </summary>
    public class BinaryPackedAccountLayout : IAccountLayoutStrategy
    {
        public const byte DefaultVersion = 0;

        public static BinaryPackedAccountLayout Instance { get; } = new BinaryPackedAccountLayout();

        public bool HasExternalCodeHash => true;

        public byte[] EncodeAccount(Account account)
        {
            if (account == null) throw new ArgumentNullException(nameof(account));

            var nonce = (ulong)account.Nonce;
            var balance = account.Balance;
            const uint codeSize = 0;

            return BasicDataLeaf.Pack(DefaultVersion, codeSize, nonce, balance);
        }

        public Account DecodeAccount(byte[] data)
        {
            if (data == null || data.Length == 0) return null;

            BasicDataLeaf.Unpack(data, out _, out _, out var nonce, out var balance);
            return new Account
            {
                Nonce = nonce,
                Balance = balance,
                StateRoot = null,
                CodeHash = null
            };
        }
    }
}
