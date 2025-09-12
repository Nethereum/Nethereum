using System.Numerics;
using Nethereum.Model;

namespace Nethereum.Wallet.Services.Transaction
{
    public static class TransactionConstants
    {
        public static readonly BigInteger DEFAULT_TRANSFER_GAS_LIMIT = SignedLegacyTransaction.DEFAULT_GAS_LIMIT;
    }
}