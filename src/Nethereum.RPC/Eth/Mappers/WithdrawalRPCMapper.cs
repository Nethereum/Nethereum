using System.Collections.Generic;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using ModelWithdrawal = Nethereum.Model.Withdrawal;

namespace Nethereum.RPC.Eth.Mappers
{
    public static class WithdrawalRPCMapper
    {
        public static ModelWithdrawal ToModelWithdrawal(this Withdrawal withdrawal)
        {
            return new ModelWithdrawal
            {
                Index = (ulong)withdrawal.Index.Value,
                ValidatorIndex = (ulong)withdrawal.ValidatorIndex.Value,
                Address = withdrawal.Address.HexToByteArray(),
                AmountInGwei = (ulong)withdrawal.Amount.Value
            };
        }

        public static List<ModelWithdrawal> ToModelWithdrawals(this IEnumerable<Withdrawal> withdrawals)
        {
            var result = new List<ModelWithdrawal>();
            if (withdrawals == null) return result;
            foreach (var withdrawal in withdrawals)
                result.Add(withdrawal.ToModelWithdrawal());
            return result;
        }
    }
}
