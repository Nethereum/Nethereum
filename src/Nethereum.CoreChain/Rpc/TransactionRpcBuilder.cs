using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Mappers;
using Nethereum.Util;

namespace Nethereum.CoreChain.Rpc
{
    public static class TransactionRpcBuilder
    {
        public static Transaction Build(
            ISignedTransaction signedTx,
            byte[] blockHash,
            BigInteger? blockNumber,
            int? transactionIndex,
            BlockHeader blockHeader,
            string senderOverride = null)
        {
            return Populate(new Transaction(), signedTx, blockHash, blockNumber, transactionIndex, blockHeader, senderOverride);
        }

        public static T Populate<T>(
            T tx,
            ISignedTransaction signedTx,
            byte[] blockHash,
            BigInteger? blockNumber,
            int? transactionIndex,
            BlockHeader blockHeader,
            string senderOverride = null) where T : Transaction
        {
            tx.TransactionHash = signedTx.Hash?.ToHex(true);
            tx.BlockHash = blockHash?.ToHex(true);
            tx.BlockNumber = blockNumber.HasValue ? new HexBigInteger(blockNumber.Value) : null;
            tx.TransactionIndex = transactionIndex.HasValue ? new HexBigInteger(transactionIndex.Value) : null;
            tx.Type = new HexBigInteger(signedTx.TransactionType.AsChainByteType());
            tx.From = senderOverride ?? RpcTransactionAddress.ResolveSender(signedTx);
            tx.To = RpcTransactionAddress.ResolveReceiver(signedTx);
            tx.Value = new HexBigInteger(signedTx.GetValue());
            tx.Gas = new HexBigInteger(signedTx.GetGasLimit());
            tx.Nonce = new HexBigInteger(signedTx.GetNonce());
            tx.Input = signedTx.GetData()?.ToHex(true) ?? "0x";
            tx.GasPrice = new HexBigInteger(ResolveGasPrice(signedTx, blockHeader));

            if (blockHeader != null)
                tx.BlockTimestamp = new HexBigInteger(blockHeader.Timestamp);

            PopulateTypedFields(tx, signedTx);
            PopulateSignature(tx, signedTx);

            return tx;
        }

        private static EvmUInt256 ResolveGasPrice(ISignedTransaction signedTx, BlockHeader blockHeader) =>
            blockHeader != null
                ? signedTx.GetEffectiveGasPrice(blockHeader.BaseFee ?? EvmUInt256.Zero)
                : signedTx.GetMaxFeePerGas();

        private static void PopulateTypedFields(Transaction tx, ISignedTransaction signedTx)
        {
            switch (signedTx)
            {
                case Transaction1559 eip1559:
                    tx.MaxFeePerGas = new HexBigInteger(eip1559.MaxFeePerGas ?? EvmUInt256.Zero);
                    tx.MaxPriorityFeePerGas = new HexBigInteger(eip1559.MaxPriorityFeePerGas ?? EvmUInt256.Zero);
                    tx.AccessList = eip1559.AccessList.ToRPCAccessList();
                    tx.ChainId = new HexBigInteger(eip1559.ChainId);
                    tx.YParity = SignatureParity(signedTx);
                    break;

                case Transaction2930 eip2930:
                    tx.AccessList = eip2930.AccessList.ToRPCAccessList();
                    tx.ChainId = new HexBigInteger(eip2930.ChainId);
                    tx.YParity = SignatureParity(signedTx);
                    break;

                case Transaction4844 eip4844:
                    tx.MaxFeePerGas = new HexBigInteger(eip4844.MaxFeePerGas ?? EvmUInt256.Zero);
                    tx.MaxPriorityFeePerGas = new HexBigInteger(eip4844.MaxPriorityFeePerGas ?? EvmUInt256.Zero);
                    tx.AccessList = eip4844.AccessList.ToRPCAccessList();
                    tx.MaxFeePerBlobGas = new HexBigInteger(eip4844.MaxFeePerBlobGas ?? EvmUInt256.Zero);
                    tx.BlobVersionedHashes = ToHexArray(eip4844.BlobVersionedHashes);
                    tx.ChainId = new HexBigInteger(eip4844.ChainId);
                    tx.YParity = SignatureParity(signedTx);
                    break;

                case Transaction7702 eip7702:
                    tx.MaxFeePerGas = new HexBigInteger(eip7702.MaxFeePerGas ?? EvmUInt256.Zero);
                    tx.MaxPriorityFeePerGas = new HexBigInteger(eip7702.MaxPriorityFeePerGas ?? EvmUInt256.Zero);
                    tx.AccessList = eip7702.AccessList.ToRPCAccessList();
                    tx.AuthorisationList = eip7702.AuthorisationList.ToRPCAuthorisation();
                    tx.ChainId = new HexBigInteger(eip7702.ChainId);
                    tx.YParity = SignatureParity(signedTx);
                    break;

                case LegacyTransactionChainId legacyChainId:
                    tx.ChainId = new HexBigInteger(new BigInteger(
                        ByteUtil.BigEndianToBigIntegerLittleEndianUnsigned(legacyChainId.ChainId)));
                    break;
            }
        }

        private static HexBigInteger SignatureParity(ISignedTransaction signedTx)
        {
            var v = signedTx.Signature?.V;
            if (v == null || v.Length == 0) return new HexBigInteger(0);
            return new HexBigInteger(v.ToRpcSignatureQuantity());
        }

        private static void PopulateSignature(Transaction tx, ISignedTransaction signedTx)
        {
            tx.R = signedTx.Signature?.R.ToRpcSignatureQuantity() ?? "0x0";
            tx.S = signedTx.Signature?.S.ToRpcSignatureQuantity() ?? "0x0";
            tx.V = signedTx.Signature?.V.ToRpcSignatureQuantity() ?? "0x0";
        }

        private static string[] ToHexArray(List<byte[]> values)
        {
            if (values == null) return null;
            var result = new string[values.Count];
            for (var i = 0; i < values.Count; i++)
                result[i] = values[i]?.ToHex(true);
            return result;
        }
    }
}
