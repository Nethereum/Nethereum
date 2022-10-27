using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.ABI.Model;
using Nethereum.Contracts.Constants;
using Nethereum.Contracts.ContractHandlers;
using Nethereum.Contracts.QueryHandlers.MultiCall;
using Nethereum.Contracts.Standards.ERC721.ContractDefinition;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Contracts.Standards.ERC721
{
    public partial class ERC721ContractService
    {


#if !DOTNET35


        public Task<bool> SupportsErc721InterfaceQueryAsync()
        {
            var supportsInterfaceFunction = new SupportsInterfaceFunction();
            supportsInterfaceFunction.InterfaceId = "0x80ac58cd".HexToByteArray();
            return ContractHandler.QueryAsync<SupportsInterfaceFunction, bool>(supportsInterfaceFunction);
        }

        public Task<bool> SupportsErc721MetadataInterfaceQueryAsync()
        {
            var supportsInterfaceFunction = new SupportsInterfaceFunction();
            supportsInterfaceFunction.InterfaceId = "0x5b5e139f".HexToByteArray();
            return ContractHandler.QueryAsync<SupportsInterfaceFunction, bool>(supportsInterfaceFunction);
        }

        public Task<bool> SupportsErc721EnumberableInterfaceQueryAsync()
        {
            var supportsInterfaceFunction = new SupportsInterfaceFunction();
            supportsInterfaceFunction.InterfaceId = "0x780e9d63".HexToByteArray();
            return ContractHandler.QueryAsync<SupportsInterfaceFunction, bool>(supportsInterfaceFunction);
        }

        public async Task<List<ERC721TokenOwnerInfo>> GetAllOwnersUsingTotalSupplyAndMultiCallAsync(int startTokenId = 0, int numberOfCallsPerRequest = MultiQueryHandler.DEFAULT_CALLS_PER_REQUEST, string multiCallAddress = CommonAddresses.MULTICALL_ADDRESS)
        {
            var totalSupply = await this.TotalSupplyQueryAsync().ConfigureAwait(false);
            return await GetAllOwnersUsingIdRangeAndMultiCallAsync(startTokenId, (int)totalSupply, numberOfCallsPerRequest, multiCallAddress).ConfigureAwait(false);

        }

        public async Task<List<ERC721TokenOwnerInfo>> GetAllOwnersUsingIdRangeAndMultiCallAsync(int startTokenId, int endTokenId, int numberOfCallsPerRequest = MultiQueryHandler.DEFAULT_CALLS_PER_REQUEST, string multiCallAddress = CommonAddresses.MULTICALL_ADDRESS)
        {

            var calls = new List<MulticallInputOutput<OwnerOfFunction, OwnerOfOutputDTO>>();
            for (int i = startTokenId; i <= endTokenId; i++)
            {
                var tokenOfOwnerByIndex = new OwnerOfFunction()
                { TokenId = i };
                calls.Add(new MulticallInputOutput<OwnerOfFunction, OwnerOfOutputDTO>(tokenOfOwnerByIndex,
                    ContractAddress));
            }
            var multiqueryHandler = this._ethApiContractService.GetMultiQueryHandler(multiCallAddress);
            var results = await multiqueryHandler.MultiCallAsync(numberOfCallsPerRequest, calls.ToArray()).ConfigureAwait(false);
            return calls.Select(x =>
                new ERC721TokenOwnerInfo() { ContractAddress = ContractAddress, Owner = x.Output.ReturnValue1, TokenId = x.Input.TokenId }
            ).ToList();
        }

        public async Task<List<ERC721TokenOwnerInfo>> GetAllMetadataUrlsUsingIdRangeAndMultiCallAsync(int startTokenId, int endTokenId, int numberOfCallsPerRequest = MultiQueryHandler.DEFAULT_CALLS_PER_REQUEST, string multiCallAddress = CommonAddresses.MULTICALL_ADDRESS)
        {

            var calls = new List<MulticallInputOutput<TokenURIFunction, TokenURIOutputDTO>>();
            for (int i = startTokenId; i <= endTokenId; i++)
            {
                var tokenUriFunction = new TokenURIFunction()
                { TokenId = i };
                calls.Add(new MulticallInputOutput<TokenURIFunction, TokenURIOutputDTO>(tokenUriFunction,
                    ContractAddress));
            }
            var multiqueryHandler = this._ethApiContractService.GetMultiQueryHandler(multiCallAddress);
            var results = await multiqueryHandler.MultiCallAsync(numberOfCallsPerRequest, calls.ToArray()).ConfigureAwait(false);
            return calls.Select(x => new ERC721TokenOwnerInfo() { ContractAddress = ContractAddress, MetadataUrl = x.Output.ReturnValue1, TokenId = x.Input.TokenId }).ToList();
        }

        public async Task<List<ERC721TokenOwnerInfo>> GetAllMetadataUrlsUsingMultiCallAsync(int[] tokenIds, int numberOfCallsPerRequest = MultiQueryHandler.DEFAULT_CALLS_PER_REQUEST, string multiCallAddress = CommonAddresses.MULTICALL_ADDRESS)
        {

            var calls = new List<MulticallInputOutput<TokenURIFunction, TokenURIOutputDTO>>();
            for (int i = 0; i < tokenIds.Length; i++)
            {
                var tokenUriFunction = new TokenURIFunction()
                { TokenId = tokenIds[i] };
                calls.Add(new MulticallInputOutput<TokenURIFunction, TokenURIOutputDTO>(tokenUriFunction,
                    ContractAddress));
            }
            var multiqueryHandler = this._ethApiContractService.GetMultiQueryHandler(multiCallAddress);
            var results = await multiqueryHandler.MultiCallAsync(numberOfCallsPerRequest, calls.ToArray()).ConfigureAwait(false);
            return calls.Select(x => new ERC721TokenOwnerInfo() { ContractAddress = ContractAddress, MetadataUrl = x.Output.ReturnValue1, TokenId = x.Input.TokenId }).ToList();
        }


        public async Task<List<BigInteger>> GetAllTokenIdsOfOwnerUsingTokenOfOwnerByIndexAndMultiCallAsync(string ownerAddress, int numberOfCallsPerRequest = MultiQueryHandler.DEFAULT_CALLS_PER_REQUEST, string multiCallAddress = CommonAddresses.MULTICALL_ADDRESS)
        {
            var balance = await this.BalanceOfQueryAsync(ownerAddress).ConfigureAwait(false);
            var calls = new List<MulticallInputOutput<TokenOfOwnerByIndexFunction, TokenOfOwnerByIndexOutputDTO>>();
            for (int i = 0; i < balance; i++)
            {
                var tokenOfOwnerByIndex = new TokenOfOwnerByIndexFunction()
                { Owner = ownerAddress, Index = i };
                calls.Add(new MulticallInputOutput<TokenOfOwnerByIndexFunction, TokenOfOwnerByIndexOutputDTO>(tokenOfOwnerByIndex,
                    ContractAddress));
            }
            var multiqueryHandler = this._ethApiContractService.GetMultiQueryHandler(multiCallAddress);
            var results = await multiqueryHandler.MultiCallAsync(numberOfCallsPerRequest, calls.ToArray()).ConfigureAwait(false);
            return calls.Select(x => x.Output.ReturnValue1).ToList();
        }

        public async Task<List<ERC721TokenOwnerInfo>> GetAllTokenUrlsOfOwnerUsingTokenOfOwnerByIndexAndMultiCallAsync(string ownerAddress, int numberOfCallsPerRequest = MultiQueryHandler.DEFAULT_CALLS_PER_REQUEST, string multiCallAddress = CommonAddresses.MULTICALL_ADDRESS)
        {
            var tokenIds = await GetAllTokenIdsOfOwnerUsingTokenOfOwnerByIndexAndMultiCallAsync(ownerAddress, numberOfCallsPerRequest, multiCallAddress).ConfigureAwait(false);
            var calls = new List<MulticallInputOutput<TokenURIFunction, TokenURIOutputDTO>>();
            foreach (var nftIndex in tokenIds)
            {
                var tokenUri = new TokenURIFunction()
                { TokenId = nftIndex };
                calls.Add(new MulticallInputOutput<TokenURIFunction, TokenURIOutputDTO>(tokenUri,
                    ContractAddress));
            }
            var multiqueryHandler = this._ethApiContractService.GetMultiQueryHandler(multiCallAddress);
            var results = await multiqueryHandler.MultiCallAsync(numberOfCallsPerRequest, calls.ToArray()).ConfigureAwait(false);
            return calls.Select(x => new ERC721TokenOwnerInfo() { TokenId = x.Input.TokenId, MetadataUrl = x.Output.ReturnValue1, ContractAddress = ContractAddress, Owner = ownerAddress }).ToList();
        }
#endif
    }
}
