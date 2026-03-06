using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Signer;

namespace Nethereum.CoreChain.Rpc
{
    public static class SignedTransactionExtensions
    {
        public static Transaction ToRpcTransaction(
            this ISignedTransaction tx,
            byte[] blockHash,
            BigInteger blockNumber,
            int transactionIndex)
        {
            var from = tx.GetSenderAddress();

            var rpcTx = new Transaction
            {
                TransactionHash = tx.Hash?.ToHex(true),
                BlockHash = blockHash?.ToHex(true),
                BlockNumber = new HexBigInteger(blockNumber),
                TransactionIndex = new HexBigInteger(transactionIndex),
                From = from,
                To = tx.GetReceiverAddress(),
                Value = new HexBigInteger(tx.GetValue()),
                Gas = new HexBigInteger(tx.GetGasLimit()),
                Nonce = new HexBigInteger(tx.GetNonce()),
                Input = tx.GetData()?.ToHex(true) ?? "0x"
            };

            if (tx is Transaction7702 tx7702)
            {
                rpcTx.Type = new HexBigInteger(4);
                rpcTx.MaxFeePerGas = new HexBigInteger(tx7702.MaxFeePerGas ?? BigInteger.Zero);
                rpcTx.MaxPriorityFeePerGas = new HexBigInteger(tx7702.MaxPriorityFeePerGas ?? BigInteger.Zero);
            }
            else if (tx is Transaction1559 tx1559)
            {
                rpcTx.Type = new HexBigInteger(2);
                rpcTx.MaxFeePerGas = new HexBigInteger(tx1559.MaxFeePerGas ?? BigInteger.Zero);
                rpcTx.MaxPriorityFeePerGas = new HexBigInteger(tx1559.MaxPriorityFeePerGas ?? BigInteger.Zero);
            }
            else if (tx is Transaction2930 tx2930)
            {
                rpcTx.Type = new HexBigInteger(1);
                rpcTx.GasPrice = new HexBigInteger(tx2930.GasPrice ?? BigInteger.Zero);
            }
            else
            {
                rpcTx.Type = new HexBigInteger(0);
                rpcTx.GasPrice = new HexBigInteger(tx.GetMaxFeePerGas());
            }

            if (tx.Signature != null)
            {
                rpcTx.V = tx.Signature.V?.ToHex(true) ?? "0x0";
                rpcTx.R = tx.Signature.R?.ToHex(true);
                rpcTx.S = tx.Signature.S?.ToHex(true);
            }

            return rpcTx;
        }

        public static string GetSenderAddress(this ISignedTransaction tx)
        {
            try
            {
                var signature = tx.Signature;
                if (signature == null) return null;

                var key = EthECKeyBuilderFromSignedTransaction.GetEthECKey(tx);
                return key?.GetPublicAddress();
            }
            catch
            {
                return null;
            }
        }
    }
}
