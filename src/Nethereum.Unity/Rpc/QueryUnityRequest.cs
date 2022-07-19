using Nethereum.RPC.Eth.DTOs;
using System.Collections;
using System.Collections.Generic;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Contracts.CQS;
using Nethereum.Contracts.Extensions;
using Nethereum.Contracts.Standards.ERC1155.ContractDefinition;
using System.Numerics;
using Newtonsoft.Json;
using System;
using Nethereum.Signer;
using Nethereum.Unity.Contracts;

namespace Nethereum.Unity.Rpc
{

    public class QueryUnityRequest<TFunctionMessage, TResponse> :
        UnityRequest<TResponse>,
        IContractQueryUnityRequest<TFunctionMessage, TResponse>
        where TFunctionMessage : FunctionMessage, new()
        where TResponse : IFunctionOutputDTO, new()

    {
        private readonly EthCallUnityRequest _ethCallUnityRequest;
        public string DefaultAccount { get; set; }

        public QueryUnityRequest(string url, string defaultAccount, JsonSerializerSettings jsonSerializerSettings = null, Dictionary<string, string> requestHeaders = null)
        {
            DefaultAccount = defaultAccount;
            _ethCallUnityRequest = new EthCallUnityRequest(url, jsonSerializerSettings, requestHeaders);
        }

        public QueryUnityRequest(IUnityRpcRequestClientFactory unityRpcClientFactory, string defaultAccount)
        {
            DefaultAccount = defaultAccount;
            _ethCallUnityRequest = new EthCallUnityRequest(unityRpcClientFactory);
        }

        public IEnumerator Query(TFunctionMessage functionMessage, string contractAddress,
            BlockParameter blockParameter = null)
        {
            if (blockParameter == null) blockParameter = BlockParameter.CreateLatest();

            functionMessage.SetDefaultFromAddressIfNotSet(DefaultAccount);
            var callInput = functionMessage.CreateCallInput(contractAddress);

            yield return _ethCallUnityRequest.SendRequest(callInput, blockParameter);

            if (_ethCallUnityRequest.Exception == null)
            {
                var result = new TResponse();
                Result = result.DecodeOutput(_ethCallUnityRequest.Result);
            }
            else
            {
                Exception = _ethCallUnityRequest.Exception;
                yield break;
            }
        }

        public IEnumerator Query(string contractAddress,
            BlockParameter blockParameter = null)
        {
            yield return Query(new TFunctionMessage(), contractAddress, blockParameter);
        }
    }
}
