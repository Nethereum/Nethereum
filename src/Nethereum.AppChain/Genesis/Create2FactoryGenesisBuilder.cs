using System;
using System.Threading.Tasks;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;

namespace Nethereum.AppChain.Genesis
{
    public class Create2FactoryGenesisBuilder
    {
        public const string CREATE2_FACTORY_ADDRESS = SystemContractPredeploys.Create2FactoryAddress;

        public const string CREATE2_FACTORY_BYTECODE = SystemContractPredeploys.Create2FactoryRuntimeCode;

        private readonly IStateStore _stateStore;

        public Create2FactoryGenesisBuilder(IStateStore stateStore)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        }

        public Task DeployCreate2FactoryAsync()
        {
            return SystemContractPredeploys.AllocateIfAbsentAsync(
                _stateStore, SystemContractPredeploys.Create2Factory);
        }

        public static string CalculateCreate2Address(string deployerAddress, byte[] salt, byte[] initCode)
        {
            return ContractUtils
                .CalculateCreate2Address(deployerAddress, salt.ToHex(true), initCode.ToHex(true))
                .ToLowerInvariant();
        }

        public static string CalculateCreate2Address(string deployerAddress, string saltHex, byte[] initCode)
        {
            var salt = saltHex.HexToByteArray().PadBytes(32);
            return CalculateCreate2Address(deployerAddress, salt, initCode);
        }
    }
}
