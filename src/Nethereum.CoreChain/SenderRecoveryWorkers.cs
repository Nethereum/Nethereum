using System;
using System.Collections.Generic;
using System.Threading;

namespace Nethereum.CoreChain
{
    internal static class SenderRecoveryWorkers
    {
        public const string WorkerThreadNamePrefix = "sender-recovery-";

        private static readonly TimeSpan IdleRetirement = TimeSpan.FromSeconds(10);
        private static readonly object Gate = new object();
        private static readonly Queue<RecoveredSenders> Pending = new Queue<RecoveredSenders>();
        private static int _threads;
        private static int _started;

        public static void Submit(RecoveredSenders senders)
        {
            if (senders.Completed.IsCompleted) return;
            lock (Gate)
            {
                Pending.Enqueue(senders);
                StartThreadsFor(senders.Length);
                Monitor.PulseAll(Gate);
            }
        }

        private static void StartThreadsFor(int work)
        {
            var wanted = Math.Min(Math.Max(1, Environment.ProcessorCount), work);
            while (_threads < wanted)
            {
                _threads++;
                new Thread(RunWorker) { IsBackground = true, Name = WorkerThreadNamePrefix + _started++ }.Start();
            }
        }

        private static void RunWorker()
        {
            RecoveredSenders? senders;
            while ((senders = NextWork()) != null)
            {
                while (senders.TryRecoverNext()) { }
            }
        }

        private static RecoveredSenders? NextWork()
        {
            lock (Gate)
            {
                while (true)
                {
                    while (Pending.Count > 0)
                    {
                        var senders = Pending.Peek();
                        if (senders.HasUnclaimed) return senders;
                        Pending.Dequeue();
                    }
                    if (!Monitor.Wait(Gate, IdleRetirement) && Pending.Count == 0)
                    {
                        _threads--;
                        return null;
                    }
                }
            }
        }
    }
}
