using System.Collections.Generic;
using System.Linq;
using Nethereum.EVM.Types;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;

namespace Nethereum.EVM.Compatibility
{
    public static class EvmTypeConversions
    {
        public static EvmCallContext ToEvmCallContext(this CallInput input)
        {
            var ctx = new EvmCallContext
            {
                From = input.From,
                To = input.To,
                Data = input.Data != null ? input.Data.HexToByteArray() : new byte[0],
                Value = input.Value != null ? EvmUInt256BigIntegerExtensions.FromBigInteger(input.Value.Value) : EvmUInt256.Zero,
                Gas = input.Gas != null ? (long)input.Gas.Value : 10_000_000,
                GasPrice = input.GasPrice != null ? EvmUInt256BigIntegerExtensions.FromBigInteger(input.GasPrice.Value) : EvmUInt256.Zero,
                ChainId = input.ChainId != null ? EvmUInt256BigIntegerExtensions.FromBigInteger(input.ChainId.Value) : EvmUInt256.Zero,
                MaxFeePerGas = input.MaxFeePerGas != null ? EvmUInt256BigIntegerExtensions.FromBigInteger(input.MaxFeePerGas.Value) : EvmUInt256.Zero,
                MaxPriorityFeePerGas = input.MaxPriorityFeePerGas != null ? EvmUInt256BigIntegerExtensions.FromBigInteger(input.MaxPriorityFeePerGas.Value) : EvmUInt256.Zero,
            };
            return ctx;
        }

        public static EvmCallContext ToEvmCallContext(this TransactionInput input)
        {
            var ctx = ((CallInput)input).ToEvmCallContext();
            ctx.Nonce = input.Nonce != null ? (ulong)input.Nonce.Value : 0;
            return ctx;
        }

        public static FilterLog ToFilterLog(this EvmLog log)
        {
            return new FilterLog
            {
                Address = log.Address,
                Data = log.Data,
                LogIndex = new HexBigInteger(log.LogIndex),
                Topics = log.Topics != null ? log.Topics.Cast<object>().ToArray() : new object[0],
            };
        }

        public static CallInput ToCallInput(this EvmCallContext ctx)
        {
            return new CallInput
            {
                From = ctx.From,
                To = ctx.To,
                Data = ctx.Data != null ? ctx.Data.ToHex(true) : "0x",
                Value = new HexBigInteger(ctx.Value.ToBigInteger()),
                Gas = new HexBigInteger(ctx.Gas),
                GasPrice = new HexBigInteger(ctx.GasPrice.ToBigInteger()),
                ChainId = new HexBigInteger(ctx.ChainId.ToBigInteger()),
                MaxFeePerGas = new HexBigInteger(ctx.MaxFeePerGas.ToBigInteger()),
                MaxPriorityFeePerGas = new HexBigInteger(ctx.MaxPriorityFeePerGas.ToBigInteger()),
            };
        }

        public static List<FilterLog> ToFilterLogs(this List<EvmLog> logs)
        {
            if (logs == null) return new List<FilterLog>();
            var result = new List<FilterLog>(logs.Count);
            for (var i = 0; i < logs.Count; i++)
                result.Add(logs[i].ToFilterLog());
            return result;
        }

        public static List<CallInput> ToCallInputs(this List<EvmCallContext> calls)
        {
            if (calls == null) return new List<CallInput>();
            var result = new List<CallInput>(calls.Count);
            for (var i = 0; i < calls.Count; i++)
                result.Add(calls[i].ToCallInput());
            return result;
        }
    }
}
