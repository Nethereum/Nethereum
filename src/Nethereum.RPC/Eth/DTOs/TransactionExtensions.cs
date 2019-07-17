using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.Util;
using Newtonsoft.Json.Linq;

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
                   transactionReceipt.ContractAddress.IsNotAnEmptyAddress();
        }

        public static bool CreatedContract(this TransactionReceiptVO transactionReceiptVO, string contractAddress)
        {
            return transactionReceiptVO.TransactionReceipt.IsContractAddressEqual(contractAddress);
        }

        public static IEnumerable<FilterLogVO> GetTransactionLogs(this Transaction transaction, TransactionReceipt receipt)
        {
            for (var i = 0; i < receipt.Logs?.Count; i++)
            {
                if (receipt.Logs[i] is JObject log)
                {
                    var typedLog = log.ToObject<FilterLog>();

                    yield return
                        new FilterLogVO(transaction, receipt, typedLog);
                }
            }
        }

        public static string[] GetAllRelatedAddresses(this Transaction tx, TransactionReceipt receipt)
        {
            if (tx == null)
                return new string[] { };

            var uniqueAddresses = new UniqueAddressList()
                {tx.From};

            if (tx.To.IsNotAnEmptyAddress())
                uniqueAddresses.Add(tx.To);

            if (receipt != null)
            {
                if (receipt.ContractAddress.IsNotAnEmptyAddress())
                    uniqueAddresses.Add(receipt.ContractAddress);

                foreach (var log in tx.GetTransactionLogs(receipt))
                {
                    if (log.Address.IsNotAnEmptyAddress())
                        uniqueAddresses.Add(log.Address);
                }
            }

            return uniqueAddresses.ToArray();

        }

    }

}
