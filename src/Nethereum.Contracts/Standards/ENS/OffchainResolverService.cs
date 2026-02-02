using System.Threading.Tasks;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts.ContractHandlers;
using Nethereum.Contracts.Standards.ENS.OffchainResolver.ContractDefinition;
using Nethereum.Contracts.Services;

namespace Nethereum.Contracts.Standards.ENS
{


    public partial class OffchainResolverService : IContractHandlerService
    {
        public string ContractAddress { get; }
        public ContractHandler ContractHandler { get; set; }
        public OffchainResolverService(IEthApiContractService ethApiContractService, string contractAddress)
        {
            ContractAddress = contractAddress;
#if !DOTNET35
            ContractHandler = ethApiContractService.GetContractHandler(contractAddress);
#endif
        }
#if !DOTNET35
        public Task<byte[]> MakeSignatureHashQueryAsync(MakeSignatureHashFunction makeSignatureHashFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MakeSignatureHashFunction, byte[]>(makeSignatureHashFunction, blockParameter);
        }


        public Task<byte[]> MakeSignatureHashQueryAsync(string target, ulong expires, byte[] request, byte[] result, BlockParameter blockParameter = null)
        {
            var makeSignatureHashFunction = new MakeSignatureHashFunction();
            makeSignatureHashFunction.Target = target;
            makeSignatureHashFunction.Expires = expires;
            makeSignatureHashFunction.Request = request;
            makeSignatureHashFunction.Result = result;

            return ContractHandler.QueryAsync<MakeSignatureHashFunction, byte[]>(makeSignatureHashFunction, blockParameter);
        }

        public Task<byte[]> ResolveQueryAsync(ResolveFunction resolveFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ResolveFunction, byte[]>(resolveFunction, blockParameter);
        }


        public Task<byte[]> ResolveQueryAsync(byte[] name, byte[] data, BlockParameter blockParameter = null)
        {
            var resolveFunction = new ResolveFunction();
            resolveFunction.Name = name;
            resolveFunction.Data = data;

            return ContractHandler.QueryAsync<ResolveFunction, byte[]>(resolveFunction, blockParameter);
        }

        public Task<byte[]> ResolveWithProofQueryAsync(ResolveWithProofFunction resolveWithProofFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ResolveWithProofFunction, byte[]>(resolveWithProofFunction, blockParameter);
        }


        public Task<byte[]> ResolveWithProofQueryAsync(byte[] response, byte[] extraData, BlockParameter blockParameter = null)
        {
            var resolveWithProofFunction = new ResolveWithProofFunction();
            resolveWithProofFunction.Response = response;
            resolveWithProofFunction.ExtraData = extraData;

            return ContractHandler.QueryAsync<ResolveWithProofFunction, byte[]>(resolveWithProofFunction, blockParameter);
        }

        public Task<bool> SignersQueryAsync(SignersFunction signersFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<SignersFunction, bool>(signersFunction, blockParameter);
        }


        public Task<bool> SignersQueryAsync(string returnValue1, BlockParameter blockParameter = null)
        {
            var signersFunction = new SignersFunction();
            signersFunction.ReturnValue1 = returnValue1;

            return ContractHandler.QueryAsync<SignersFunction, bool>(signersFunction, blockParameter);
        }

        public Task<bool> SupportsInterfaceQueryAsync(SupportsInterfaceFunction supportsInterfaceFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<SupportsInterfaceFunction, bool>(supportsInterfaceFunction, blockParameter);
        }


        public Task<bool> SupportsInterfaceQueryAsync(byte[] interfaceID, BlockParameter blockParameter = null)
        {
            var supportsInterfaceFunction = new SupportsInterfaceFunction();
            supportsInterfaceFunction.InterfaceID = interfaceID;

            return ContractHandler.QueryAsync<SupportsInterfaceFunction, bool>(supportsInterfaceFunction, blockParameter);
        }

        public Task<string> UrlQueryAsync(UrlFunction urlFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<UrlFunction, string>(urlFunction, blockParameter);
        }


        public Task<string> UrlQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<UrlFunction, string>(null, blockParameter);
        }
#endif
    }
}
