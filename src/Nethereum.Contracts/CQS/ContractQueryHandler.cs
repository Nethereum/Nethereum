using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.Contracts.CQS
{
#if !DOTNET35
    public class ContractQueryHandler<TFunctionDTO, TOutputDTO> : ContractHandler<TFunctionDTO>
        where TOutputDTO : new()
        where TFunctionDTO: ContractMessage
    {
        public async Task<TOutputDTO> ExecuteAsync(TFunctionDTO functionMessage,
                                                   BlockParameter block = null)
                                                          
        {
            ValidateFunctionDTO(functionMessage);
            var function = GetFunction();
            return await function.CallDeserializingToObjectAsync<TOutputDTO>(functionMessage, functionMessage.FromAddress,
                GetMaximumGas(functionMessage), GetValue(functionMessage), block).ConfigureAwait(false);
        }
    }
#endif
}
