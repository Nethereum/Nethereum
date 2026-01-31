using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class EthCreateAccessListHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.eth_createAccessList.ToString();

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var callInput = GetParam<CallInput>(request, 0);
            var blockTag = GetOptionalParam<string>(request, 1, "latest");

            ValidateBlockParameterIsLatest(blockTag, MethodName);

            BigInteger? gas = callInput.Gas?.Value;
            BigInteger? value = callInput.Value?.Value;

            var result = await context.Node.CreateAccessListAsync(
                callInput.To,
                callInput.Data?.HexToByteArray(),
                callInput.From,
                value,
                gas
            );

            var accessListDto = result.AccessList.Select(item => new AccessList
            {
                Address = item.Address,
                StorageKeys = item.StorageKeys?.Select(k => k.ToHex(true)).ToList() ?? new List<string>()
            }).ToList();

            var response = new AccessListGasUsed
            {
                AccessList = accessListDto,
                GasUsed = new Nethereum.Hex.HexTypes.HexBigInteger(result.GasUsed),
                Error = result.Error
            };

            return Success(request.Id, response);
        }
    }
}
