using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs;
using System.Collections.Generic;
using System.Numerics;

namespace Nethereum.RPC.Eth.Mappers
{
    public static class TransactionRPCMapper
    {
        public static IndexedSignedTransaction ToSignedTransaction(this Transaction transaction, BigInteger? chainId = null)
        {
            List<AccessListItem> accessList = transaction.AccessList.ToSignerAccessListItemArray();
                
            byte? type = null;
            if (transaction.Type != null)
            {
                type = (byte)transaction.Type.Value;
            }

            var signedTransaction =  TransactionFactory.CreateTransaction(chainId, type, transaction.Nonce.GetValue(), transaction.MaxPriorityFeePerGas.GetValue(), transaction.MaxFeePerGas.GetValue(), transaction.GasPrice.GetValue(), transaction.Gas.GetValue(), transaction.To, transaction.Value.GetValue(), transaction.Input, accessList, transaction.R, transaction.S, transaction.V);
            return new IndexedSignedTransaction() { Index = transaction.TransactionIndex, SignedTransaction = signedTransaction };
        }


        public static List<IndexedSignedTransaction> ToSignedTransactions(this IEnumerable<Transaction> transactions, BigInteger? chainId = null)
        {
            var transactionsReturn = new List<IndexedSignedTransaction>();
            foreach(var transaction in transactions)
            {
                transactionsReturn.Add(transaction.ToSignedTransaction(chainId));
            }
            return transactionsReturn;
        }
    }
}
