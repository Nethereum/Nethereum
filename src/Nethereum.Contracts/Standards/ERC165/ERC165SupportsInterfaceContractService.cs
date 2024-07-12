using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.ABI.Model;
using Nethereum.Contracts.ContractHandlers;
using Nethereum.Contracts.Services;
using Nethereum.Hex.HexConvertors.Extensions;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Nethereum.Contracts.Standards.ERC165
{
    public class ERC165SupportsInterfaceContractService
    {

        public string ContractAddress { get; }

        public ContractHandler ContractHandler { get; }

        public ERC165SupportsInterfaceContractService(IEthApiContractService ethApiContractService, string contractAddress)
        {
            ContractAddress = contractAddress;
#if !DOTNET35
            ContractHandler = ethApiContractService.GetContractHandler(contractAddress);
#endif
        }

        public partial class SupportsInterfaceFunction : SupportsInterfaceFunctionBase { }

        [Function("supportsInterface", "bool")]
        public class SupportsInterfaceFunctionBase : FunctionMessage
        {
            [Parameter("bytes4", "interfaceId", 1)]
            public virtual byte[] InterfaceId { get; set; }
        }
#if !DOTNET35
        public Task<bool> SupportsInterfaceQueryAsync(string hexSignatureString)
        {
            if (hexSignatureString.IsHex() == false) throw new ArgumentException("The hexSignatureString should be a valid hex string");

            var supportsInterfaceFunction = new SupportsInterfaceFunction();
            supportsInterfaceFunction.InterfaceId = hexSignatureString.HexToByteArray();
            return ContractHandler.QueryAsync<SupportsInterfaceFunction, bool>(supportsInterfaceFunction);
        }

        public Task<bool> SupportsInterfaceQueryAsync(byte[] interfaceId)
        {
            if (interfaceId.Length != 4) throw new ArgumentException("The interfaceId should be 4 bytes long");
            var supportsInterfaceFunction = new SupportsInterfaceFunction();
            supportsInterfaceFunction.InterfaceId = interfaceId;
            return ContractHandler.QueryAsync<SupportsInterfaceFunction, bool>(supportsInterfaceFunction);
        }

        public Task<bool> SupportsInterfaceQueryAsync<TFunctionMessage>() where TFunctionMessage : FunctionMessage, new()
        {
            var abi = ABITypedRegistry.GetFunctionABI<TFunctionMessage>();
            return SupportsInterfaceQueryAsync(abi.Signature);
        }

        public Task<bool> SupportsInterfaceQueryAsync<TFunctionMessage>(TFunctionMessage functionMessage) where TFunctionMessage : FunctionMessage, new()
        {
            return SupportsInterfaceQueryAsync<TFunctionMessage>();
        }

        public Task<bool> SupportsInterfaceQueryAsync(FunctionABI functionABI)
        {
            return SupportsInterfaceQueryAsync(functionABI.Signature);
        }

#endif
    }
}
