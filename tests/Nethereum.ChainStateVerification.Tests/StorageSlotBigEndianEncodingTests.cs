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
using Xunit;

namespace Nethereum.ChainStateVerification.Tests
{
    public class StorageSlotBigEndianEncodingTests
    {
        private const string Contract = "0x4444444444444444444444444444444444444444";
        private static readonly Sha3KeccackHashProvider HashProvider = Sha3KeccackHashProvider.Instance;

        private readonly PatriciaTrie _state;
        private readonly PatriciaTrie _storage;
        private readonly Account _contract;

        public StorageSlotBigEndianEncodingTests()
        {
            _storage = new PatriciaTrie(new InMemoryContentNodeStore());

            PutSlot(new BigInteger(256), new BigInteger(111));
            PutSlot(new BigInteger(1), new BigInteger(999));

            PutSlot(new BigInteger(258), new BigInteger(222));
            PutSlot(new BigInteger(513), new BigInteger(888));

            var mappingSlot = MappingSlot(mappingKey: "0x00000000000000000000000000000000001234", baseSlot: 0);
            PutSlot(mappingSlot, new BigInteger(333));

            _storage.SaveNodesToStorage();

            _contract = new Account
            {
                Nonce = 1,
                Balance = 0,
                CodeHash = HashProvider.ComputeHash(new byte[] { 0x60, 0x00 }),
                StateRoot = _storage.Root.GetHash()
            };

            _state = new PatriciaTrie(new InMemoryContentNodeStore());
            _state.Put(AddressKey(Contract), AccountEncoder.Current.Encode(_contract));
            _state.SaveNodesToStorage();
        }

        private static byte[] AddressKey(string address) => HashProvider.ComputeHash(address.HexToByteArray());

        private static byte[] SlotKey(BigInteger slot)
            => HashProvider.ComputeHash(slot.ToByteArray(isUnsigned: true, isBigEndian: true).PadTo32Bytes());

        private void PutSlot(BigInteger slot, BigInteger value)
            => _storage.Put(SlotKey(slot), AccountStorage.EncodeValueForStorage(value.ToByteArray(isUnsigned: true, isBigEndian: true)));

        private static BigInteger MappingSlot(string mappingKey, int baseSlot)
        {
            var keyBytes = mappingKey.HexToByteArray().PadTo32Bytes();
            var baseSlotBytes = new BigInteger(baseSlot).ToByteArray(isUnsigned: true, isBigEndian: true).PadTo32Bytes();
            var hash = HashProvider.ComputeHash(keyBytes.Concat(baseSlotBytes).ToArray());
            return new BigInteger(hash, isUnsigned: true, isBigEndian: true);
        }

        private VerifiedStateService Service()
            => new VerifiedStateService(new FixedHeader(_state.Root.GetHash()), new GethLikeProofSource(_state, _storage, _contract), new NoCode(), new TrieProofVerifier());

        [Fact]
        public async Task Given_ASlotOf0x100_When_TheVerifiedStorageIsReadByItsBigIntegerPosition_Then_TheValueStoredAtSlot0x100IsReturnedNotSlot1()
        {
            var value = await Service().GetStorageAtAsync(Contract, new BigInteger(256));

            Assert.Equal(new BigInteger(111), value.ToHex().HexToBigInteger(false));
        }

        [Fact]
        public async Task Given_ASlotOf0x0102_When_TheVerifiedStorageIsReadByItsBigIntegerPosition_Then_TheValueStoredAtSlot0x0102IsReturnedNotSlot0x0201()
        {
            var value = await Service().GetStorageAtAsync(Contract, new BigInteger(258));

            Assert.Equal(new BigInteger(222), value.ToHex().HexToBigInteger(false));
        }

        [Fact]
        public async Task Given_AKeccakMappingSlot_When_TheVerifiedStorageIsReadByItsBigIntegerPosition_Then_TheValueStoredAtThatMappingSlotIsReturned()
        {
            var mappingSlot = MappingSlot(mappingKey: "0x00000000000000000000000000000000001234", baseSlot: 0);

            var value = await Service().GetStorageAtAsync(Contract, mappingSlot);

            Assert.Equal(new BigInteger(333), value.ToHex().HexToBigInteger(false));
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

        private sealed class NoCode : IEthGetCode
        {
            public BlockParameter DefaultBlock { get; set; } = BlockParameter.CreateLatest();
            public RpcRequest BuildRequest(string address, BlockParameter block, object id = null) => throw new NotImplementedException();
            public Task<string> SendRequestAsync(string address, object id = null) => throw new NotImplementedException();
            public Task<string> SendRequestAsync(string address, BlockParameter block, object id = null) => throw new NotImplementedException();
        }

        private sealed class GethLikeProofSource : IEthGetProof
        {
            private readonly PatriciaTrie _state;
            private readonly PatriciaTrie _storage;
            private readonly Account _account;

            public GethLikeProofSource(PatriciaTrie state, PatriciaTrie storage, Account account)
            {
                _state = state;
                _storage = storage;
                _account = account;
            }

            public BlockParameter DefaultBlock { get; set; } = BlockParameter.CreateLatest();

            public RpcRequest BuildRequest(string address, string[] storageKeys, BlockParameter block, object id = null) =>
                throw new NotImplementedException();

            public Task<AccountProof> SendRequestAsync(string address, string[] storageKeys, object id = null) =>
                SendRequestAsync(address, storageKeys, BlockParameter.CreateLatest(), id);

            public Task<AccountProof> SendRequestAsync(string address, string[] storageKeys, BlockParameter block, object id = null)
            {
                var proof = new AccountProof
                {
                    Address = address,
                    Balance = new HexBigInteger((BigInteger)_account.Balance),
                    Nonce = new HexBigInteger((BigInteger)_account.Nonce),
                    CodeHash = _account.CodeHash.ToHex(true),
                    StorageHash = _account.StateRoot.ToHex(true),
                    AccountProofs = ProofGenerator.GeneratePathProof(_state, AddressKey(address)).Select(n => n.ToHex(true)).ToList(),
                    StorageProof = new List<StorageProof>()
                };

                foreach (var storageKey in storageKeys)
                {
                    var slot = storageKey.HexToBigInteger(false);
                    var slotKey = SlotKey(slot);
                    var stored = _storage.Get(slotKey);
                    var value = stored == null
                        ? BigInteger.Zero
                        : Nethereum.RLP.RLP.Decode(stored).RLPData.ToBigIntegerFromRLPDecoded();

                    proof.StorageProof.Add(new StorageProof
                    {
                        Key = new HexBigInteger(slot),
                        Value = new HexBigInteger(value),
                        Proof = ProofGenerator.GeneratePathProof(_storage, slotKey).Select(n => n.ToHex(true)).ToList()
                    });
                }

                return Task.FromResult(proof);
            }
        }
    }
}
