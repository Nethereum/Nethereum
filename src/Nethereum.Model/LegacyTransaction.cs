using System;
using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;

namespace Nethereum.Model
{
    public class LegacyTransaction : SignedLegacyTransaction
    {
        public override TransactionType TransactionType => TransactionType.LegacyTransaction;

        public LegacyTransaction(byte[] rawData)
        {
            RlpSignerEncoder = new RLPSignedDataHashBuilder(rawData, NUMBER_ENCODING_ELEMENTS);
            if (Signature != null)
            {
                ValidateValidV(RlpSignerEncoder);
            }
        }

        public LegacyTransaction(RLPSignedDataHashBuilder rlpSigner)
        {
            ValidateValidV(rlpSigner);
            RlpSignerEncoder = rlpSigner;
        }

        private static void ValidateValidV(RLPSignedDataHashBuilder rlpSigner)
        {
            if (rlpSigner.IsVSignatureForChain())
                throw new Exception("LegacyTransactionChainId should be used instead of LegacyTransaction");
        }

        public LegacyTransaction(byte[] nonce, byte[] gasPrice, byte[] gasLimit, byte[] receiveAddress, byte[] value,
            byte[] data)
        {
            RlpSignerEncoder = new RLPSignedDataHashBuilder(GetElementsInOrder(nonce, gasPrice, gasLimit, receiveAddress, value, data));
        }

        public LegacyTransaction(byte[] nonce, byte[] gasPrice, byte[] gasLimit, byte[] receiveAddress, byte[] value,
            byte[] data, byte[] r, byte[] s, byte v)
        {
            RlpSignerEncoder = new RLPSignedDataHashBuilder(GetElementsInOrder(nonce, gasPrice, gasLimit, receiveAddress, value, data),
                r, s, v);
        }

        public LegacyTransaction(string to, BigInteger amount, BigInteger nonce)
            : this(to, amount, nonce, DEFAULT_GAS_PRICE, DEFAULT_GAS_LIMIT)
        {
        }

        public LegacyTransaction(string to, BigInteger amount, BigInteger nonce, string data)
            : this(to, amount, nonce, DEFAULT_GAS_PRICE, DEFAULT_GAS_LIMIT, data)
        {
        }

        public LegacyTransaction(string to, BigInteger amount, BigInteger nonce, BigInteger gasPrice, BigInteger gasLimit)
            : this(to, amount, nonce, gasPrice, gasLimit, "")
        {
        }

        public LegacyTransaction(string to, BigInteger amount, BigInteger nonce, BigInteger gasPrice,
            BigInteger gasLimit, string data) : this(nonce.ToBytesForRLPEncoding(), gasPrice.ToBytesForRLPEncoding(),
            gasLimit.ToBytesForRLPEncoding(), to.HexToByteArray(), amount.ToBytesForRLPEncoding(), data.HexToByteArray()
        )
        {
        }

        public string ToJsonHex()
        {
            var s = "['{0}','{1}','{2}','{3}','{4}','{5}','{6}','{7}','{8}']";
            return string.Format(s, Nonce.ToHex(),
                GasPrice.ToHex(), GasLimit.ToHex(), ReceiveAddress.ToHex(), Value.ToHex(), ToHex(Data),
                Signature.V.ToHex(),
                Signature.R.ToHex(),
                Signature.S.ToHex());
        }

        private byte[][] GetElementsInOrder(byte[] nonce, byte[] gasPrice, byte[] gasLimit, byte[] receiveAddress,
            byte[] value,
            byte[] data)
        {
            if (receiveAddress == null)
                receiveAddress = DefaultValues.EMPTY_BYTE_ARRAY;
            //order  nonce, gasPrice, gasLimit, receiveAddress, value, data
            return new[] {nonce, gasPrice, gasLimit, receiveAddress, value, data};
        }

    }
}