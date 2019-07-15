using Nethereum.ABI;
using Nethereum.Hex.HexTypes;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

namespace Nethereum.Geth.VMStackParsing
{
    public static class VmStackExtensions
    {
        private static readonly JArray EmptyJArray = new JArray();
        private static readonly AddressType AddressType = new AddressType();

        public static IEnumerable<StructLog> GetInterContractCalls(this JObject stackTrace, string contractAddress)
        {
            return stackTrace.GetStructLogs().GetInterContractCalls(contractAddress);
        }

        public static JArray GetStructLogs(this JObject stackTrace)
        {
            if (stackTrace == null) return EmptyJArray;

            if (stackTrace["structLogs"] is JArray structLogs)
            {
                return structLogs;
            }

            return EmptyJArray;
        }

        public static IEnumerable<StructLog> GetInterContractCalls(this JArray structLogs, string contractAddress)
        {
            var addressStack = new Stack<string>();
            int currentDepth = 1;

            for (int i = 0; i < structLogs.Count; i++)
            {
                var l = structLogs[i];

                var depth = l["depth"].Value<int>();

                if (depth < currentDepth)
                {
                    addressStack.Pop();
                    currentDepth = depth;
                }

                if (addressStack.Count == 0)
                {
                    addressStack.Push(contractAddress);
                }

                if (l["op"].Value<string>() == OpCodes.SelfDestruct)
                {
                    var destructedAddress = addressStack.Pop();
                    var selfDestructLog = l.ToStructLog(structLogs, i, addressStack.Peek());
                    selfDestructLog.To = destructedAddress;
                    yield return selfDestructLog;
                }

                if(OpCodes.IsInterContract(l["op"]?.Value<string>()))
                {
                    var log = l.ToStructLog(structLogs, i, addressStack.Peek());
                    addressStack.Push(log.To);
                    currentDepth++;
                    yield return log;
                }
            }
        }

        private static StructLog ToStructLog(this JToken token, JArray structLogs, int currentIndex, string callingAddress)
        {
            var stackArray = token["stack"] as JArray;

            return new StructLog
            {
                From = callingAddress,
                Op = token["op"]?.Value<string>(),
                Depth = token["depth"].Value<uint>(),
                Gas = token["gas"]?.ToBigInteger(),
                GasCost = token["gasCost"]?.ToBigInteger(),

                StackGas = stackArray?[stackArray.Count - 1].ToBigInteger(),
                To = stackArray?[stackArray.Count - 2].ConvertStructLogValueToAddress(),
                StackEtherValue = stackArray?[stackArray.Count - 3].ToBigInteger()
            }
                .Populate(structLogs, currentIndex);
        }

        private static HexBigInteger ToBigInteger(this JToken token)
        {
            return new HexBigInteger(token.Value<string>());
        }

        private static string ConvertStructLogValueToAddress(this JToken token)
        {
            var rawValue = token.Value<string>();
            return AddressType.Decode(rawValue, typeof(string)) as string;
        }

        private static int? FirstIndexOf(this JArray structArray, string opCode, uint depth, int startingIndex)
        {
            for (int i = startingIndex + 1; i < structArray.Count; i++)
            {
                var structLog = structArray[i];
                if (structLog["op"].Value<string>() == opCode && structLog["depth"].Value<uint>() == depth)
                {
                    return i;
                }
            }

            return null;
        }

        private static StructLog Populate(this StructLog structLog, JArray structLogArray, int currentIndex)
        {
            if (structLog.Op == OpCodes.Create)
            {
                structLog.To = structLog.FindAddressOfNewlyCreatedContract(structLogArray, currentIndex);   
            }

            return structLog;
        }

        private static string FindAddressOfNewlyCreatedContract(this StructLog createLog, JArray structLogArray, int currentIndex)
        {
            var requiredDepth = createLog.Depth + 1;

            var indexOfNextReturnNode = structLogArray.FirstIndexOf(OpCodes.Return, requiredDepth, currentIndex);
            if (indexOfNextReturnNode != null)
            {
                var indexOfNextNode = indexOfNextReturnNode + 1;

                if (indexOfNextNode < structLogArray.Count)
                {
                    var nextLog = structLogArray[indexOfNextNode];
                    if (nextLog["stack"] is JArray stack)
                    {
                        return stack.LastOrDefault().ConvertStructLogValueToAddress();
                    }
                }
            }

            return null;
        }

    }
}
