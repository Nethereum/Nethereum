using System;
using Nethereum.Merkle.Binary.Keys;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.CoreChain
{
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
