using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;
using Nethereum.Util;

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
                RLP.RLP.EncodeElement(GetValueForEncoding(transaction.Nonce)),
                RLP.RLP.EncodeElement(GetValueForEncoding(transaction.MaxPriorityFeePerGas)),
                RLP.RLP.EncodeElement(GetValueForEncoding(transaction.MaxFeePerGas)),
                RLP.RLP.EncodeElement(GetValueForEncoding(transaction.GasLimit)),
                RLP.RLP.EncodeElement(transaction.ReceiverAddress.HexToByteArray()),
                RLP.RLP.EncodeElement(GetValueForEncoding(transaction.Amount)),
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
            return (Transaction7702)TransactionRlpDecoder.DecodeBody(rlpData, TransactionType.EIP7702, true);
        }
    }
}