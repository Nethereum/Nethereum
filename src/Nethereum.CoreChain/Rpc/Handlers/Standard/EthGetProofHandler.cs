using System.Collections.Generic;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using Nethereum.CoreChain.Services;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC;
using Nethereum.RPC.Eth.DTOs;
using Newtonsoft.Json.Linq;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class EthGetProofHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.eth_getProof.ToString();

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var address = GetParam<string>(request, 0);
            var storageKeys = ParseStorageKeys(request);
            var blockParameter = GetOptionalParam<string>(request, 2, BlockParameter.BlockParameterType.latest.ToString());

            var block = await ResolveBlockAsync(blockParameter, context);
            if (block == null)
            {
                return Error(request.Id, -32602, "Block not found");
            }

            var proofService = new ProofService(context.Node.State, context.Node.TrieNodes);
            var proof = await proofService.GenerateAccountProofAsync(address, storageKeys, block.StateRoot);

            return Success(request.Id, proof);
        }

        private List<BigInteger> ParseStorageKeys(RpcRequestMessage request)
        {
            var keys = new List<BigInteger>();
            var rawParams = request.RawParameters;

            if (rawParams == null) return keys;

            if (rawParams is object[] array && array.Length > 1)
            {
                var keysParam = array[1];
                if (keysParam is object[] objArray)
                {
                    foreach (var item in objArray)
                    {
                        var keyStr = item?.ToString();
                        if (!string.IsNullOrEmpty(keyStr))
                        {
                            keys.Add(keyStr.HexToBigInteger(false));
                        }
                    }
                }
                else if (keysParam is JsonElement je && je.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in je.EnumerateArray())
                    {
                        var keyStr = item.GetString();
                        if (!string.IsNullOrEmpty(keyStr))
                        {
                            keys.Add(keyStr.HexToBigInteger(false));
                        }
                    }
                }
                else if (keysParam is JArray jArray)
                {
                    foreach (var item in jArray)
                    {
                        var keyStr = item?.ToString();
                        if (!string.IsNullOrEmpty(keyStr))
                        {
                            keys.Add(keyStr.HexToBigInteger(false));
                        }
                    }
                }
            }
            else if (rawParams is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
            {
                if (jsonElement.GetArrayLength() > 1)
                {
                    var keysElement = jsonElement[1];
                    if (keysElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in keysElement.EnumerateArray())
                        {
                            var keyStr = item.GetString();
                            if (!string.IsNullOrEmpty(keyStr))
                            {
                                keys.Add(keyStr.HexToBigInteger(false));
                            }
                        }
                    }
                }
            }

            return keys;
        }

        private async Task<Nethereum.Model.BlockHeader> ResolveBlockAsync(string blockParameter, RpcContext context)
        {
            if (blockParameter == BlockParameter.BlockParameterType.latest.ToString() || blockParameter == BlockParameter.BlockParameterType.pending.ToString())
            {
                return await context.Node.GetLatestBlockAsync();
            }

            if (blockParameter == BlockParameter.BlockParameterType.earliest.ToString())
            {
                return await context.Node.GetBlockByNumberAsync(0);
            }

            if (blockParameter.StartsWith("0x"))
            {
                var blockNumber = blockParameter.HexToBigInteger(false);
                return await context.Node.GetBlockByNumberAsync(blockNumber);
            }

            return await context.Node.GetLatestBlockAsync();
        }
    }
}
