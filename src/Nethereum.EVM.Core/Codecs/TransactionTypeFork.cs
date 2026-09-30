using System;
using Nethereum.EVM;
using Nethereum.Model;

namespace Nethereum.Model.Codecs
{
    public static class TransactionTypeFork
    {
        public static bool IsAcceptedAt(ISignedTransaction tx, HardforkName fork)
        {
            if (tx is null) throw new ArgumentNullException(nameof(tx));

            if (tx is Transaction7702)
                return fork >= HardforkName.Prague;

            if (tx is Transaction4844)
                return fork >= HardforkName.Cancun;

            if (tx is Transaction1559)
                return fork >= HardforkName.London;

            if (tx is Transaction2930)
                return fork >= HardforkName.Berlin;

            if (tx is LegacyTransaction || tx is LegacyTransactionChainId)
                return true;

            return false;
        }
    }
}
