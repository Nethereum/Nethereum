using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Proofs;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.RPC.Eth.ChainValidation;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.RPC.UnitTests.ChainValidation
{
    public class StorageProofKeyBindingTests
    {
        private const string Contract = "0x2222222222222222222222222222222222222222";
        private const string NeverUsed = "0x3333333333333333333333333333333333333333";
        private static readonly Sha3KeccackHashProvider HashProvider = Sha3KeccackHashProvider.Instance;
        private static readonly string ZeroHash = new byte[32].ToHex(true);

        private readonly PatriciaTrie _state;
        private readonly PatriciaTrie _storage;
        private readonly byte[] _stateRoot;
        private readonly Account _contract;

        public StorageProofKeyBindingTests()
        {
            _storage = new PatriciaTrie(new InMemoryContentNodeStore());
            PutSlot(2, 105);
            PutSlot(9, 5000);
            for (var i = 100; i < 140; i++) PutSlot(i, i);
            _storage.SaveNodesToStorage();

            _contract = new Account { Nonce = 1, Balance = 0, CodeHash = HashProvider.ComputeHash(new byte[] { 0x60, 0x00 }), StateRoot = _storage.Root.GetHash() };
            _state = new PatriciaTrie(new InMemoryContentNodeStore());
            _state.Put(AddressKey(Contract), AccountEncoder.Current.Encode(_contract));
            for (var i = 0; i < 64; i++)
                _state.Put(HashProvider.ComputeHash(new[] { (byte)i, (byte)0xCC }),
                           AccountEncoder.Current.Encode(new Account { Nonce = (ulong)i, CodeHash = DefaultValues.EMPTY_DATA_HASH, StateRoot = DefaultValues.EMPTY_TRIE_HASH }));
            _state.SaveNodesToStorage();
            _stateRoot = _state.Root.GetHash();
        }

        private void PutSlot(int slot, int value)
            => _storage.Put(HashProvider.ComputeHash(Slot32(slot)), AccountStorage.EncodeValueForStorage(new BigInteger(value).ToByteArray(isUnsigned: true, isBigEndian: true)));

        private static byte[] Slot32(int slot) => new BigInteger(slot).ToByteArray(isUnsigned: true, isBigEndian: true).PadTo32Bytes();

        private static byte[] AddressKey(string address) => HashProvider.ComputeHash(address.HexToByteArray());

        private static JArray Hex(IEnumerable<byte[]> nodes) => new JArray(nodes.Select(n => n.ToHex(true)));

        private JObject Response(string address, string codeHash, string storageHash, params (string key, string value, JArray proof)[] slots)
            => new JObject
            {
                ["address"] = address,
                ["balance"] = "0x0",
                ["nonce"] = address == Contract ? "0x1" : "0x0",
                ["codeHash"] = codeHash,
                ["storageHash"] = storageHash,
                ["accountProof"] = Hex(ProofGenerator.GeneratePathProof(_state, AddressKey(address))),
                ["storageProof"] = new JArray(slots.Select(slot => new JObject { ["key"] = slot.key, ["value"] = slot.value, ["proof"] = slot.proof }))
            };

        private JObject ContractResponse(params (string key, string value, JArray proof)[] slots)
            => Response(Contract, _contract.CodeHash.ToHex(true), _contract.StateRoot.ToHex(true), slots);

        private JArray StoragePath(byte[] slotKey) => Hex(ProofGenerator.GeneratePathProof(_storage, HashProvider.ComputeHash(slotKey)));

        private Task<byte[]> Read(JObject response, string address, string slot)
            => new EthChainProofValidationService(new ServesProof(response))
                .GetAndValidateValueFromStorage(address, slot, _stateRoot, new BlockParameter(7));

        [Fact]
        public async Task Given_TheRpcAnswersTheRequestedSlotWithAnotherSlotsValidProof_When_TheValidatedValueIsRead_Then_ItIsRejected()
        {
            var substituted = ContractResponse(("0x9", "0x1388", StoragePath(Slot32(9))));

            await Assert.ThrowsAsync<InvalidChainDataException>(() => Read(substituted, Contract, "0x2"));
        }

        [Fact]
        public async Task Given_TheRpcEchoesA33ByteSlotKeyThatIsNumericallyTheRequestedSlot_When_ItProvesThatKeyAbsent_Then_TheForgedZeroIsRejected()
        {
            var slot33 = new byte[33];
            slot33[32] = 0x02;
            var alias = ContractResponse((slot33.ToHex(true), "0x0", StoragePath(slot33)));

            await Assert.ThrowsAsync<InvalidChainDataException>(() => Read(alias, Contract, "0x2"));
        }

        [Fact]
        public async Task Given_TheRpcReturnsNoStorageEntry_When_TheValidatedValueIsRead_Then_InvalidChainDataIsThrown()
        {
            await Assert.ThrowsAsync<InvalidChainDataException>(() => Read(ContractResponse(), Contract, "0x2"));
        }

        [Fact]
        public async Task Given_HonestGethEchoesOfTheSlot_When_TheValidatedValueIsRead_Then_TheStoredValueIsReturned()
        {
            var quantity = await Read(ContractResponse(("0x2", "0x69", StoragePath(Slot32(2)))), Contract, "0x02");
            var full = await Read(ContractResponse((Slot32(2).ToHex(true), "0x69", StoragePath(Slot32(2)))), Contract, Slot32(2).ToHex(true));

            Assert.Equal(new BigInteger(105), quantity.ToHex().HexToBigInteger(false));
            Assert.Equal(new BigInteger(105), full.ToHex().HexToBigInteger(false));
        }

        [Fact]
        public async Task Given_AGethResponseForANeverUsedAddress_When_ASlotIsRead_Then_ZeroIsReturned()
        {
            var geth = Response(NeverUsed, ZeroHash, ZeroHash, ("0x2", "0x0", new JArray()));

            var value = await Read(geth, NeverUsed, "0x2");

            Assert.Equal(BigInteger.Zero, value.ToHex().HexToBigInteger(false));
        }

        private sealed class ServesProof : ClientBase
        {
            private readonly JObject _response;

            public ServesProof(JObject response) => _response = response;

            public override Task<RpcResponseMessage> SendAsync(RpcRequestMessage rpcRequestMessage, string route = null)
                => Task.FromResult(new RpcResponseMessage(rpcRequestMessage.Id, _response));

            protected override Task<RpcResponseMessage[]> SendAsync(RpcRequestMessage[] requests)
                => throw new System.NotSupportedException();
        }
    }
}
