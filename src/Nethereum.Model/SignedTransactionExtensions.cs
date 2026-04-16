using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;
using Nethereum.Util;

namespace Nethereum.Model
{
    public static class SignedTransactionExtensions
    {
        public static string GetReceiverAddress(this ISignedTransaction tx)
        {
            if (tx is Transaction7702 tx7702)
                return tx7702.ReceiverAddress;
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
            if (tx is Transaction7702 tx7702)
                return tx7702.Data?.HexToByteArray();
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

        public static EvmUInt256 GetValue(this ISignedTransaction tx)
        {
            if (tx is Transaction7702 tx7702)
                return tx7702.Amount ?? EvmUInt256.Zero;
            if (tx is Transaction1559 tx1559)
                return tx1559.Amount ?? EvmUInt256.Zero;
            if (tx is Transaction2930 tx2930)
                return tx2930.Amount ?? EvmUInt256.Zero;
            if (tx is LegacyTransaction legacyTx)
                return legacyTx.Value.ToEvmUInt256FromRLPDecoded();
            if (tx is LegacyTransactionChainId legacyChainTx)
                return legacyChainTx.Value.ToEvmUInt256FromRLPDecoded();
            return EvmUInt256.Zero;
        }

        public static EvmUInt256 GetGasLimit(this ISignedTransaction tx)
        {
            if (tx is Transaction7702 tx7702)
                return tx7702.GasLimit ?? 21000;
            if (tx is Transaction1559 tx1559)
                return tx1559.GasLimit ?? 21000;
            if (tx is Transaction2930 tx2930)
                return tx2930.GasLimit ?? 21000;
            if (tx is LegacyTransaction legacyTx)
                return legacyTx.GasLimit.ToEvmUInt256FromRLPDecoded();
            if (tx is LegacyTransactionChainId legacyChainTx)
                return legacyChainTx.GasLimit.ToEvmUInt256FromRLPDecoded();
            return 21000;
        }

        public static EvmUInt256 GetNonce(this ISignedTransaction tx)
        {
            if (tx is Transaction7702 tx7702)
                return tx7702.Nonce ?? EvmUInt256.Zero;
            if (tx is Transaction1559 tx1559)
                return tx1559.Nonce ?? EvmUInt256.Zero;
            if (tx is Transaction2930 tx2930)
                return tx2930.Nonce ?? EvmUInt256.Zero;
            if (tx is LegacyTransaction legacyTx)
                return legacyTx.Nonce.ToEvmUInt256FromRLPDecoded();
            if (tx is LegacyTransactionChainId legacyChainTx)
                return legacyChainTx.Nonce.ToEvmUInt256FromRLPDecoded();
            return EvmUInt256.Zero;
        }

        public static EvmUInt256 GetMaxFeePerGas(this ISignedTransaction tx)
        {
            if (tx is Transaction7702 tx7702)
                return tx7702.MaxFeePerGas ?? EvmUInt256.Zero;
            if (tx is Transaction1559 tx1559)
                return tx1559.MaxFeePerGas ?? EvmUInt256.Zero;
            if (tx is Transaction2930 tx2930)
                return tx2930.GasPrice ?? EvmUInt256.Zero;
            if (tx is LegacyTransaction legacyTx)
                return legacyTx.GasPrice.ToEvmUInt256FromRLPDecoded();
            if (tx is LegacyTransactionChainId legacyChainTx)
                return legacyChainTx.GasPrice.ToEvmUInt256FromRLPDecoded();
            return EvmUInt256.Zero;
        }

        public static EvmUInt256 GetMaxPriorityFeePerGas(this ISignedTransaction tx)
        {
            if (tx is Transaction7702 tx7702)
                return tx7702.MaxPriorityFeePerGas ?? EvmUInt256.Zero;
            if (tx is Transaction1559 tx1559)
                return tx1559.MaxPriorityFeePerGas ?? EvmUInt256.Zero;
            return EvmUInt256.Zero;
        }

        public static EvmUInt256 GetChainId(this ISignedTransaction tx)
        {
            if (tx is Transaction7702 tx7702)
                return tx7702.ChainId;
            if (tx is Transaction1559 tx1559)
                return tx1559.ChainId;
            if (tx is Transaction2930 tx2930)
                return tx2930.ChainId;
            if (tx is LegacyTransactionChainId legacyChainTx)
                return legacyChainTx.GetChainIdAsBigInteger();
            return EvmUInt256.Zero;
        }

        public static bool IsContractCreation(this ISignedTransaction tx)
        {
            var to = tx.GetReceiverAddress();
            return string.IsNullOrEmpty(to) || to == "0x" || to == "0x0000000000000000000000000000000000000000";
        }
    }
}
