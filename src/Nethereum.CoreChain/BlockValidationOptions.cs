using System;
using Nethereum.CoreChain.Storage;

namespace Nethereum.CoreChain
{
    public sealed class BlockValidationOptions
    {
        public bool SealedByClique { get; init; }

        public bool FeeFreeBaseFee { get; init; }

        public Func<long>? UnixClock { get; init; }

        public IUncleStore? UncleStore { get; init; }

        public Nethereum.Model.ITransactionVerificationAndRecovery? TransactionVerifier { get; init; }
    }
}
