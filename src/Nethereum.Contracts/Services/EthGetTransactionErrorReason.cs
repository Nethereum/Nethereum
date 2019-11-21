using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Services;
using Nethereum.RPC.Eth.Transactions;

namespace Nethereum.Contracts.Services
{
    public class EthGetContractTransactionErrorReason: IEthGetContractTransactionErrorReason
    {
        private readonly IEthApiTransactionsService _apiTransactionsService;

        public EthGetContractTransactionErrorReason(IEthApiTransactionsService apiTransactionsService)
        {
            _apiTransactionsService = apiTransactionsService;
        }
#if !DOTNET35
        public async Task<string> SendRequestAsync(string transactionHash)
        {
            var transaction = await _apiTransactionsService.GetTransactionByHash.SendRequestAsync(transactionHash);
            var errorHex = await _apiTransactionsService.Call.SendRequestAsync(transaction.ConvertToTransactionInput(), new BlockParameter(transaction.BlockNumber));
            
            if (ErrorFunction.IsErrorData(errorHex))
            {
                return new FunctionCallDecoder().DecodeFunctionErrorMessage(errorHex);
            }
            return string.Empty;
        }
#endif
    }
}