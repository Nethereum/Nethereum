using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util.HashProviders;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    /// <summary>
    /// Asserts the on-disk byte shape of <c>RocksDbStateStore</c> after the canonical-key rekey:
    /// accounts keyed by <c>keccak256(address)</c> with the original 20-byte address inlined as a
    /// value prefix; storage keyed by <c>keccak256(address) ‖ rawSlot[32]</c>. Yellow Paper §4.1.
    /// </summary>
    public class StateStoreKeccakLayoutTests : IDisposable
    {
        private readonly RocksDbTestFixture _fixture;
        private readonly Sha3KeccackHashProvider _keccak = new();

        private const string Address = "0xabcdef0123456789abcdef0123456789abcdef01";
        private static readonly byte[] AddressBytes = Address.HexToByteArray();

        public StateStoreKeccakLayoutTests() => _fixture = new RocksDbTestFixture();
        public void Dispose() => _fixture.Dispose();

        // ---------- 1. ACCOUNT KEY SHAPE ----------

        [Fact]
        public async Task SaveAccount_OnDiskKey_IsKeccak256OfAddress()
        {
            var account = MakeAccount(balance: 1234, nonce: 5);
            await _fixture.StateStore.SaveAccountAsync(Address, account);

            var expectedKey = _keccak.ComputeHash(AddressBytes);
            var raw = _fixture.Manager.Get(RocksDbManager.CF_STATE_ACCOUNTS, expectedKey);

            Assert.NotNull(raw);
            Assert.Equal(32, expectedKey.Length);
        }

        [Fact]
        public async Task SaveAccount_RawAddressKey_DoesNotExist()
        {
            var account = MakeAccount(balance: 1234, nonce: 5);
            await _fixture.StateStore.SaveAccountAsync(Address, account);

            // Old layout used the raw 20-byte address as the key. After rekey, that key must NOT exist.
            var stale = _fixture.Manager.Get(RocksDbManager.CF_STATE_ACCOUNTS, AddressBytes);
            Assert.Null(stale);
        }

        // ---------- 2. ACCOUNT VALUE LAYOUT ----------

        [Fact]
        public async Task SaveAccount_ValuePrefix_IsInlineAddressBytes()
        {
            var account = MakeAccount(balance: 7777, nonce: 9);
            await _fixture.StateStore.SaveAccountAsync(Address, account);

            var key = _keccak.ComputeHash(AddressBytes);
            var raw = _fixture.Manager.Get(RocksDbManager.CF_STATE_ACCOUNTS, key);
            Assert.NotNull(raw);
            Assert.True(raw.Length >= 20, "value too short to hold inline address");

            var inlineAddress = raw.Take(20).ToArray();
            Assert.Equal(AddressBytes, inlineAddress);
        }

        // ---------- 3. ACCOUNT ROUND-TRIP ----------

        [Fact]
        public async Task SaveAccount_RoundTrip_PreservesEveryField()
        {
            var original = new Account
            {
                Balance = new BigInteger(123_456_789),
                Nonce = 17,
                CodeHash = MakeBytes(0xAB, 32),
                StateRoot = MakeBytes(0xCD, 32)
            };
            await _fixture.StateStore.SaveAccountAsync(Address, original);

            var roundtrip = await _fixture.StateStore.GetAccountAsync(Address);
            Assert.NotNull(roundtrip);
            Assert.Equal(original.Balance, roundtrip.Balance);
            Assert.Equal(original.Nonce, roundtrip.Nonce);
            Assert.Equal(original.CodeHash, roundtrip.CodeHash);
            Assert.Equal(original.StateRoot, roundtrip.StateRoot);
        }

        // ---------- 4. ENUMERATION YIELDS THE ORIGINAL ADDRESS ----------

        [Fact]
        public async Task GetAllAccounts_YieldsOriginalAddressString_NotKeccakHash()
        {
            await _fixture.StateStore.SaveAccountAsync(Address, MakeAccount(balance: 1, nonce: 0));

            var all = await _fixture.StateStore.GetAllAccountsAsync();

            Assert.Single(all);
            var key = all.Keys.Single();
            Assert.Equal(Address, key.ToLowerInvariant());
        }

        [Fact]
        public async Task StreamAccounts_YieldsOriginalAddressString_NotKeccakHash()
        {
            await _fixture.StateStore.SaveAccountAsync(Address, MakeAccount(balance: 1, nonce: 0));

            var collected = new System.Collections.Generic.List<string>();
            await foreach (var kv in _fixture.StateStore.StreamAccountsAsync())
                collected.Add(kv.Key);

            Assert.Single(collected);
            Assert.Equal(Address, collected[0].ToLowerInvariant());
        }

        // ---------- 5. STORAGE KEY SHAPE ----------

        [Fact]
        public async Task SaveStorage_OnDiskKey_IsKeccakAddrConcatKeccakSlot()
        {
            BigInteger slot = 42;
            var value = MakeBytes(0xEE, 32);
            await _fixture.StateStore.SaveStorageAsync(Address, slot, value);

            // Yellow Paper §4.1: storage path = keccak(addr) ‖ keccak(slot).
            // Aligns with geth/erigon/reth and snap/1 wire shape.
            var addrHash = _keccak.ComputeHash(AddressBytes);
            var slotHash = Nethereum.CoreChain.Storage.StateKeys.StorageSlotKey(slot);

            var expectedKey = new byte[64];
            Buffer.BlockCopy(addrHash, 0, expectedKey, 0, 32);
            Buffer.BlockCopy(slotHash, 0, expectedKey, 32, 32);

            var raw = _fixture.Manager.Get(RocksDbManager.CF_STATE_STORAGE, expectedKey);
            Assert.NotNull(raw);
            Assert.Equal(value, raw);
        }

        // ---------- 6. STORAGE ROUND-TRIP ----------

        [Fact]
        public async Task SaveStorage_RoundTrip_PreservesValue()
        {
            BigInteger slot = 99;
            var value = MakeBytes(0x77, 32);
            await _fixture.StateStore.SaveStorageAsync(Address, slot, value);

            var read = await _fixture.StateStore.GetStorageAsync(Address, slot);
            Assert.Equal(value, read);
        }

        // ---------- 7. STORAGE BIGINTEGER RECOVERY ----------

        [Fact]
        public async Task GetAllStorage_YieldsKeccakSlotKeys()
        {
            // Three distinct slot values across the BigInteger range
            BigInteger small = 7;
            BigInteger mid = new BigInteger(123_456_789);
            BigInteger large = BigInteger.Parse("123456789012345678901234567890");

            await _fixture.StateStore.SaveStorageAsync(Address, small, MakeBytes(0x11, 32));
            await _fixture.StateStore.SaveStorageAsync(Address, mid, MakeBytes(0x22, 32));
            await _fixture.StateStore.SaveStorageAsync(Address, large, MakeBytes(0x33, 32));

            var all = await _fixture.StateStore.GetAllStorageAsync(Address);

            Assert.Equal(3, all.Count);
            // Keys are keccak(paddedSlot) — Yellow Paper §4.1 storage-trie path.
            Assert.True(all.ContainsKey(Nethereum.CoreChain.Storage.StateKeys.StorageSlotKey(small)), "small slot keccak missing");
            Assert.True(all.ContainsKey(Nethereum.CoreChain.Storage.StateKeys.StorageSlotKey(mid)), "mid slot keccak missing");
            Assert.True(all.ContainsKey(Nethereum.CoreChain.Storage.StateKeys.StorageSlotKey(large)), "large slot keccak missing");
            Assert.Equal(MakeBytes(0x11, 32), all[Nethereum.CoreChain.Storage.StateKeys.StorageSlotKey(small)]);
            Assert.Equal(MakeBytes(0x22, 32), all[Nethereum.CoreChain.Storage.StateKeys.StorageSlotKey(mid)]);
            Assert.Equal(MakeBytes(0x33, 32), all[Nethereum.CoreChain.Storage.StateKeys.StorageSlotKey(large)]);
        }

        // ---------- 8. STORAGE PREFIX ISOLATION ----------

        [Fact]
        public async Task GetAllStorage_DoesNotLeakAcrossAddresses()
        {
            const string other = "0x1234567890123456789012345678901234567890";
            await _fixture.StateStore.SaveStorageAsync(Address, 1, MakeBytes(0xA1, 32));
            await _fixture.StateStore.SaveStorageAsync(other, 1, MakeBytes(0xB1, 32));

            var mine = await _fixture.StateStore.GetAllStorageAsync(Address);
            var theirs = await _fixture.StateStore.GetAllStorageAsync(other);

            Assert.Single(mine);
            Assert.Single(theirs);
            var slotOneHash = Nethereum.CoreChain.Storage.StateKeys.StorageSlotKey(BigInteger.One);
            Assert.Equal(MakeBytes(0xA1, 32), mine[slotOneHash]);
            Assert.Equal(MakeBytes(0xB1, 32), theirs[slotOneHash]);
        }

        // ---------- 9. ADDRESS-CASE INSENSITIVITY ----------

        [Fact]
        public async Task SaveAccount_ChecksummedAddress_ResolvesSameAsLowercase()
        {
            var account = MakeAccount(balance: 555, nonce: 1);
            var checksummed = "0xABCDEF0123456789ABCDEF0123456789abcdef01";

            await _fixture.StateStore.SaveAccountAsync(checksummed, account);
            var read = await _fixture.StateStore.GetAccountAsync(checksummed.ToLowerInvariant());
            Assert.NotNull(read);
            Assert.Equal(account.Balance, read.Balance);

            // And the underlying RocksDB key matches keccak of the 20-byte address regardless of case.
            var expectedKey = _keccak.ComputeHash(checksummed.HexToByteArray());
            var raw = _fixture.Manager.Get(RocksDbManager.CF_STATE_ACCOUNTS, expectedKey);
            Assert.NotNull(raw);
        }

        // ---------- helpers ----------

        private static Account MakeAccount(BigInteger balance, BigInteger nonce) => new()
        {
            Balance = balance,
            Nonce = nonce,
            CodeHash = DefaultValues.EMPTY_DATA_HASH,
            StateRoot = DefaultValues.EMPTY_TRIE_HASH
        };

        private static byte[] MakeBytes(byte fill, int length)
        {
            var b = new byte[length];
            for (var i = 0; i < length; i++) b[i] = fill;
            return b;
        }
    }
}
