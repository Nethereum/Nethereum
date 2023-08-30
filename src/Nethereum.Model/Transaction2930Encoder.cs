using System.Collections.Generic;
using System.Linq;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;

namespace Nethereum.Model
{
    public class Transaction2930Encoder : TransactionTypeEncoder<Transaction2930>
    {
        public static Transaction2930Encoder Current { get; } = new Transaction2930Encoder();
        public byte Type = TransactionType.LegacyEIP2930.AsByte();

        public List<byte[]> GetEncodedElements(Transaction2930 transaction)
        {
            var encodedData = new List<byte[]>
            {
                RLP.RLP.EncodeElement(transaction.ChainId.ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(GetBigIntegerForEncoding(transaction.Nonce)),
                RLP.RLP.EncodeElement(GetBigIntegerForEncoding(transaction.GasPrice)),
                RLP.RLP.EncodeElement(GetBigIntegerForEncoding(transaction.GasLimit)),
                RLP.RLP.EncodeElement(transaction.ReceiverAddress.HexToByteArray()),
                RLP.RLP.EncodeElement(GetBigIntegerForEncoding(transaction.Amount)),
                RLP.RLP.EncodeElement(transaction.Data.HexToByteArray()),
                AccessListRLPEncoderDecoder.EncodeAccessList(transaction.AccessList)
            };
            return encodedData;
        }

        public override byte[] EncodeRaw(Transaction2930 transaction)
        {
            var encodedBytes = RLP.RLP.EncodeList(GetEncodedElements(transaction).ToArray());
            var returnBytes = AddTypeToEncodedBytes(encodedBytes, Type);
            return returnBytes;
        }


        public override byte[] Encode(Transaction2930 transaction)
        {
            var encodedData = GetEncodedElements(transaction);

            RLPSignedDataEncoder.AddSignatureToEncodedData(transaction.Signature, encodedData);

            var encodedBytes = RLP.RLP.EncodeList(encodedData.ToArray());
            var returnBytes = AddTypeToEncodedBytes(encodedBytes, Type);
            return returnBytes;

        }


        public override Transaction2930 Decode(byte[] rplData)
        {
            if (rplData[0] == Type)
            {
                rplData = rplData.Skip(1).ToArray();
            }

            var decodedList = RLP.RLP.Decode(rplData);
            var decodedElements = (RLPCollection)decodedList;
            var chainId = decodedElements[0].RLPData.ToBigIntegerFromRLPDecoded();
            var nonce = decodedElements[1].RLPData.ToBigIntegerFromRLPDecoded();
            var gasPrice = decodedElements[2].RLPData.ToBigIntegerFromRLPDecoded();
            var gasLimit = decodedElements[3].RLPData.ToBigIntegerFromRLPDecoded();
            var receiverAddress = decodedElements[4].RLPData?.ToHex(true);
            var amount = decodedElements[5].RLPData.ToBigIntegerFromRLPDecoded();
            var data = decodedElements[6].RLPData?.ToHex(true);
            var accessList = AccessListRLPEncoderDecoder.DecodeAccessList(decodedElements[7].RLPData);

            var signature = RLPSignedDataDecoder.DecodeSignature(decodedElements, 8);

            return new Transaction2930(chainId, nonce, gasPrice, gasLimit,
                receiverAddress, amount, data, accessList, signature);
        }

    }
}