using System.Collections.Generic;
using System.Linq;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;

namespace Nethereum.Model
{
    public class Transaction7702Encoder : TransactionTypeEncoder<Transaction7702>
    {
        public static Transaction7702Encoder Current { get; } = new Transaction7702Encoder();
        public byte Type = TransactionType.EIP7702.AsByte();

        public List<byte[]> GetEncodedElements(Transaction7702 transaction)
        {
            var encodedData = new List<byte[]>
            {
                RLP.RLP.EncodeElement(transaction.ChainId.ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(GetBigIntegerForEncoding(transaction.Nonce)),
                RLP.RLP.EncodeElement(GetBigIntegerForEncoding(transaction.MaxPriorityFeePerGas)),
                RLP.RLP.EncodeElement(GetBigIntegerForEncoding(transaction.MaxFeePerGas)),
                RLP.RLP.EncodeElement(GetBigIntegerForEncoding(transaction.GasLimit)),
                RLP.RLP.EncodeElement(transaction.ReceiverAddress.HexToByteArray()),
                RLP.RLP.EncodeElement(GetBigIntegerForEncoding(transaction.Amount)),
                RLP.RLP.EncodeElement(transaction.Data.HexToByteArray()),
                AccessListRLPEncoderDecoder.EncodeAccessList(transaction.AccessList),
                AuthorisationListRLPEncoderDecoder.Encode(transaction.AuthorisationList)
            };
            return encodedData;
        }

        public override byte[] EncodeRaw(Transaction7702 transaction)
        {
            var encodedBytes = RLP.RLP.EncodeList(GetEncodedElements(transaction).ToArray());
            var returnBytes = AddTypeToEncodedBytes(encodedBytes, Type);
            return returnBytes;
        }

        public override byte[] Encode(Transaction7702 transaction)
        {
            var encodedData = GetEncodedElements(transaction);

            RLPSignedDataEncoder.AddSignatureToEncodedData(transaction.Signature, encodedData);

            var encodedBytes = RLP.RLP.EncodeList(encodedData.ToArray());
            var returnBytes = AddTypeToEncodedBytes(encodedBytes, Type);
            return returnBytes;
        }

        public override Transaction7702 Decode(byte[] rlpData)
        {
            if (rlpData[0] == Type)
            {
                rlpData = rlpData.Skip(1).ToArray();
            }

            var decodedList = RLP.RLP.Decode(rlpData);
            var decodedElements = (RLPCollection)decodedList;
            var chainId = decodedElements[0].RLPData.ToBigIntegerFromRLPDecoded();
            var nonce = decodedElements[1].RLPData.ToBigIntegerFromRLPDecoded();
            var maxPriorityFeePerGas = decodedElements[2].RLPData.ToBigIntegerFromRLPDecoded();
            var maxFeePerGas = decodedElements[3].RLPData.ToBigIntegerFromRLPDecoded();
            var gasLimit = decodedElements[4].RLPData.ToBigIntegerFromRLPDecoded();
            var receiverAddress = decodedElements[5].RLPData?.ToHex(true);
            var amount = decodedElements[6].RLPData.ToBigIntegerFromRLPDecoded();
            var data = decodedElements[7].RLPData?.ToHex(true);
            var accessList = AccessListRLPEncoderDecoder.DecodeAccessList(decodedElements[8].RLPData);
            var authorizationList = AuthorisationListRLPEncoderDecoder.Decode(decodedElements[9].RLPData);

            var signature = RLPSignedDataDecoder.DecodeSignature(decodedElements, 10);

            return new Transaction7702(chainId, nonce, maxPriorityFeePerGas, maxFeePerGas, gasLimit,
                receiverAddress, amount, data, accessList, authorizationList, signature);
        }
    }
}