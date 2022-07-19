using System.Collections.Generic;
using Nethereum.Contracts.Standards.ERC721.ContractDefinition;
using System.Numerics;
using System.Collections;
using System;
using UnityEngine.Networking;
using Nethereum.Unity.Rpc;
using Newtonsoft.Json;

namespace Nethereum.Unity.Contracts.Standards.ERC721
{

    public class NFTsOfUserUnityRequest : UnityRequest<List<string>>
    {

        private QueryUnityRequest<BalanceOfFunction, BalanceOfOutputDTO> _balanceOfFunction;
        private QueryUnityRequest<TokenOfOwnerByIndexFunction, TokenOfOwnerByIndexOutputDTO> _tokenOfOwnerByIndexFunction;
        private QueryUnityRequest<TokenURIFunction, TokenURIOutputDTO> _tokenUriFunction;

        public NFTsOfUserUnityRequest(string url, string defaultAccount, JsonSerializerSettings jsonSerializerSettings = null, Dictionary<string, string> requestHeaders = null)
        {
            _balanceOfFunction = new QueryUnityRequest<BalanceOfFunction, BalanceOfOutputDTO>(url, defaultAccount, jsonSerializerSettings, requestHeaders);
            _tokenOfOwnerByIndexFunction = new QueryUnityRequest<TokenOfOwnerByIndexFunction, TokenOfOwnerByIndexOutputDTO>(url, defaultAccount, jsonSerializerSettings, requestHeaders);
            _tokenUriFunction = new QueryUnityRequest<TokenURIFunction, TokenURIOutputDTO>(url, defaultAccount, jsonSerializerSettings, requestHeaders);
        }

        public NFTsOfUserUnityRequest(string defaultAccount, IUnityRpcRequestClientFactory unityRpcRequestClientFactory)
        {
            _balanceOfFunction = new QueryUnityRequest<BalanceOfFunction, BalanceOfOutputDTO>(unityRpcRequestClientFactory, defaultAccount);
            _tokenOfOwnerByIndexFunction = new QueryUnityRequest<TokenOfOwnerByIndexFunction, TokenOfOwnerByIndexOutputDTO>(unityRpcRequestClientFactory, defaultAccount);
            _tokenUriFunction = new QueryUnityRequest<TokenURIFunction, TokenURIOutputDTO>(unityRpcRequestClientFactory, defaultAccount);
        }

        public IEnumerator GetAllMetadataUrls(string contractAddress, string account)
        {
            _balanceOfFunction.Exception = null;

            yield return _balanceOfFunction.Query(new BalanceOfFunction() { Owner = account }, contractAddress);

            if (_balanceOfFunction.Exception != null)
            {
                Exception = _balanceOfFunction.Exception;
                yield break;
            }

            var nftsOwned = new List<BigInteger>();
            var balance = _balanceOfFunction.Result.ReturnValue1;
            //this needs a multicall option
            for (int i = 0; i < balance; i++)
            {
                _tokenOfOwnerByIndexFunction.Exception = null;
                yield return _tokenOfOwnerByIndexFunction.Query(new TokenOfOwnerByIndexFunction() { Owner = account, Index = i }, contractAddress);


                if (_tokenOfOwnerByIndexFunction.Exception != null)
                {
                    Exception = _tokenOfOwnerByIndexFunction.Exception;
                    yield break;
                }

                nftsOwned.Add(_tokenOfOwnerByIndexFunction.Result.ReturnValue1);
            }

            var result = new List<string>();
            foreach (var nftIndex in nftsOwned)
            {
                _tokenUriFunction.Exception = null;
                yield return _tokenUriFunction.Query(new TokenURIFunction() { TokenId = nftIndex }, contractAddress);


                if (_tokenUriFunction.Exception != null)
                {
                    Exception = _tokenUriFunction.Exception;
                    yield break;
                }

                result.Add(_tokenUriFunction.Result.ReturnValue1);
            }

            Result = result;
        }

    }
}
