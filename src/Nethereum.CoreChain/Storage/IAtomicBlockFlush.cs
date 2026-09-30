using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.Model;

namespace Nethereum.CoreChain.Storage
{
    public interface IAtomicBlockFlush
    {
        Task FlushBlockAsync(FlatStateBatch flat, ulong block, byte[] hash);

        void DiscardCapturedBlock();

        void ArmWithdrawals(ulong block, IList<Withdrawal> withdrawals);

        bool WithdrawalsFoldedFor(ulong block);

        bool BlockOwnedByStagedFlush(ulong block);

        Task DrainAsync();
    }
}
