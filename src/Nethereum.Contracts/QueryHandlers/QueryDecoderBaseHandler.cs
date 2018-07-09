using System.Threading.Tasks;
using Nethereum.Contracts.Services;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Transactions;

namespace Nethereum.Contracts.QueryHandlers
{
#if !DOTNET35
    public abstract class QueryDecoderBaseHandler<TFunctionMessage, TFunctionOutput> :
        IQueryHandler<TFunctionMessage, TFunctionOutput> 
        where TFunctionMessage : FunctionMessage, new()
    {
        protected QueryRawHandler<TFunctionMessage> QueryRawHandler { get; set; }

        public string DefaultAddressFrom
        {
            get => QueryRawHandler.DefaultAddressFrom;
            set => QueryRawHandler.DefaultAddressFrom = value;
        }

        public QueryDecoderBaseHandler(IClient client, string defaultAddressFrom = null, BlockParameter defaultBlockParameter = null) 
        {
            QueryRawHandler = new QueryRawHandler<TFunctionMessage>(client, defaultAddressFrom, defaultBlockParameter);
        }

        public QueryDecoderBaseHandler(EthCall ethCall, string defaultAddressFrom = null, BlockParameter defaultBlockParameter = null)
        {
            QueryRawHandler = new QueryRawHandler<TFunctionMessage>(ethCall, defaultAddressFrom, defaultBlockParameter);
        }

        public async Task<TFunctionOutput> QueryAsync(string contractAddress, TFunctionMessage functionMessage = null,  BlockParameter block = null)
        {
            var result = await QueryRawHandler.QueryAsync(contractAddress, functionMessage, block).ConfigureAwait(false);
            return DecodeOutput(result);
        }

        protected abstract TFunctionOutput DecodeOutput(string output);
    }
#endif
}