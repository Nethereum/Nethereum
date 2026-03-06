using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.AppChain.Genesis
{
    public class Create2FactoryGenesisBuilder
    {
        public const string CREATE2_FACTORY_ADDRESS = "0x4e59b44847b379578588920cA78FbF26c0B4956C";

        public const string CREATE2_FACTORY_BYTECODE =
            "0x7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffe03601600081602082378035828234f58015156039578182fd5b8082525050506014600cf3";

        private readonly IStateStore _stateStore;
        private readonly Sha3Keccack _keccak = new Sha3Keccack();

        public Create2FactoryGenesisBuilder(IStateStore stateStore)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        }

        public async Task DeployCreate2FactoryAsync()
        {
            var bytecode = CREATE2_FACTORY_BYTECODE.HexToByteArray();
            var codeHash = _keccak.CalculateHash(bytecode);

            await _stateStore.SaveCodeAsync(codeHash, bytecode);

            var factoryAccount = new Account
            {
                Balance = BigInteger.Zero,
                Nonce = 1,
                CodeHash = codeHash
            };

            var normalizedAddress = AddressUtil.Current.ConvertToValid20ByteAddress(CREATE2_FACTORY_ADDRESS);
            await _stateStore.SaveAccountAsync(normalizedAddress, factoryAccount);
        }

        public static string CalculateCreate2Address(string deployerAddress, byte[] salt, byte[] initCode)
        {
            var keccak = new Sha3Keccack();

            var deployerBytes = deployerAddress.HexToByteArray();
            var initCodeHash = keccak.CalculateHash(initCode);

            var data = new byte[1 + 20 + 32 + 32];
            data[0] = 0xff;
            Array.Copy(deployerBytes, 0, data, 1, 20);
            Array.Copy(salt, 0, data, 21, 32);
            Array.Copy(initCodeHash, 0, data, 53, 32);

            var hash = keccak.CalculateHash(data);

            var addressBytes = new byte[20];
            Array.Copy(hash, 12, addressBytes, 0, 20);

            return "0x" + addressBytes.ToHex();
        }

        public static string CalculateCreate2Address(string deployerAddress, string saltHex, byte[] initCode)
        {
            var salt = saltHex.HexToByteArray().PadBytes(32);
            return CalculateCreate2Address(deployerAddress, salt, initCode);
        }
    }
}
