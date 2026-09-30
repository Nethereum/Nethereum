using System.Collections.Generic;
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
            if (tx is Transaction4844 tx4844)
                return tx4844.ReceiverAddress;
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
            if (tx is Transaction4844 tx4844)
                return tx4844.Data?.HexToByteArray();
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
            if (tx is Transaction4844 tx4844)
                return tx4844.Amount ?? EvmUInt256.Zero;
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
            if (tx is Transaction4844 tx4844)
                return tx4844.GasLimit ?? 21000;
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
            if (tx is Transaction4844 tx4844)
                return tx4844.Nonce ?? EvmUInt256.Zero;
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
            if (tx is Transaction4844 tx4844)
                return tx4844.MaxFeePerGas ?? EvmUInt256.Zero;
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
            if (tx is Transaction4844 tx4844)
                return tx4844.MaxPriorityFeePerGas ?? EvmUInt256.Zero;
            if (tx is Transaction1559 tx1559)
                return tx1559.MaxPriorityFeePerGas ?? EvmUInt256.Zero;
            return EvmUInt256.Zero;
        }

        /// <summary>
        /// The transaction's EIP-2930 access list, or null for a type that has
        /// no access-list field at all — an absence the caller must be able to
        /// tell apart from a declared but empty one.
        /// </summary>
        public static List<AccessListItem> GetAccessList(this ISignedTransaction tx)
        {
            if (tx is Transaction7702 tx7702)
                return tx7702.AccessList;
            if (tx is Transaction4844 tx4844)
                return tx4844.AccessList;
            if (tx is Transaction1559 tx1559)
                return tx1559.AccessList;
            if (tx is Transaction2930 tx2930)
                return tx2930.AccessList;
            return null;
        }

        public static bool DeclaresItsOwnChainId(this ISignedTransaction tx)
        {
            return tx is Transaction7702 || tx is Transaction4844 ||
                   tx is Transaction1559 || tx is Transaction2930 ||
                   tx is LegacyTransactionChainId;
        }

        public static EvmUInt256 GetChainId(this ISignedTransaction tx)
        {
            if (tx is Transaction7702 tx7702)
                return tx7702.ChainId;
            if (tx is Transaction4844 tx4844)
                return tx4844.ChainId;
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
            return IsContractCreationRecipient(tx.GetReceiverAddress());
        }

        /// <summary>
        /// Per the Yellow Paper, a contract-creation transaction is marked by
        /// the <c>to</c> field being the empty byte string — RLP-encoded as 0x80.
        /// The 20-byte zero address (0x0000…0000) is a legitimate destination
        /// for value transfers (early mainnet had many burn-to-zero txs, e.g.
        /// block 1,150,000 tx[0]) and MUST NOT be treated as creation.
        /// </summary>
        public static bool IsContractCreationRecipient(string receiverAddress)
        {
            return string.IsNullOrEmpty(receiverAddress) || receiverAddress == "0x";
        }

        /// <summary>
        /// EIP-1559 §"Fee market change": the transaction types that declare
        /// <c>max_priority_fee_per_gas</c> and <c>max_fee_per_gas</c>, and are
        /// therefore priced by the 1559 fee rule rather than by a flat
        /// <c>gasPrice</c>.
        /// </summary>
        public static bool IsFeeMarketCapable(this ISignedTransaction tx)
        {
            return tx is Transaction7702 || tx is Transaction4844 || tx is Transaction1559;
        }

        public static EvmUInt256 GetEffectiveGasPrice(this ISignedTransaction tx, EvmUInt256 baseFee)
        {
            if (tx.IsFeeMarketCapable())
            {
                var maxFee = tx.GetMaxFeePerGas();
                var maxPriority = tx.GetMaxPriorityFeePerGas();
                var diff = maxFee - baseFee;
                var priority = maxPriority < diff ? maxPriority : diff;
                return baseFee + priority;
            }
            return tx.GetMaxFeePerGas();
        }
    }
}
