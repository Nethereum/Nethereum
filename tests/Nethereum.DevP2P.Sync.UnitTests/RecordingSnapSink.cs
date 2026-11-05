using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    internal sealed class RecordingSnapFlatWriter : ISnapFlatStateWriter
    {
        private readonly ConcurrentQueue<byte[]> _accounts = new();
        private readonly ConcurrentQueue<(byte[] Account, byte[] Slot)> _slots = new();

        public IReadOnlyCollection<byte[]> AccountsWritten => _accounts;
        public IReadOnlyCollection<(byte[] Account, byte[] Slot)> SlotsWritten => _slots;
        public ConcurrentDictionary<string, byte[]> SlotValues { get; } = new();

        public int AccountWriteCount(byte[] accountHash)
            => _accounts.Count(a => Nethereum.Util.ByteUtil.AreEqual(a, accountHash));

        public Task<Account> GetAccountByHashAsync(byte[] accountHash) => Task.FromResult<Account>(null);

        public Task SaveAccountByHashAsync(byte[] accountHash, Account account)
        {
            _accounts.Enqueue(accountHash);
            return Task.CompletedTask;
        }

        public Task DeleteAccountByHashAsync(byte[] accountHash) => Task.CompletedTask;

        public Task SaveStorageByHashAsync(byte[] accountHash, byte[] slotKeccak, byte[] value)
        {
            _slots.Enqueue((accountHash, slotKeccak));
            SlotValues[slotKeccak.ToHex()] = value;
            return Task.CompletedTask;
        }
    }

    internal sealed class RecordingSnapSink
    {
        public RecordingSnapSink()
        {
            Sink = new TrieSnapSyncSink(NodeStore, State, Flat);
        }

        public RecordingSnapFlatWriter Flat { get; } = new();
        public InMemoryContentNodeStore NodeStore { get; } = new();
        public InMemoryStateStore State { get; } = new();
        public TrieSnapSyncSink Sink { get; }
    }
}
