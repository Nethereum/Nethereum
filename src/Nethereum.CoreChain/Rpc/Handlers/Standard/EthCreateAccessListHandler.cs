using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.EVM.Gas;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Model;
using Nethereum.RPC;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class EthCreateAccessListHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.eth_createAccessList.ToString();

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var callInput = GetParam<CallInput>(request, 0);
            var blockTag = GetOptionalParam<string>(request, 1, "latest");

            var blockNumber = await ResolveBlockNumberAsync(blockTag, context);

            BigInteger? gas = callInput.Gas?.Value;
            BigInteger? value = callInput.Value?.Value;

            var dataBytes = callInput.Data?.HexToByteArray();

            var result = await context.Node.CreateAccessListAsync(
                callInput.To,
                dataBytes,
                blockNumber,
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
                GasUsed = new Nethereum.Hex.HexTypes.HexBigInteger(
                    await GasUsedWithAccessListAsync(context, callInput, dataBytes, value, result, blockNumber)),
                Error = string.IsNullOrEmpty(result.Error) ? null : result.Error
            };

            return Success(request.Id, response);
        }

        private static async Task<BigInteger> GasUsedWithAccessListAsync(
            RpcContext context, CallInput callInput, byte[] dataBytes, BigInteger? value,
            AccessListResult result, BigInteger blockNumber)
        {
            var gasRules = await context.ResolveGasRulesAtBlockOrHeadAsync(blockNumber);
            var isContractCreation = SignedTransactionExtensions.IsContractCreationRecipient(callInput.To);
            var isSelfTransfer = !isContractCreation && callInput.From.IsTheSameAddress(callInput.To);
            var hasValue = value.HasValue && value.Value > 0;
            var accessList = ToAccessListEntries(result.AccessList);

            var intrinsicWithList = gasRules.CalculateIntrinsicGas(
                dataBytes, isContractCreation, accessList, isSelfTransfer, hasValue);

            var baseIntrinsic = gasRules.CalculateIntrinsicGas(
                dataBytes, isContractCreation, (List<AccessListEntry>)null, isSelfTransfer, hasValue);
            var accessListSurcharge = intrinsicWithList - baseIntrinsic;

            var floorWithList = (BigInteger)gasRules.CalculateFloorGasLimit(
                dataBytes, isContractCreation, isSelfTransfer, hasValue, accessList);

            return BigInteger.Max(result.GasUsed + accessListSurcharge, floorWithList);
        }

        private static List<AccessListEntry> ToAccessListEntries(List<AccessListItem> accessList)
        {
            if (accessList == null) return null;
            return accessList.Select(item => new AccessListEntry
            {
                Address = item.Address,
                StorageKeys = item.StorageKeys?.Select(k => k.ToHex(true)).ToList()
            }).ToList();
        }
    }
}
