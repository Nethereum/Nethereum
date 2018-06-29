using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Accounts;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Contracts.CQS
{

#if !DOTNET35
    public class ContractQueryHandler<TContractMessage> : ContractHandlerBase<TContractMessage>, IContractQueryHandler<TContractMessage> where TContractMessage : ContractMessage
    {
        private FunctionMessageEncodingService<TContractMessage> _functionMessageEncodingService = new FunctionMessageEncodingService<TContractMessage>();

        private string _defaultAddressFrom;

        public List<IQueryHandlerPreRequestHandler<TContractMessage>> QueryHandlerPreRequestHandlers { get; set; }

        public ContractQueryHandler()
        {
            
        }

        public ContractQueryHandler(IClient client, string defaultAddressFrom = null)
        {
            _defaultAddressFrom = defaultAddressFrom;
            Initialise(client, null);
        }

        public ContractQueryHandler(IClient client, IAccount account, IQueryHandlerPreRequestHandler<TContractMessage>[] preRequestHandlers = null)
        {
            QueryHandlerPreRequestHandlers = new List<IQueryHandlerPreRequestHandler<TContractMessage>>(preRequestHandlers);
            Initialise(client, account);
        }

        public override string GetAccountAddressFrom()
        {
            var address = base.GetAccountAddressFrom();
            if (address == null) return _defaultAddressFrom;
            return address;
        }

        public async Task<TFunctionOutput> QueryDeserializingToObjectAsync<TFunctionOutput>(
            TContractMessage contractFunctionMessage, string contractAddress,
            BlockParameter block = null) where TFunctionOutput : new()

        {
            var result = await QueryRawAsync(contractFunctionMessage, contractAddress, block).ConfigureAwait(false);
            return _functionMessageEncodingService.DecodeDTOTypeOutput<TFunctionOutput>(result);
        }

        public async Task<TFunctionOutput> QueryAsync<TFunctionOutput>(TContractMessage contractFunctionMessage,
            string contractAddress,
            BlockParameter block = null)
        {
            var result = await QueryRawAsync(contractFunctionMessage, contractAddress, block).ConfigureAwait(false);
            return _functionMessageEncodingService.DecodeSimpleTypeOutput<TFunctionOutput>(result);
        }

        public async Task<byte[]> QueryRawAsBytesAsync(TContractMessage contractFunctionMessage,
            string contractAddress,
            BlockParameter block = null)
        {
            var rawResult = await QueryRawAsync(contractFunctionMessage, contractAddress, block).ConfigureAwait(false);
            return rawResult.HexToByteArray();
        }

        public async Task<string> QueryRawAsync(TContractMessage contractFunctionMessage,
            string contractAddress,
            BlockParameter block = null)
        {
            _functionMessageEncodingService.SetContractAddress(contractAddress);
            _functionMessageEncodingService.DefaultAddressFrom = GetAccountAddressFrom();
            await ExecutePreRequestHandlersAsync(contractFunctionMessage, contractAddress, block).ConfigureAwait(false);
            var callInput = _functionMessageEncodingService.CreateCallInput(contractFunctionMessage);
            return await Eth.Transactions.Call.SendRequestAsync(callInput, block).ConfigureAwait(false);
        }

        protected async Task ExecutePreRequestHandlersAsync(TContractMessage contractFunctionMessage,
            string contractAddress,
            BlockParameter block = null)
        {
            foreach (var queryHandlerPreRequestHandler in QueryHandlerPreRequestHandlers)
            {
                await queryHandlerPreRequestHandler.ExecuteAsync(contractFunctionMessage, contractAddress, block).ConfigureAwait(false);
            }
        }
    }
#endif
}