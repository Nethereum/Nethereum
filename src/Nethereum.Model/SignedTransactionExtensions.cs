using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;

namespace Nethereum.Model
{
    public static class SignedTransactionExtensions
    {
        public static string GetReceiverAddress(this ISignedTransaction tx)
        {
            if (tx is Transaction1559 tx1559)
                return tx1559.ReceiverAddress;
            if (tx is Transaction2930 tx2930)
                return tx2930.ReceiverAddress;
            if (tx is LegacyTransaction legacyTx)
                return legacyTx.ReceiveAddress?.ToHex(true);
            if (tx is LegacyTransactionChainId legacyChainTx)
                return legacyChainTx.ReceiveAddress?.ToHex(true);
            return null;
        }

        public static byte[] GetData(this ISignedTransaction tx)
        {
            if (tx is Transaction1559 tx1559)
                return tx1559.Data?.HexToByteArray();
            if (tx is Transaction2930 tx2930)
                return tx2930.Data?.HexToByteArray();
            if (tx is LegacyTransaction legacyTx)
                return legacyTx.Data;
            if (tx is LegacyTransactionChainId legacyChainTx)
                return legacyChainTx.Data;
            return null;
        }

        public static BigInteger GetValue(this ISignedTransaction tx)
        {
            if (tx is Transaction1559 tx1559)
                return tx1559.Amount ?? BigInteger.Zero;
            if (tx is Transaction2930 tx2930)
                return tx2930.Amount ?? BigInteger.Zero;
            if (tx is LegacyTransaction legacyTx)
                return legacyTx.Value.ToBigIntegerFromRLPDecoded();
            if (tx is LegacyTransactionChainId legacyChainTx)
                return legacyChainTx.Value.ToBigIntegerFromRLPDecoded();
            return BigInteger.Zero;
        }

        public static BigInteger GetGasLimit(this ISignedTransaction tx)
        {
            if (tx is Transaction1559 tx1559)
                return tx1559.GasLimit ?? 21000;
            if (tx is Transaction2930 tx2930)
                return tx2930.GasLimit ?? 21000;
            if (tx is LegacyTransaction legacyTx)
                return legacyTx.GasLimit.ToBigIntegerFromRLPDecoded();
            if (tx is LegacyTransactionChainId legacyChainTx)
                return legacyChainTx.GasLimit.ToBigIntegerFromRLPDecoded();
            return 21000;
        }

        public static BigInteger GetNonce(this ISignedTransaction tx)
        {
            if (tx is Transaction1559 tx1559)
                return tx1559.Nonce ?? BigInteger.Zero;
            if (tx is Transaction2930 tx2930)
                return tx2930.Nonce ?? BigInteger.Zero;
            if (tx is LegacyTransaction legacyTx)
                return legacyTx.Nonce.ToBigIntegerFromRLPDecoded();
            if (tx is LegacyTransactionChainId legacyChainTx)
                return legacyChainTx.Nonce.ToBigIntegerFromRLPDecoded();
            return BigInteger.Zero;
        }

        public static BigInteger GetMaxFeePerGas(this ISignedTransaction tx)
        {
            if (tx is Transaction1559 tx1559)
                return tx1559.MaxFeePerGas ?? BigInteger.Zero;
            if (tx is Transaction2930 tx2930)
                return tx2930.GasPrice ?? BigInteger.Zero;
            if (tx is LegacyTransaction legacyTx)
                return legacyTx.GasPrice.ToBigIntegerFromRLPDecoded();
            if (tx is LegacyTransactionChainId legacyChainTx)
                return legacyChainTx.GasPrice.ToBigIntegerFromRLPDecoded();
            return BigInteger.Zero;
        }

        public static BigInteger GetMaxPriorityFeePerGas(this ISignedTransaction tx)
        {
            if (tx is Transaction1559 tx1559)
                return tx1559.MaxPriorityFeePerGas ?? BigInteger.Zero;
            return BigInteger.Zero;
        }

        public static BigInteger GetChainId(this ISignedTransaction tx)
        {
            if (tx is Transaction1559 tx1559)
                return tx1559.ChainId;
            if (tx is Transaction2930 tx2930)
                return tx2930.ChainId;
            if (tx is LegacyTransactionChainId legacyChainTx)
                return legacyChainTx.GetChainIdAsBigInteger();
            return BigInteger.Zero;
        }

        public static bool IsContractCreation(this ISignedTransaction tx)
        {
            var to = tx.GetReceiverAddress();
            return string.IsNullOrEmpty(to) || to == "0x" || to == "0x0000000000000000000000000000000000000000";
        }
    }
}
