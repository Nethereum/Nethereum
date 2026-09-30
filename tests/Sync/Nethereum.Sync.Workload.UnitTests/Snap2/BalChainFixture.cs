using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.Chain.TestData.UnitTests
{
    internal sealed class BalChainFixture
    {
        private const long GenesisTimestamp = 1_700_000_000;
        private const long BlockIntervalSeconds = 12;

        private static readonly Sha3Keccack Keccak = Sha3Keccack.Current;

        private readonly List<BlockHeader> _headers = new();
        private readonly List<byte[]> _hashes = new();
        private readonly List<List<AccountChanges>> _accessLists = new();
        private readonly Dictionary<byte[], ulong> _numberByHash = new(ByteArrayComparer.Current);
        private readonly HeadStateLoader.BytecodeStore _codes = new();
        private readonly Dictionary<string, FixtureAccount> _state = new();

        private BalChainFixture() { }

        public InMemoryContentNodeStore NodeStore { get; } = new();

        public ulong Tip => (ulong)(_headers.Count - 1);

        public static BalChainFixture Build(
            IReadOnlyList<AccountChanges> genesis, IReadOnlyList<IReadOnlyList<AccountChanges>> blocks)
        {
            var fixture = new BalChainFixture();
            fixture.ApplyChanges(genesis);
            fixture.Seal(new List<AccountChanges>(), new byte[32]);
            foreach (var block in blocks)
            {
                fixture.ApplyChanges(block);
                fixture.Seal(block.ToList(), fixture._hashes[^1]);
            }
            return fixture;
        }

        public BlockHeader HeaderAt(ulong number) => _headers[(int)number];

        public byte[] HashAt(ulong number) => _hashes[(int)number];

        public IReadOnlyList<AccountChanges> AccessListAt(ulong number) => _accessLists[(int)number];

        public byte[] EncodedAccessListAt(ulong number)
            => BlockAccessListRLPEncoder.Current.Encode(_accessLists[(int)number]);

        public byte[] EncodedAccessListByBlockHash(byte[] blockHash)
            => _numberByHash.TryGetValue(blockHash, out var number) ? EncodedAccessListAt(number) : null;

        public PatriciaSnapRequestHandler CreateSnapHandler(int softResponseLimit = PatriciaSnapRequestHandler.SoftResponseLimit)
            => new PatriciaSnapRequestHandler(NodeStore, _codes, softResponseLimit: softResponseLimit);

        public ScriptedSnapPeer CreateSnapPeer() => new ScriptedSnapPeer(CreateSnapHandler());

        public ScriptedBlockAccessListPeerSource CreateBlockAccessListPeerSource()
            => new ScriptedBlockAccessListPeerSource(EncodedAccessListByBlockHash);

        public BlockingBackfillScheduler CreateBlockingBackfillScheduler()
            => new BlockingBackfillScheduler(CreateSnapHandler());

        public async Task LayCanonicalAsync(IChainStoreBundle bundle)
        {
            for (ulong number = 0; number <= Tip; number++)
                await bundle.Blocks.SaveAsync(HeaderAt(number), HashAt(number));
            bundle.Metadata.SaveHeaderSyncState(HeaderSubchains.OpenTip(HeaderSyncState.Empty, Tip));
        }

        private void ApplyChanges(IReadOnlyList<AccountChanges> changes)
        {
            foreach (var change in changes)
            {
                var address = change.Address.HexToByteArray().ToHex(true);
                var account = _state.TryGetValue(address, out var existing) ? existing : new FixtureAccount();
                ApplyStorage(account, change);
                ApplyFields(account, change);
                if (account.IsEmpty) _state.Remove(address);
                else _state[address] = account;
            }
        }

        private static void ApplyStorage(FixtureAccount account, AccountChanges change)
        {
            foreach (var slot in change.StorageChanges.Where(s => s.Changes.Count > 0))
            {
                var postValue = slot.Changes.OrderBy(c => c.BlockAccessIndex).Last().PostValue;
                var slotHash = Keccak.CalculateHash(slot.Slot.ToBigEndian());
                if (postValue.IsZero) account.Storage.Remove(slotHash);
                else account.Storage[slotHash] = TrimLeadingZeros(postValue.ToBigEndian());
            }
        }

        private static void ApplyFields(FixtureAccount account, AccountChanges change)
        {
            if (change.BalanceChanges.Count > 0)
                account.Balance = change.BalanceChanges.OrderBy(c => c.BlockAccessIndex).Last().PostBalance;
            if (change.NonceChanges.Count > 0)
                account.Nonce = change.NonceChanges.OrderBy(c => c.BlockAccessIndex).Last().NewNonce;
            if (change.CodeChanges.Count > 0)
                account.Code = change.CodeChanges.OrderBy(c => c.BlockAccessIndex).Last().NewCode ?? Array.Empty<byte>();
        }

        private void Seal(List<AccountChanges> accessList, byte[] parentHash)
        {
            var number = (ulong)_headers.Count;
            var header = new BlockHeader
            {
                ParentHash = parentHash,
                UnclesHash = new byte[32],
                Coinbase = "0x0000000000000000000000000000000000000000",
                StateRoot = CommitStateRoot(),
                TransactionsHash = DefaultValues.EMPTY_TRIE_HASH,
                ReceiptHash = DefaultValues.EMPTY_TRIE_HASH,
                BlockNumber = number,
                LogsBloom = new byte[256],
                Difficulty = 0,
                Timestamp = GenesisTimestamp + (long)number * BlockIntervalSeconds,
                GasLimit = 30_000_000,
                GasUsed = 0,
                MixHash = new byte[32],
                ExtraData = Array.Empty<byte>(),
                Nonce = new byte[8],
                BaseFee = 7,
                WithdrawalsRoot = DefaultValues.EMPTY_TRIE_HASH,
                BlobGasUsed = 0,
                ExcessBlobGas = 0,
                ParentBeaconBlockRoot = new byte[32],
                RequestsHash = new byte[32],
                BlockAccessListHash = BlockAccessListRLPEncoder.Current.Hash(accessList),
                SlotNumber = number,
            };
            var hash = RlpKeccakBlockHashProvider.Instance.ComputeBlockHash(header);
            _headers.Add(header);
            _hashes.Add(hash);
            _accessLists.Add(accessList);
            _numberByHash[hash] = number;
        }

        private byte[] CommitStateRoot()
        {
            var accountTrie = new PatriciaTrie(NodeStore);
            foreach (var entry in _state)
            {
                var account = new Account
                {
                    Nonce = entry.Value.Nonce,
                    Balance = entry.Value.Balance,
                    StateRoot = CommitStorageRoot(entry.Value.Storage),
                    CodeHash = CommitCode(entry.Value.Code),
                };
                accountTrie.Put(Keccak.CalculateHash(entry.Key.HexToByteArray()), new AccountEncoder().Encode(account));
            }
            accountTrie.SaveNodesToStorage();
            return accountTrie.Root.GetHash();
        }

        private byte[] CommitStorageRoot(Dictionary<byte[], byte[]> storage)
        {
            if (storage.Count == 0) return DefaultValues.EMPTY_TRIE_HASH;
            var storageTrie = new PatriciaTrie(NodeStore);
            foreach (var slot in storage)
                storageTrie.Put(slot.Key, RLP.RLP.EncodeElement(slot.Value));
            storageTrie.SaveNodesToStorage();
            return storageTrie.Root.GetHash();
        }

        private byte[] CommitCode(byte[] code)
        {
            if (code.Length == 0) return DefaultValues.EMPTY_DATA_HASH;
            var codeHash = Keccak.CalculateHash(code);
            _codes.Put(codeHash, code);
            return codeHash;
        }

        private static byte[] TrimLeadingZeros(byte[] value)
        {
            var start = 0;
            while (start < value.Length && value[start] == 0) start++;
            return value.Skip(start).ToArray();
        }

        private sealed class FixtureAccount
        {
            public EvmUInt256 Nonce { get; set; }

            public EvmUInt256 Balance { get; set; }

            public byte[] Code { get; set; } = Array.Empty<byte>();

            public Dictionary<byte[], byte[]> Storage { get; } = new(ByteArrayComparer.Current);

            public bool IsEmpty => Nonce.IsZero && Balance.IsZero && Code.Length == 0;
        }
    }
}
