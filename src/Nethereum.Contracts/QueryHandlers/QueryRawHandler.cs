using System.Threading.Tasks;
using Nethereum.Contracts.Services;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Contracts.QueryHandlers
{
#if !DOTNET35
    public class QueryRawHandler<TFunctionMessage> :
        QueryHandlerBase<TFunctionMessage>, IQueryHandler<TFunctionMessage, string> 
        where TFunctionMessage : FunctionMessage, new()
    {
        
        public QueryRawHandler(IClient client, string defaultAddressFrom):base(client, defaultAddressFrom)
        {
        }

        public QueryRawHandler(EthApiContractService eth):base(eth)
        {
            
        }

        public Task<string> QueryAsync(
            string contractAddress,
            TFunctionMessage contractFunctionMessage = null,
            BlockParameter block = null)
        {
            if (contractFunctionMessage == null) contractFunctionMessage = new TFunctionMessage();
            if (block == null) block = Eth.DefaultBlock;
            FunctionMessageEncodingService.SetContractAddress(contractAddress);
            EnsureInitialiseAddress();
            var callInput = FunctionMessageEncodingService.CreateCallInput(contractFunctionMessage);
            return Eth.Transactions.Call.SendRequestAsync(callInput, block);
        }
    }
#endif
}