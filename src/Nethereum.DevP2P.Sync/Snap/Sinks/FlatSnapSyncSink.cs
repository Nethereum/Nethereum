using System;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Snap.Storage;
using Nethereum.Model;

namespace Nethereum.DevP2P.Sync.Snap.Sinks
{
    public sealed class FlatSnapSyncSink : ISnapSyncSink
    {
        private readonly IStateStore _codeStore;
        private int _accountCount;
        private int _slotCount;
        private int _bytecodeCount;

        public FlatSnapSyncSink(ISnapFlatStateWriter flatWriter, IStateStore codeStore)
        {
            FlatWriter = flatWriter ?? throw new ArgumentNullException(nameof(flatWriter));
            _codeStore = codeStore ?? throw new ArgumentNullException(nameof(codeStore));
        }

        public ISnapFlatStateWriter FlatWriter { get; }

        public int AccountCount => Volatile.Read(ref _accountCount);
        public int SlotCount => Volatile.Read(ref _slotCount);
        public int BytecodeCount => Volatile.Read(ref _bytecodeCount);

        public ValueTask BeginAsync(byte[] targetRoot, CancellationToken ct) => default;

        public async ValueTask WriteAccountAsync(byte[] accountHash, byte[] slimRlp, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var account = new AccountEncoder().Decode(SlimAccountEncoder.FromSlim(slimRlp));
            await FlatWriter.SaveAccountByHashAsync(accountHash, account).ConfigureAwait(false);
            Interlocked.Increment(ref _accountCount);
        }

        public ValueTask<IStorageScope> BeginAccountStorageAsync(
            byte[] accountHash, byte[] expectedStorageRoot, CancellationToken ct)
            => new ValueTask<IStorageScope>(new FlatStorageScope(this, accountHash));

        public async ValueTask WriteBytecodeAsync(byte[] codeHash, byte[] code, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await _codeStore.SaveCodeAsync(codeHash, code).ConfigureAwait(false);
            Interlocked.Increment(ref _bytecodeCount);
        }

        public ValueTask<byte[]> FinaliseRootAsync(CancellationToken ct)
            => throw new InvalidOperationException(
                "A flat-only snap/2 download keeps no local trie; the state root is produced by generating the trie from flat state.");

        private sealed class FlatStorageScope : IStorageScope
        {
            private readonly FlatSnapSyncSink _sink;
            private readonly byte[] _accountHash;

            public FlatStorageScope(FlatSnapSyncSink sink, byte[] accountHash)
            {
                _sink = sink;
                _accountHash = accountHash;
            }

            public async ValueTask WriteSlotAsync(byte[] slotHash, byte[] valueRlp, CancellationToken ct)
            {
                ct.ThrowIfCancellationRequested();
                await _sink.FlatWriter.SaveStorageByHashAsync(
                    _accountHash, slotHash, Nethereum.RLP.RLP.Decode(valueRlp).RLPData).ConfigureAwait(false);
                Interlocked.Increment(ref _sink._slotCount);
            }

            public ValueTask EndAsync(CancellationToken ct) => default;

            public ValueTask AbortAsync(CancellationToken ct) => default;
        }
    }
}
