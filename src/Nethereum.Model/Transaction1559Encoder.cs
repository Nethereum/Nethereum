using System;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Util;

namespace Nethereum.Model
{

    public class Transaction1559Encoder:TransactionTypeEncoder<Transaction1559>
    {
        public static Transaction1559Encoder Current { get; } = new Transaction1559Encoder();
        public byte Type = TransactionType.EIP1559.AsByte();

        public List<byte[]> GetEncodedElements(Transaction1559 transaction)
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
                AccessListRLPEncoderDecoder.EncodeAccessList(transaction.AccessList)
            };
            return encodedData;
        }

        public override byte[] EncodeRaw(Transaction1559 transaction)
        {
            var encodedBytes = RLP.RLP.EncodeList(GetEncodedElements(transaction).ToArray());
            var returnBytes = AddTypeToEncodedBytes(encodedBytes, Type);
            return returnBytes;
        }


        public override byte[] Encode(Transaction1559 transaction)
        {
            var encodedData = GetEncodedElements(transaction);

            RLPSignedDataEncoder.AddSignatureToEncodedData(transaction.Signature, encodedData);

            var encodedBytes = RLP.RLP.EncodeList(encodedData.ToArray());
            var returnBytes = AddTypeToEncodedBytes(encodedBytes, Type);
            return returnBytes;

        }


        public override Transaction1559 Decode(byte[] rplData)
        {
            return (Transaction1559)TransactionRlpDecoder.DecodeBody(rplData, TransactionType.EIP1559, true);
        }

    }
}