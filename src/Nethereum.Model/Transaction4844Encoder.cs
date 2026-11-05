using System.Collections.Generic;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;
using Nethereum.Util;

namespace Nethereum.Model
{
    public class Transaction4844Encoder : TransactionTypeEncoder<Transaction4844>
    {
        public static Transaction4844Encoder Current { get; } = new Transaction4844Encoder();
        public byte Type = TransactionType.Blob.AsByte();

        public List<byte[]> GetEncodedElements(Transaction4844 transaction)
        {
            var encodedData = new List<byte[]>
            {
                RLP.RLP.EncodeElement(transaction.ChainId.ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(GetValueForEncoding(transaction.Nonce)),
                RLP.RLP.EncodeElement(GetValueForEncoding(transaction.MaxPriorityFeePerGas)),
                RLP.RLP.EncodeElement(GetValueForEncoding(transaction.MaxFeePerGas)),
                RLP.RLP.EncodeElement(GetValueForEncoding(transaction.GasLimit)),
                RLP.RLP.EncodeElement(transaction.ReceiverAddress.HexToByteArray()),
                RLP.RLP.EncodeElement(GetValueForEncoding(transaction.Amount)),
                RLP.RLP.EncodeElement(transaction.Data.HexToByteArray()),
                AccessListRLPEncoderDecoder.EncodeAccessList(transaction.AccessList),
                RLP.RLP.EncodeElement(GetValueForEncoding(transaction.MaxFeePerBlobGas)),
                EncodeBlobVersionedHashes(transaction.BlobVersionedHashes)
            };
            return encodedData;
        }

        public override byte[] EncodeRaw(Transaction4844 transaction)
        {
            var encodedBytes = RLP.RLP.EncodeList(GetEncodedElements(transaction).ToArray());
            return AddTypeToEncodedBytes(encodedBytes, Type);
        }

        public override byte[] Encode(Transaction4844 transaction)
        {
            var encodedData = GetEncodedElements(transaction);
            RLPSignedDataEncoder.AddSignatureToEncodedData(transaction.Signature, encodedData);
            var encodedBytes = RLP.RLP.EncodeList(encodedData.ToArray());
            return AddTypeToEncodedBytes(encodedBytes, Type);
        }

        public override Transaction4844 Decode(byte[] rlpData)
        {
            return Decode(rlpData, allowNetworkWrapper: true);
        }

        public Transaction4844 Decode(byte[] rlpData, bool allowNetworkWrapper)
        {
            return (Transaction4844)TransactionRlpDecoder.DecodeBody(rlpData, TransactionType.Blob, allowNetworkWrapper);
        }

        public byte[] EncodeWithSidecar(Transaction4844 transaction)
        {
            if (transaction.Sidecar == null)
                return Encode(transaction);

            var signedTxElements = GetEncodedElements(transaction);
            RLPSignedDataEncoder.AddSignatureToEncodedData(transaction.Signature, signedTxElements);
            var signedTxRlp = RLP.RLP.EncodeList(signedTxElements.ToArray());

            var blobsEncoded = EncodeByteArrayList(transaction.Sidecar.Blobs);
            var commitmentsEncoded = EncodeByteArrayList(transaction.Sidecar.Commitments);
            var proofsEncoded = EncodeByteArrayList(transaction.Sidecar.Proofs);

            var wrapperElements = transaction.Sidecar.WrapperVersion == 0
                ? new[] { signedTxRlp, blobsEncoded, commitmentsEncoded, proofsEncoded }
                : new[] { signedTxRlp, RLP.RLP.EncodeElement(new[] { transaction.Sidecar.WrapperVersion }), blobsEncoded, commitmentsEncoded, proofsEncoded };

            var encodedBytes = RLP.RLP.EncodeList(wrapperElements);
            return AddTypeToEncodedBytes(encodedBytes, Type);
        }

        private static byte[] EncodeByteArrayList(List<byte[]> items)
        {
            if (items == null || items.Count == 0)
                return RLP.RLP.EncodeList(new byte[0][]);

            var encoded = new byte[items.Count][];
            for (int i = 0; i < items.Count; i++)
                encoded[i] = RLP.RLP.EncodeElement(items[i]);

            return RLP.RLP.EncodeList(encoded);
        }

        private static byte[] EncodeBlobVersionedHashes(List<byte[]> hashes)
        {
            if (hashes == null || hashes.Count == 0)
                return RLP.RLP.EncodeList(new byte[0][]);

            var encoded = new byte[hashes.Count][];
            for (int i = 0; i < hashes.Count; i++)
                encoded[i] = RLP.RLP.EncodeElement(hashes[i]);

            return RLP.RLP.EncodeList(encoded);
        }
    }
}
