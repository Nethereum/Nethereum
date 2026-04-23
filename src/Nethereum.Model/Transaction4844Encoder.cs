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
            if (rlpData[0] == Type)
            {
                rlpData = rlpData.SliceFrom(1);
            }

            var decodedList = RLP.RLP.Decode(rlpData);
            var decodedElements = (RLPCollection)decodedList;

            if (IsNetworkWrapper(decodedElements))
            {
                var txElements = (RLPCollection)decodedElements[0];
                var tx = DecodeTxFields(txElements);
                tx.Sidecar = DecodeSidecar(decodedElements);
                return tx;
            }

            return DecodeTxFields(decodedElements);
        }

        private static Transaction4844 DecodeTxFields(RLPCollection decodedElements)
        {
            var chainId = decodedElements[0].RLPData.ToEvmUInt256FromRLPDecoded();
            var nonce = decodedElements[1].RLPData.ToEvmUInt256FromRLPDecoded();
            var maxPriorityFeePerGas = decodedElements[2].RLPData.ToEvmUInt256FromRLPDecoded();
            var maxFeePerGas = decodedElements[3].RLPData.ToEvmUInt256FromRLPDecoded();
            var gasLimit = decodedElements[4].RLPData.ToEvmUInt256FromRLPDecoded();
            var receiverAddress = decodedElements[5].RLPData?.ToHex(true);
            var amount = decodedElements[6].RLPData.ToEvmUInt256FromRLPDecoded();
            var data = decodedElements[7].RLPData?.ToHex(true);
            var accessList = AccessListRLPEncoderDecoder.DecodeAccessList(decodedElements[8].RLPData);
            var maxFeePerBlobGas = decodedElements[9].RLPData.ToEvmUInt256FromRLPDecoded();
            var blobVersionedHashes = DecodeBlobVersionedHashes(decodedElements[10]);

            var signature = RLPSignedDataDecoder.DecodeSignature(decodedElements, 11);

            return new Transaction4844(chainId, nonce, maxPriorityFeePerGas, maxFeePerGas, gasLimit,
                receiverAddress, amount, data, accessList, maxFeePerBlobGas, blobVersionedHashes, signature);
        }

        private static bool IsNetworkWrapper(RLPCollection elements)
        {
            return elements.Count == 4 && elements[0] is RLPCollection;
        }

        private static BlobSidecar DecodeSidecar(RLPCollection wrapperElements)
        {
            var blobs = DecodeByteArrayList(wrapperElements[1]);
            var commitments = DecodeByteArrayList(wrapperElements[2]);
            var proofs = DecodeByteArrayList(wrapperElements[3]);
            return new BlobSidecar(blobs, commitments, proofs);
        }

        private static List<byte[]> DecodeByteArrayList(IRLPElement element)
        {
            var result = new List<byte[]>();
            if (element is RLPCollection collection)
            {
                foreach (var item in collection)
                    result.Add(item.RLPData);
            }
            return result;
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

            var wrapperElements = new[]
            {
                signedTxRlp,
                blobsEncoded,
                commitmentsEncoded,
                proofsEncoded
            };

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

        private static List<byte[]> DecodeBlobVersionedHashes(IRLPElement element)
        {
            var result = new List<byte[]>();
            if (element is RLPCollection collection)
            {
                foreach (var item in collection)
                    result.Add(item.RLPData);
            }
            return result;
        }
    }
}
