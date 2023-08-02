using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.ABI.Model;
using Nethereum.Contracts.Constants;
using Nethereum.Contracts.Services;
using Nethereum.Contracts.Standards.ERC721.ContractDefinition;

namespace Nethereum.Contracts.Standards.ERC721
{
    public class ERC721Service
    {
        private readonly IEthApiContractService _ethApiContractService;

        public ERC721Service(IEthApiContractService ethApiContractService)
        {
            _ethApiContractService = ethApiContractService;
        }

        public ERC721ContractService GetContractService(string contractAddress)
        {
            return new ERC721ContractService(_ethApiContractService, contractAddress);
        }

        public List<FunctionABI> GetRequiredFunctionAbis()
        {
            var signatures = new List<FunctionABI>
            {
                ABITypedRegistry.GetFunctionABI<OwnerOfFunction>(),
                ABITypedRegistry.GetFunctionABI<BalanceOfFunction>(),
                ABITypedRegistry.GetFunctionABI<TransferFromFunction>(),
                ABITypedRegistry.GetFunctionABI<SafeTransferFromFunction>(),
                ABITypedRegistry.GetFunctionABI<SafeTransferFrom1Function>(),
                ABITypedRegistry.GetFunctionABI<SetApprovalForAllFunction>(),
                ABITypedRegistry.GetFunctionABI<ApproveFunction>(),
                ABITypedRegistry.GetFunctionABI<IsApprovedForAllFunction>(),
                ABITypedRegistry.GetFunctionABI<SupportsInterfaceFunction>()
            };
            return signatures;
        }

        public string[] GetRequiredFunctionSignatures()
        {
            return GetRequiredFunctionAbis().Select(x => x.Sha3Signature).ToArray();
        }

        public List<FunctionABI> GetOptionalMetadataExtensionsFunctionAbis()
        {
            var signatures = new List<FunctionABI>
            {
                ABITypedRegistry.GetFunctionABI<NameFunction>(),
                ABITypedRegistry.GetFunctionABI<SymbolFunction>(),
                ABITypedRegistry.GetFunctionABI<TokenURIFunction>(),
            };
            return signatures;
        }

        public List<FunctionABI> GetOptionalEnumerableExtensionFunctionAbis()
        {
            var signatures = new List<FunctionABI>
            {
                ABITypedRegistry.GetFunctionABI<TotalSupplyFunction>(),
                ABITypedRegistry.GetFunctionABI<TokenByIndexFunction>(),
                ABITypedRegistry.GetFunctionABI<TokenOfOwnerByIndexFunction>(),
            };
            return signatures;
        }

        public string[] GetOptionalMetadataFunctionSignatures()
        {
            return GetOptionalMetadataExtensionsFunctionAbis().Select(x => x.Sha3Signature).ToArray();
        }

        public string[] GetOptionalEnumerableExtensionFunctionSignatures()
        {
            return GetOptionalEnumerableExtensionFunctionAbis().Select(x => x.Sha3Signature).ToArray();
        }

#if !DOTNET35
        public async Task<List<ERC721TokenOwnerInfo>> GetAllTokenIdsOfOwnerUsingTokenOfOwnerByIndexAndMultiCallAsync(string ownerAddress, string[] contractAddresses, string multiCallAddress = CommonAddresses.MULTICALL_ADDRESS)
        {
            var balanceCalls = new List<MulticallInputOutput<BalanceOfFunction, BalanceOfOutputDTO>>();
            foreach (var contractAddress in contractAddresses)
            {
                var balanceCall = new BalanceOfFunction() {Owner = ownerAddress};
                balanceCalls.Add(new MulticallInputOutput<BalanceOfFunction, BalanceOfOutputDTO>(balanceCall,
                    contractAddress));
            }

            var multiqueryHandler = this._ethApiContractService.GetMultiQueryHandler(multiCallAddress);
            var results = await multiqueryHandler.MultiCallAsync(balanceCalls.ToArray()).ConfigureAwait(false);

            var contractsWithBalance = balanceCalls.Where(x => x.Output.ReturnValue1 > 0).ToArray();
            
            var calls = new List<MulticallInputOutput<TokenOfOwnerByIndexFunction, TokenOfOwnerByIndexOutputDTO>>();

            foreach (var contractWithBalance in contractsWithBalance)
            {
                var balance = contractWithBalance.Output.ReturnValue1;
                for (int i = 0; i < balance; i++)
                {
                    var tokenOfOwnerByIndex = new TokenOfOwnerByIndexFunction()
                        { Owner = ownerAddress, Index = i };
                    calls.Add(new MulticallInputOutput<TokenOfOwnerByIndexFunction, TokenOfOwnerByIndexOutputDTO>(tokenOfOwnerByIndex,
                        contractWithBalance.Target));
                }
            } 
          
            var resultsTokens  = await multiqueryHandler.MultiCallAsync(calls.ToArray()).ConfigureAwait(false);
            return calls.Select(x => new ERC721TokenOwnerInfo()
            {
                TokenId = x.Output.ReturnValue1,
                ContractAddress = x.Target,
                Owner = ownerAddress
            }).ToList();
        }

        public async Task<List<ERC721TokenOwnerInfo>> GetAllTokenUrlsOfOwnerUsingTokenOfOwnerByIndexAndMultiCallAsync(string ownerAddress, string[] contractAddresses, string multiCallAddress = CommonAddresses.MULTICALL_ADDRESS)
        {
            var ownerTokens = await GetAllTokenIdsOfOwnerUsingTokenOfOwnerByIndexAndMultiCallAsync(ownerAddress, contractAddresses, multiCallAddress).ConfigureAwait(false);
            var calls = new List<MulticallInputOutput<TokenURIFunction, TokenURIOutputDTO>>();
            foreach (var ownerToken in ownerTokens)
            {
                var tokenUri = new TokenURIFunction()
                    { TokenId = ownerToken.TokenId };
                calls.Add(new MulticallInputOutput<TokenURIFunction, TokenURIOutputDTO>(tokenUri,
                    ownerToken.ContractAddress));
            }
            var multiqueryHandler = this._ethApiContractService.GetMultiQueryHandler(multiCallAddress);
            var results = await multiqueryHandler.MultiCallAsync(calls.ToArray()).ConfigureAwait(false);
            return calls.Select(x => new ERC721TokenOwnerInfo() { TokenId = x.Input.TokenId, MetadataUrl = x.Output.ReturnValue1, ContractAddress = x.Target, Owner = ownerAddress }).ToList();
        }
#endif
    }
}