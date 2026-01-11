using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using System.Numerics;

namespace Nethereum.CoreChain.IntegrationTests.Contracts
{
    public static class SimpleStorageContract
    {
        // Simple storage contract bytecode
        // Solidity:
        // pragma solidity ^0.8.0;
        // contract SimpleStorage {
        //     uint256 public value;
        //     event ValueChanged(uint256 newValue);
        //     function setValue(uint256 _value) public {
        //         value = _value;
        //         emit ValueChanged(_value);
        //     }
        //     function getValue() public view returns (uint256) {
        //         return value;
        //     }
        // }
        public const string Bytecode = "608060405234801561001057600080fd5b50610150806100206000396000f3fe608060405234801561001057600080fd5b50600436106100415760003560e01c806320965255146100465780633fa4f24514610064578063552410771461006c575b600080fd5b61004e610088565b60405161005b9190610099565b60405180910390f35b61004e610091565b61008661007a3660046100b4565b60008190556040518181527f93fe6d397c74fdf1402a8b72e47b68512f0510d7b98a4bc4cbdf6ac7108b3c599060200160405180910390a150565b005b60005490565b60005481565b6000602082840312156100c657600080fd5b503591905056fea264697066735822122012";

        public static byte[] GetDeploymentBytecode()
        {
            return Nethereum.Hex.HexConvertors.Extensions.HexByteConvertorExtensions.HexToByteArray(Bytecode);
        }
    }

    [Function("getValue", "uint256")]
    public class GetValueFunction : FunctionMessage
    {
    }

    [Function("setValue")]
    public class SetValueFunction : FunctionMessage
    {
        [Parameter("uint256", "_value", 1)]
        public BigInteger Value { get; set; }
    }

    [Event("ValueChanged")]
    public class ValueChangedEvent : IEventDTO
    {
        [Parameter("uint256", "newValue", 1, false)]
        public BigInteger NewValue { get; set; }
    }
}
