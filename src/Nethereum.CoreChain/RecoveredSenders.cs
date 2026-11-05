using System;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Model;

namespace Nethereum.CoreChain
{
    internal sealed class RecoveredSenders
    {
        private readonly ITransactionVerificationAndRecovery _verifier;
        private readonly ISignedTransaction?[] _transactions;
        private readonly string?[] _senders;
        private readonly bool[] _settled;
        private readonly object _gate = new object();
        private readonly TaskCompletionSource<bool> _completed =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _claimed;
        private int _remaining;

        public RecoveredSenders(ITransactionVerificationAndRecovery verifier, ISignedTransaction?[] transactions)
        {
            _verifier = verifier;
            _transactions = transactions;
            _senders = new string?[transactions.Length];
            _settled = new bool[transactions.Length];
            _remaining = CountToRecover(transactions);
            if (_remaining == 0) _completed.SetResult(true);
        }

        public Task Completed => _completed.Task;

        public int Length => _transactions.Length;

        public bool HasUnclaimed => Volatile.Read(ref _claimed) < _transactions.Length;

        public bool TryRecoverNext()
        {
            var index = Interlocked.Increment(ref _claimed) - 1;
            if (index >= _transactions.Length) return false;
            var transaction = _transactions[index];
            if (transaction == null) return true;
            Settle(index, Recover(transaction));
            return true;
        }

        public string? TryGetSender(int index, ISignedTransaction transaction)
        {
            if (!IsBoundTo(index, transaction)) return null;
            lock (_gate) return _settled[index] ? _senders[index] : null;
        }

        public string? WaitForSender(int index, ISignedTransaction transaction)
        {
            if (!IsBoundTo(index, transaction)) return null;
            lock (_gate)
            {
                while (!_settled[index]) Monitor.Wait(_gate);
                return _senders[index];
            }
        }

        private bool IsBoundTo(int index, ISignedTransaction transaction) =>
            (uint)index < (uint)_transactions.Length && ReferenceEquals(_transactions[index], transaction);

        private string? Recover(ISignedTransaction transaction)
        {
            try
            {
                return _verifier.GetSenderAddress(transaction);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void Settle(int index, string? sender)
        {
            lock (_gate)
            {
                _senders[index] = sender;
                _settled[index] = true;
                Monitor.PulseAll(_gate);
            }
            if (Interlocked.Decrement(ref _remaining) == 0) _completed.TrySetResult(true);
        }

        private static int CountToRecover(ISignedTransaction?[] transactions)
        {
            var count = 0;
            foreach (var transaction in transactions)
                if (transaction != null) count++;
            return count;
        }
    }
}
