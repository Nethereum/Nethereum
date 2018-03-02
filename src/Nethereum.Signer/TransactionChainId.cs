using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;

namespace Nethereum.Signer
{
    public class TransactionChainId : TransactionBase
    {
        private const int NUMBER_ENCODING_ELEMENTS = 6;

        public TransactionChainId(byte[] rawData, BigInteger chainId)
        {
            //Instantiate and decode
            SimpleRlpSigner = new RLPSigner(rawData, 6);
            //append the chainId, r and s so it can be recovered using the raw hash
            SimpleRlpSigner.AppendData(chainId.ToBytesForRLPEncoding(), 0.ToBytesForRLPEncoding(),
                0.ToBytesForRLPEncoding());
        }

        public TransactionChainId(byte[] nonce, byte[] gasPrice, byte[] gasLimit, byte[] receiveAddress, byte[] value,
            byte[] data, byte[] chainId)
        {
            SimpleRlpSigner =
                new RLPSigner(GetElementsInOrder(nonce, gasPrice, gasLimit, receiveAddress, value, data, chainId),
                    NUMBER_ENCODING_ELEMENTS);
        }

        public TransactionChainId(byte[] nonce, byte[] gasPrice, byte[] gasLimit, byte[] receiveAddress, byte[] value,
            byte[] data, byte[] chainId, byte[] r, byte[] s, byte[] v)
        {
            SimpleRlpSigner = new RLPSigner(
                GetElementsInOrder(nonce, gasPrice, gasLimit, receiveAddress, value, data, chainId),
                r, s, v, NUMBER_ENCODING_ELEMENTS);
        }

        public TransactionChainId(string to, BigInteger amount, BigInteger nonce, BigInteger chainId)
            : this(to, amount, nonce, DEFAULT_GAS_PRICE, DEFAULT_GAS_LIMIT, chainId)
        {
        }

        public TransactionChainId(string to, BigInteger amount, BigInteger nonce, string data, BigInteger chainId)
            : this(to, amount, nonce, DEFAULT_GAS_PRICE, DEFAULT_GAS_LIMIT, data, chainId)
        {
        }

        public TransactionChainId(string to, BigInteger amount, BigInteger nonce, BigInteger gasPrice,
            BigInteger gasLimit, BigInteger chainId)
            : this(to, amount, nonce, gasPrice, gasLimit, "", chainId)
        {
        }

        public TransactionChainId(string to, BigInteger amount, BigInteger nonce, BigInteger gasPrice,
            BigInteger gasLimit, string data, BigInteger chainId) : this(nonce.ToBytesForRLPEncoding(),
            gasPrice.ToBytesForRLPEncoding(),
            gasLimit.ToBytesForRLPEncoding(), to.HexToByteArray(), amount.ToBytesForRLPEncoding(),
            data.HexToByteArray(), chainId.ToBytesForRLPEncoding()
        )
        {
        }

        public byte[] ChainId => SimpleRlpSigner.Data[6];

        public byte[] RHash => SimpleRlpSigner.Data[7];

        public byte[] SHash => SimpleRlpSigner.Data[8];

        public override EthECKey Key => EthECKey.RecoverFromSignature(SimpleRlpSigner.Signature,
            SimpleRlpSigner.RawHash,
            ChainId.ToBigIntegerFromRLPDecoded());

        public string ToJsonHex()
        {
            var data =
                $"['{Nonce.ToHex()}','{GasPrice.ToHex()}','{GasLimit.ToHex()}','{ReceiveAddress.ToHex()}','{Value.ToHex()}','{ToHex(Data)}','{ChainId.ToHex()}','{RHash.ToHex()}','{SHash.ToHex()}'";

            if (Signature != null)
                data = data + $", '{Signature.V.ToHex()}', '{Signature.R.ToHex()}', '{Signature.S.ToHex()}'";
            return data + "]";
        }

        public override void Sign(EthECKey key)
        {
            SimpleRlpSigner.Sign(key, ChainId.ToBigIntegerFromRLPDecoded());
        }

        private byte[][] GetElementsInOrder(byte[] nonce, byte[] gasPrice, byte[] gasLimit, byte[] receiveAddress,
            byte[] value,
            byte[] data, byte[] chainId)
        {
            if (receiveAddress == null)
                receiveAddress = EMPTY_BYTE_ARRAY;
            //order  nonce, gasPrice, gasLimit, receiveAddress, value, data, chainId, r = 0, s =0
            return new[]
            {
                nonce, gasPrice, gasLimit, receiveAddress, value, data, chainId, 0.ToBytesForRLPEncoding(),
                0.ToBytesForRLPEncoding()
            };
        }
    }
}