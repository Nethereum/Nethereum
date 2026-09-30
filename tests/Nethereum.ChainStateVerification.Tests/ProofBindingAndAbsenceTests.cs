using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Consensus.LightClient;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Proofs;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.ChainStateVerification.Tests
{
    public class ProofBindingAndAbsenceTests
    {
        private const string Whale = "0x1111111111111111111111111111111111111111";
        private const string Contract = "0x2222222222222222222222222222222222222222";
        private const string NeverUsed = "0x3333333333333333333333333333333333333333";
        private const string ZeroPrefixed = "0x00000000219ab540356cbb839cbe05303d7705fa";
        private const string ZeroPrefixedStripped = "0x219ab540356cbb839cbe05303d7705fa";
        private static readonly string ZeroHash = new byte[32].ToHex(true);
        private static readonly BigInteger WhaleBalance = BigInteger.Parse("5000000000000000000000");
        private static readonly Sha3KeccackHashProvider HashProvider = Sha3KeccackHashProvider.Instance;

        private readonly PatriciaTrie _state;
        private readonly PatriciaTrie _storage;
        private readonly byte[] _stateRoot;
        private readonly Account _whale;
        private readonly Account _contract;
        private readonly Account _zeroPrefixed;

        public ProofBindingAndAbsenceTests()
        {
            _storage = new PatriciaTrie(new InMemoryContentNodeStore());
            PutSlot(2, 105);
            PutSlot(3, 7);
            for (var i = 100; i < 140; i++) PutSlot(i, i);
            _storage.SaveNodesToStorage();

            _whale = new Account { Nonce = 3, Balance = WhaleBalance, CodeHash = DefaultValues.EMPTY_DATA_HASH, StateRoot = DefaultValues.EMPTY_TRIE_HASH };
            _contract = new Account { Nonce = 1, Balance = 0, CodeHash = HashProvider.ComputeHash(new byte[] { 0x60, 0x00 }), StateRoot = _storage.Root.GetHash() };

            _state = new PatriciaTrie(new InMemoryContentNodeStore());
            _state.Put(AddressKey(Whale), AccountEncoder.Current.Encode(_whale));
            _state.Put(AddressKey(Contract), AccountEncoder.Current.Encode(_contract));
            _zeroPrefixed = new Account { Nonce = 1, Balance = WhaleBalance, CodeHash = HashProvider.ComputeHash(new byte[] { 0x60, 0x01 }), StateRoot = _storage.Root.GetHash() };
            _state.Put(AddressKey(ZeroPrefixed), AccountEncoder.Current.Encode(_zeroPrefixed));
            for (var i = 0; i < 64; i++)
                _state.Put(HashProvider.ComputeHash(new[] { (byte)i, (byte)0xBB }),
                           AccountEncoder.Current.Encode(new Account { Nonce = (ulong)i, CodeHash = DefaultValues.EMPTY_DATA_HASH, StateRoot = DefaultValues.EMPTY_TRIE_HASH }));
            _state.SaveNodesToStorage();
            _stateRoot = _state.Root.GetHash();
        }

        private void PutSlot(int slot, int value)
            => _storage.Put(SlotKey(slot), AccountStorage.EncodeValueForStorage(new BigInteger(value).ToByteArray(isUnsigned: true, isBigEndian: true)));

        private static byte[] AddressKey(string address) => HashProvider.ComputeHash(address.HexToByteArray());

        private static byte[] SlotKey(int slot)
            => HashProvider.ComputeHash(new BigInteger(slot).ToByteArray(isUnsigned: true, isBigEndian: true).PadTo32Bytes());

        private static List<string> Hex(IEnumerable<byte[]> nodes) => nodes.Select(n => n.ToHex(true)).ToList();

        private AccountProof HonestProof(string address, Account account, params int[] slots)
        {
            var proof = new AccountProof
            {
                Address = address,
                Balance = new HexBigInteger((BigInteger)account.Balance),
                Nonce = new HexBigInteger((BigInteger)account.Nonce),
                CodeHash = account.CodeHash.ToHex(true),
                StorageHash = account.StateRoot.ToHex(true),
                AccountProofs = Hex(ProofGenerator.GeneratePathProof(_state, AddressKey(address))),
                StorageProof = new List<StorageProof>()
            };
            foreach (var slot in slots)
            {
                var stored = _storage.Get(SlotKey(slot));
                var value = stored == null ? BigInteger.Zero : Nethereum.RLP.RLP.Decode(stored).RLPData.ToBigIntegerFromRLPDecoded();
                proof.StorageProof.Add(new StorageProof
                {
                    Key = new HexBigInteger(slot),
                    Value = new HexBigInteger(value),
                    Proof = Hex(ProofGenerator.GeneratePathProof(_storage, SlotKey(slot)))
                });
            }
            return proof;
        }

        private static Account EmptyAccount() => new Account
        {
            CodeHash = DefaultValues.EMPTY_DATA_HASH,
            StateRoot = DefaultValues.EMPTY_TRIE_HASH
        };

        private VerifiedStateService Service(AccountProof served)
            => new VerifiedStateService(new FixedHeader(_stateRoot), new ServesOneProof(served), new NoCode(), new TrieProofVerifier());

        private VerifiedStateBackend Backend(AccountProof served)
            => new VerifiedStateBackend(new FixedHeader(_stateRoot), new ServesOneProof(served), new TrieProofVerifier());

        private StorageProofVerifier Standalone(AccountProof served)
            => new StorageProofVerifier(new FixedHeader(_stateRoot), new ServesOneProof(served), new TrieProofVerifier());

        private static AccountProof AsRpcResponse(string address, string balance, string nonce, string codeHash, string storageHash,
            IEnumerable<string> accountProof, params (string key, string value, IEnumerable<string> proof)[] slots)
        {
            var json = new JObject
            {
                ["address"] = address,
                ["balance"] = balance,
                ["nonce"] = nonce,
                ["codeHash"] = codeHash,
                ["storageHash"] = storageHash,
                ["accountProof"] = new JArray(accountProof),
                ["storageProof"] = new JArray(slots.Select(slot => new JObject
                {
                    ["key"] = slot.key,
                    ["value"] = slot.value,
                    ["proof"] = new JArray(slot.proof)
                }))
            };
            return json.ToObject<AccountProof>();
        }

        private List<string> AccountPath(string address) => Hex(ProofGenerator.GeneratePathProof(_state, AddressKey(address)));

        private List<string> StoragePath(byte[] slotKey) => Hex(ProofGenerator.GeneratePathProof(_storage, HashProvider.ComputeHash(slotKey)));

        private AccountProof GethAbsentResponse(string echoedAddress, string provenAddress, params string[] slots)
            => AbsentResponse(echoedAddress, provenAddress, ZeroHash, ZeroHash, slots);

        private AccountProof AbsentResponse(string echoedAddress, string provenAddress, string codeHash, string storageHash, params string[] slots)
            => AsRpcResponse(echoedAddress, "0x0", "0x0", codeHash, storageHash, AccountPath(provenAddress),
                slots.Select(slot => (slot, "0x0", (IEnumerable<string>)new List<string>())).ToArray());

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Given_TheRpcEchoesAZeroPrefixedAddressWithItsLeadingZerosStripped_When_ItProvesTheStrippedKeyAbsent_Then_EveryEntryPointRejectsIt(bool gethZeroHashes)
        {
            var alias = gethZeroHashes
                ? GethAbsentResponse(ZeroPrefixedStripped, ZeroPrefixedStripped, "0x02")
                : AbsentResponse(ZeroPrefixedStripped, ZeroPrefixedStripped, DefaultValues.EMPTY_DATA_HASH.ToHex(true), DefaultValues.EMPTY_TRIE_HASH.ToHex(true), "0x02");

            await Assert.ThrowsAsync<InvalidChainDataException>(() => Service(alias).GetAccountAsync(ZeroPrefixed));
            await Assert.ThrowsAsync<InvalidChainDataException>(() => Service(alias).GetCodeAsync(ZeroPrefixed));
            await Assert.ThrowsAsync<InvalidChainDataException>(() => Service(alias).GetStorageAtAsync(ZeroPrefixed, "0x02"));
            await Assert.ThrowsAsync<InvalidChainDataException>(() => Backend(alias).GetAccountAsync(ZeroPrefixed));
            await Assert.ThrowsAsync<InvalidChainDataException>(() => Standalone(alias).GetStorageValueAsync(ZeroPrefixed, "0x02"));
        }

        [Fact]
        public async Task Given_TheRpcEchoesA33ByteSlotKeyThatIsNumericallyTheRequestedSlot_When_ItProvesThatKeyAbsent_Then_TheForgedZeroIsRejected()
        {
            var slot33 = new byte[33];
            slot33[32] = 0x02;
            var alias = AsRpcResponse(Contract, "0x0", "0x1", _contract.CodeHash.ToHex(true), _contract.StateRoot.ToHex(true), AccountPath(Contract),
                (slot33.ToHex(true), "0x0", StoragePath(slot33)));

            await Assert.ThrowsAsync<InvalidChainDataException>(() => Service(alias).GetStorageAtAsync(Contract, "0x02"));
            await Assert.ThrowsAsync<InvalidChainDataException>(() => Service(alias).GetStorageAtAsync(Contract, new BigInteger(2)));
            await Assert.ThrowsAsync<InvalidChainDataException>(() => Standalone(alias).GetStorageValueAsync(Contract, "0x2"));
        }

        [Fact]
        public async Task Given_AGethResponseForANeverUsedAddress_When_ItsAccountCodeAndStorageAreRead_Then_TheyAreEmpty()
        {
            var geth = GethAbsentResponse(NeverUsed, NeverUsed, "0x02");

            var account = await Service(geth).GetAccountAsync(NeverUsed);
            var code = await Service(geth).GetCodeAsync(NeverUsed);
            var slot = await Service(geth).GetStorageAtAsync(NeverUsed, "0x02");
            var standalone = await Standalone(geth).GetStorageValueAsync(NeverUsed, "0x02");
            var backend = await Backend(geth).GetAccountAsync(NeverUsed);

            Assert.Equal(BigInteger.Zero, (BigInteger)account.Balance);
            Assert.Equal(BigInteger.Zero, (BigInteger)backend.Balance);
            Assert.Empty(code);
            Assert.Equal(BigInteger.Zero, slot.ToHex().HexToBigInteger(false));
            Assert.Equal(BigInteger.Zero, standalone.ToHex().HexToBigInteger(false));
            var lying = GethAbsentResponse(NeverUsed, NeverUsed);
            lying.Balance = new HexBigInteger(1);
            await Assert.ThrowsAsync<InvalidChainDataException>(() => Service(lying).GetAccountAsync(NeverUsed));
        }

        [Fact]
        public async Task Given_GethEchoesTheSlotAsAQuantityOrAs32Bytes_When_TheVerifiedStorageIsRead_Then_TheStoredValueIsReturned()
        {
            var slot32 = new BigInteger(2).ToByteArray(isUnsigned: true, isBigEndian: true).PadTo32Bytes();
            var quantity = AsRpcResponse(Contract, "0x0", "0x1", _contract.CodeHash.ToHex(true), _contract.StateRoot.ToHex(true), AccountPath(Contract),
                ("0x2", "0x69", StoragePath(slot32)));
            var full = AsRpcResponse(Contract, "0x0", "0x1", _contract.CodeHash.ToHex(true), _contract.StateRoot.ToHex(true), AccountPath(Contract),
                (slot32.ToHex(true), "0x69", StoragePath(slot32)));

            var fromQuantity = await Service(quantity).GetStorageAtAsync(Contract, "0x02");
            var fromFull = await Service(full).GetStorageAtAsync(Contract, slot32.ToHex(true));

            Assert.Equal(new BigInteger(105), fromQuantity.ToHex().HexToBigInteger(false));
            Assert.Equal(new BigInteger(105), fromFull.ToHex().HexToBigInteger(false));
        }

        [Fact]
        public async Task Given_TheRpcAnswersForANeverUsedAddressWithTheWhalesValidProof_When_TheVerifiedAccountIsRead_Then_ItIsRejected()
        {
            var substituted = HonestProof(Whale, _whale);

            await Assert.ThrowsAsync<InvalidChainDataException>(() => Service(substituted).GetAccountAsync(NeverUsed));
            await Assert.ThrowsAsync<InvalidChainDataException>(() =>
                new VerifiedStateBackend(new FixedHeader(_stateRoot), new ServesOneProof(substituted), new TrieProofVerifier()).GetAccountAsync(NeverUsed));
        }

        [Fact]
        public async Task Given_TheRpcAnswersSlot5WithSlot2sValidProof_When_TheVerifiedStorageIsRead_Then_ItIsRejected()
        {
            var substituted = HonestProof(Contract, _contract, 2);

            await Assert.ThrowsAsync<InvalidChainDataException>(() => Service(substituted).GetStorageAtAsync(Contract, "0x05"));
            await Assert.ThrowsAsync<InvalidChainDataException>(() =>
                new StorageProofVerifier(new FixedHeader(_stateRoot), new ServesOneProof(substituted), new TrieProofVerifier()).GetStorageValueAsync(Contract, "0x5"));
        }

        [Fact]
        public async Task Given_TheRpcClaimsZeroForASetSlotWithAnEmptyProof_When_TheVerifiedStorageIsRead_Then_TheForgedZeroIsRejected()
        {
            var forged = HonestProof(Contract, _contract, 2);
            forged.StorageProof[0].Value = new HexBigInteger(0);
            forged.StorageProof[0].Proof = new List<string>();

            await Assert.ThrowsAsync<InvalidChainDataException>(() => Service(forged).GetStorageAtAsync(Contract, "0x02"));
            await Assert.ThrowsAsync<InvalidChainDataException>(() =>
                new StorageProofVerifier(new FixedHeader(_stateRoot), new ServesOneProof(forged), new TrieProofVerifier()).GetStorageValueAsync(Contract, "0x2"));
        }

        [Fact]
        public async Task Given_ANeverUsedAddressWithAnAbsenceProofAndEmptyHashes_When_TheVerifiedAccountIsRead_Then_TheEmptyAccountIsReturned()
        {
            var absence = HonestProof(NeverUsed, EmptyAccount());

            var account = await Service(absence).GetAccountAsync(NeverUsed);
            var balance = await Service(absence).GetBalanceAsync(NeverUsed);

            Assert.Equal(BigInteger.Zero, balance);
            Assert.Equal(DefaultValues.EMPTY_DATA_HASH, account.CodeHash);
            var lying = HonestProof(NeverUsed, EmptyAccount());
            lying.Balance = new HexBigInteger(1);
            await Assert.ThrowsAsync<InvalidChainDataException>(() => Service(lying).GetAccountAsync(NeverUsed));
        }

        [Fact]
        public async Task Given_HonestProofs_When_TheVerifiedAccountAndSlotsAreRead_Then_TheCommittedValuesAreReturned()
        {
            var whale = await Service(HonestProof(Whale, _whale)).GetBalanceAsync(Whale);
            var set = await Service(HonestProof(Contract, _contract, 2)).GetStorageAtAsync(Contract, "0x02");
            var unset = await Service(HonestProof(Contract, _contract, 5)).GetStorageAtAsync(Contract, new BigInteger(5));
            var standalone = await new StorageProofVerifier(new FixedHeader(_stateRoot), new ServesOneProof(HonestProof(Contract, _contract, 3)), new TrieProofVerifier())
                .GetStorageValueAsync(Contract, "0x3");

            Assert.Equal(WhaleBalance, whale);
            Assert.Equal(new BigInteger(105), set.ToHex().HexToBigInteger(false));
            Assert.Equal(BigInteger.Zero, unset.ToHex().HexToBigInteger(false));
            Assert.Equal(new BigInteger(7), standalone.ToHex().HexToBigInteger(false));
        }

        private sealed class FixedHeader : ITrustedHeaderProvider
        {
            private readonly TrustedExecutionHeader _header;

            public FixedHeader(byte[] stateRoot)
            {
                _header = new TrustedExecutionHeader
                {
                    BlockNumber = 77,
                    StateRoot = stateRoot,
                    ReceiptsRoot = new byte[32],
                    BlockHash = new byte[32],
                    Timestamp = DateTimeOffset.UtcNow
                };
            }

            public TrustedExecutionHeader GetLatestFinalized() => _header;
            public TrustedExecutionHeader GetLatestOptimistic() => _header;
            public byte[] GetBlockHash(ulong blockNumber) => null;
        }

        private sealed class ServesOneProof : IEthGetProof
        {
            private readonly AccountProof _response;

            public ServesOneProof(AccountProof response) => _response = response;

            public BlockParameter DefaultBlock { get; set; } = BlockParameter.CreateLatest();

            public RpcRequest BuildRequest(string address, string[] storageKeys, BlockParameter block, object id = null) =>
                throw new NotImplementedException();

            public Task<AccountProof> SendRequestAsync(string address, string[] storageKeys, object id = null) =>
                Task.FromResult(_response);

            public Task<AccountProof> SendRequestAsync(string address, string[] storageKeys, BlockParameter block, object id = null) =>
                Task.FromResult(_response);
        }

        private sealed class NoCode : IEthGetCode
        {
            public BlockParameter DefaultBlock { get; set; } = BlockParameter.CreateLatest();
            public RpcRequest BuildRequest(string address, BlockParameter block, object id = null) => throw new NotImplementedException();
            public Task<string> SendRequestAsync(string address, object id = null) => throw new NotImplementedException();
            public Task<string> SendRequestAsync(string address, BlockParameter block, object id = null) => throw new NotImplementedException();
        }
    }
}
