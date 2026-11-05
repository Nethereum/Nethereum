using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Util;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class EthGetStorageValuesHandler : RpcHandlerBase
    {
        public override string MethodName => "eth_getStorageValues";

        private const int MaxGetStorageSlots = 1024;
        private const int ClientLimitExceeded = -38026;
        private const int KeyBytes = 32;

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var requests = GetJsonElement(request, 0);
            var blockTag = GetOptionalBlockParam(request, 1, "latest");

            if (requests.ValueKind != JsonValueKind.Object)
                throw RpcException.InvalidParams("requests must be an object of address to storage keys");

            var slotsByAddress = new List<KeyValuePair<string, List<string>>>();
            var totalSlots = 0;
            foreach (var entry in requests.EnumerateObject())
            {
                if (entry.Value.ValueKind != JsonValueKind.Array)
                    throw RpcException.InvalidParams($"storage keys of {entry.Name} must be an array");

                var keys = new List<string>();
                foreach (var key in entry.Value.EnumerateArray())
                {
                    var keyHex = key.RequireRpcString("storage key");
                    if (keyHex == null || keyHex.Length - 2 > KeyBytes * 2)
                        throw RpcException.InvalidParams($"storage key too long (want at most 32 bytes): \"{keyHex}\"");
                    keys.Add(keyHex);
                    if (++totalSlots > MaxGetStorageSlots)
                        throw new RpcException(ClientLimitExceeded, $"too many slots (max {MaxGetStorageSlots})");
                }
                slotsByAddress.Add(new KeyValuePair<string, List<string>>(entry.Name, keys));
            }

            if (totalSlots == 0)
                throw RpcException.InvalidParams("empty request");

            var blockNumber = await ResolveBlockNumberAsync(blockTag, context);

            var result = new Dictionary<string, string[]>();
            foreach (var (address, keys) in slotsByAddress)
            {
                var values = new string[keys.Count];
                for (var i = 0; i < keys.Count; i++)
                {
                    var value = await context.Node.GetStorageAtAsync(address, keys[i].HexToBigInteger(false), blockNumber);
                    values[i] = (value ?? new byte[0]).PadBytes(KeyBytes).ToHex(true);
                }
                result[address] = values;
            }

            return Success(request.Id, result);
        }
    }
}
