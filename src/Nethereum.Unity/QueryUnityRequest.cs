using Nethereum.RPC.Eth.DTOs;
using System.Collections;
using Nethereum.Contracts;
using Nethereum.Contracts.CQS;

namespace Nethereum.JsonRpc.UnityClient
{
    public class QueryUnityRequest<TFunctionMessage, TResponse> : UnityRequest<TResponse>
        where TFunctionMessage : FunctionMessage, new()
        where TResponse : IFunctionOutputDTO, new()

    {
        private string _url;
        private readonly EthCallUnityRequest _ethCallUnityRequest;
        public string DefaultAccount { get; set; }

        public QueryUnityRequest(string url, string defaultAccount)
        {
            _url = url;
            DefaultAccount = defaultAccount;
            _ethCallUnityRequest = new EthCallUnityRequest(_url);
        }

        public IEnumerator Query(TFunctionMessage functionMessage, string contractAddress,
            BlockParameter blockParameter = null)
        {
            if(blockParameter == null) blockParameter = BlockParameter.CreateLatest();

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
                this.Exception = _ethCallUnityRequest.Exception;
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
