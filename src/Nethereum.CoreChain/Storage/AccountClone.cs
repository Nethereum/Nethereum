using Nethereum.Model;

namespace Nethereum.CoreChain.Storage
{
    internal static class AccountClone
    {
        public static Account Clone(this Account account)
        {
            if (account == null) return null;
            return new Account
            {
                Nonce = account.Nonce,
                Balance = account.Balance,
                StateRoot = (byte[])account.StateRoot?.Clone(),
                CodeHash = (byte[])account.CodeHash?.Clone()
            };
        }
    }
}
