using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.RLP;

namespace Nethereum.Signer
{

    public interface ITransactionTypeDecoder
    {
        SignedTypeTransaction DecodeAsGeneric(byte[] rlpData);
    }

    public abstract class TransactionTypeEncoder<T>: ITransactionTypeDecoder where T : SignedTypeTransaction
    {
        public static byte[] AddTypeToEncodedBytes(byte[] encodedBytes, byte type)
        {
            var returnBytes = new byte[encodedBytes.Length + 1];
            Array.Copy(encodedBytes, 0, returnBytes, 1, encodedBytes.Length);
            returnBytes[0] = type;
            return returnBytes;
        }

        public byte[] GetBigIntegerForEncoding(BigInteger? value)
        {
            if (value == null) return DefaultValues.ZERO_BYTE_ARRAY;
            return value.Value.ToBytesForRLPEncoding();
        }
        public SignedTypeTransaction DecodeAsGeneric(byte[] rlpData)
        {
            return Decode(rlpData);
        }

        public abstract T Decode(byte[] rplData);
        public abstract byte[] Encode(T transaction);
        public abstract byte[] EncodeRaw(T transaction);
    }

    public class Transaction1559Encoder:TransactionTypeEncoder<Transaction1559>
    {
        public static Transaction1559Encoder Current { get; } = new Transaction1559Encoder();
        public byte Type = TransactionType.EIP1559.AsByte();

        public List<byte[]> GetEncodedElements(Transaction1559 transaction)
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

            RLPEncoder.AddSignatureToEncodedData(transaction.Signature, encodedData);

            var encodedBytes = RLP.RLP.EncodeList(encodedData.ToArray());
            var returnBytes = AddTypeToEncodedBytes(encodedBytes, Type);
            return returnBytes;

        }


        public override Transaction1559 Decode(byte[] rplData)
        {
            if (rplData[0] == Type)
            {
                rplData = rplData.Skip(1).ToArray();
            }

            var decodedList = RLP.RLP.Decode(rplData);
            var decodedData = new List<byte[]>();
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

            var signature = RLPDecoder.DecodeSignature(decodedElements, 9);

            return new Transaction1559(chainId, nonce, maxPriorityFeePerGas, maxFeePerGas, gasLimit,
                receiverAddress, amount, data, accessList, signature);
        }

    }
}