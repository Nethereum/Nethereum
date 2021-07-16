using System;
using Nethereum.Hex.HexTypes;

namespace Nethereum.RPC.TransactionTypes
{
    public enum TransactionType
    {
        LegacyTransaction = -1,
        LegacyChainTransaction = -2,
        EIP1559 = 0X02 
    }

    public static class TransactionTypeExtensions
    {
        public static byte AsByte(this TransactionType transactionType) => (byte)transactionType;

        public static HexBigInteger AsHexBigInteger(this TransactionType transactionType) =>
            new HexBigInteger((int) transactionType);

        public static TransactionType ToTransactionType(this HexBigInteger value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            return ToTypedTransaction((byte) value.Value);
        }
        /// <summary>
        /// Converts to a valid Typed transaction (ie 0x02 for 1559), if not throws an exception (ie legacy or not in range)
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static TransactionType ToTypedTransaction(this byte? value)
        {
            if (IsTypedTransaction(value))
            {
                return (TransactionType)value;
            }
            throw new ArgumentOutOfRangeException(nameof(value), "Value was not a valid typed transaction");
        }

        public static bool IsTypedTransaction(this byte? value)
        {
            return value != null && value == TransactionType.EIP1559.AsByte();
        }
    }
}