using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Contracts.Constants;
using Nethereum.Contracts.ContractHandlers;
using Nethereum.Contracts.QueryHandlers.MultiCall;
using Nethereum.Contracts.Services;
using Nethereum.Contracts.Standards.ERC721.ContractDefinition;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Contracts.Standards.ERC721
{
    public partial class ERC721Service
    {

        public class TokenIdUrl
        {
            public BigInteger TokenId { get; set; }
            public string Url { get; set; }
        }

#if !DOTNET35
       public async Task<List<BigInteger>> GetAllTokenIdsOwnedByOwnerUsingMultiCallAsync(string ownerAddress, string multiCallAddress = CommonAddresses.MULTICALL_ADDRESS)
       {
           var balance = await this.BalanceOfQueryAsync(ownerAddress);
           var calls = new List<MulticallInputOutput<TokenOfOwnerByIndexFunction, TokenOfOwnerByIndexOutputDTO>>();
           for (int i = 0; i < balance; i++)
           {
               var tokenOfOwnerByIndex = new TokenOfOwnerByIndexFunction()
                   { Owner = ownerAddress, Index = i}; 
               calls.Add(new MulticallInputOutput<TokenOfOwnerByIndexFunction, TokenOfOwnerByIndexOutputDTO>(tokenOfOwnerByIndex,
                   ContractAddress));
           }
            var multiqueryHandler = this._ethApiContractService.GetMultiQueryHandler(multiCallAddress);
            var results = await multiqueryHandler.MultiCallAsync(calls.ToArray());
            return calls.Select(x => x.Output.ReturnValue1).ToList();
       }

       public async Task<List<TokenIdUrl>> GetAllTokenUrlsOwnedByOwnerUsingMultiCallAsync(string ownerAddress, string multiCallAddress = CommonAddresses.MULTICALL_ADDRESS)
       {
           var tokenIds = await GetAllTokenIdsOwnedByOwnerUsingMultiCallAsync(ownerAddress, multiCallAddress);
           var calls = new List<MulticallInputOutput<TokenURIFunction, TokenURIOutputDTO>>();
           foreach (var nftIndex in tokenIds)
           {
                var tokenUri = new TokenURIFunction()
                   { TokenId = nftIndex };
               calls.Add(new MulticallInputOutput<TokenURIFunction, TokenURIOutputDTO>(tokenUri,
                   ContractAddress));
           }
           var multiqueryHandler = this._ethApiContractService.GetMultiQueryHandler(multiCallAddress);
           var results = await multiqueryHandler.MultiCallAsync(calls.ToArray());
           return calls.Select(x => new TokenIdUrl(){TokenId = x.Input.TokenId, Url = x.Output.ReturnValue1}).ToList();
       }
#endif
    }
}
