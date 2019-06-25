using System;
using Nethereum.Util;

namespace Nethereum.RPC.Eth.DTOs
{

    public static class TransactionExtensions
    {
        public static bool IsToAnEmptyAddress(this Transaction txn)
        {
            return txn.To.IsAnEmptyAddress();
        }

        public static bool IsToOrEmpty(this Transaction txn, string address)
        {
            return txn.To.IsEmptyOrEqualsAddress(address);
        }

        public static bool IsTo(this Transaction txn, string address)
        {
            return txn.To.IsTheSameAddress(address);
        }

        public static bool IsFrom(this Transaction txn, string address)
        {
            return txn.From.IsTheSameAddress(address);
        }

        public static bool IsFromAndTo(this Transaction txn, string from, string to)
        {
            return txn.IsFrom(from) && txn.IsTo(to);
        }

        public static bool IsForContractCreation(
            this Transaction transaction, TransactionReceipt transactionReceipt)
        {
            return transaction.To.IsAnEmptyAddress() &&
                   transactionReceipt.ContractAddress.IsAnEmptyAddress();
        }

    }

}
