using System.Numerics;
using NBitcoin.Crypto;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;

namespace Nethereum.Signer
{
    public class Transaction
    {
        public static readonly BigInteger DEFAULT_GAS_PRICE = BigInteger.Parse("10000000000000");
        public static readonly BigInteger DEFAULT_GAS_LIMIT = BigInteger.Parse("21000");
        private static readonly byte[] EMPTY_BYTE_ARRAY = new byte[0];
        private static readonly byte[] ZERO_BYTE_ARRAY = {0};

        private readonly RLPSigner simpleRlpSigner;

        public Transaction(byte[] rawData)
        {
            simpleRlpSigner = new RLPSigner(rawData, 6);
        }

        public Transaction(byte[] nonce, byte[] gasPrice, byte[] gasLimit, byte[] receiveAddress, byte[] value,
            byte[] data)
        {
            simpleRlpSigner = new RLPSigner(GetElementsInOrder(nonce, gasPrice, gasLimit, receiveAddress, value, data));
        }

        public Transaction(byte[] nonce, byte[] gasPrice, byte[] gasLimit, byte[] receiveAddress, byte[] value,
            byte[] data, byte[] r, byte[] s, byte v)
        {
            simpleRlpSigner = new RLPSigner(GetElementsInOrder(nonce, gasPrice, gasLimit, receiveAddress, value, data),
                r, s, v);
        }

        public Transaction(string to, BigInteger amount, BigInteger nonce)
            : this(to, amount, nonce, DEFAULT_GAS_PRICE, DEFAULT_GAS_LIMIT)
        {
        }

        public Transaction(string to, BigInteger amount, BigInteger nonce, string data)
            : this(to, amount, nonce, DEFAULT_GAS_PRICE, DEFAULT_GAS_LIMIT, data)
        {
        }

        public Transaction(string to, BigInteger amount, BigInteger nonce, BigInteger gasPrice, BigInteger gasLimit)
            : this(to, amount, nonce, gasPrice, gasLimit, "")
        {
        }

        public Transaction(string to, BigInteger amount, BigInteger nonce, BigInteger gasPrice,
            BigInteger gasLimit, string data) : this(nonce.ToBytesForRLPEncoding(), gasPrice.ToBytesForRLPEncoding(),
            gasLimit.ToBytesForRLPEncoding(), to.HexToByteArray(), amount.ToBytesForRLPEncoding(), data.HexToByteArray()
        )
        {
        }

        public byte[] Hash => simpleRlpSigner.Hash;

        public byte[] RawHash => simpleRlpSigner.RawHash;

        /// <summary>
        ///     The counter used to make sure each transaction can only be processed once, you may need to regenerate the
        ///     transaction if is too low or too high, simples way is to get the number of transacations
        /// </summary>
        public byte[] Nonce => simpleRlpSigner.Data[0] ?? ZERO_BYTE_ARRAY;

        public byte[] Value => simpleRlpSigner.Data[4] ?? ZERO_BYTE_ARRAY;

        public byte[] ReceiveAddress => simpleRlpSigner.Data[3];

        public byte[] GasPrice => simpleRlpSigner.Data[1] ?? ZERO_BYTE_ARRAY;

        public byte[] GasLimit => simpleRlpSigner.Data[2];

        public byte[] Data => simpleRlpSigner.Data[5];

        public EthECDSASignature Signature => simpleRlpSigner.Signature;

        public EthECKey Key => simpleRlpSigner.Key;

        public byte[] GetRLPEncoded()
        {
            return simpleRlpSigner.GetRLPEncoded();
        }

        public byte[] GetRLPEncodedRaw()
        {
            return simpleRlpSigner.GetRLPEncodedRaw();
        }

        public void Sign(EthECKey key)
        {
            simpleRlpSigner.Sign(key);
        }

        public string ToJsonHex()
        {
            var s = "['{0}','{1}','{2}','{3}','{4}','{5}','{6}','{7}','{8}']";
            return string.Format(s, Nonce.ToHex(),
                GasPrice.ToHex(), GasLimit.ToHex(), ReceiveAddress.ToHex(), Value.ToHex(), ToHex(Data),
                Signature.V.ToString("X"),
                Signature.R.ToHex(),
                Signature.S.ToHex());
        }

        private byte[][] GetElementsInOrder(byte[] nonce, byte[] gasPrice, byte[] gasLimit, byte[] receiveAddress,
            byte[] value,
            byte[] data)
        {
            if (receiveAddress == null)
                receiveAddress = EMPTY_BYTE_ARRAY;
            //order  nonce, gasPrice, gasLimit, receiveAddress, value, data
            return new[] {nonce, gasPrice, gasLimit, receiveAddress, value, data};
        }

        private string ToHex(byte[] x)
        {
            if (x == null) return "0x";
            return x.ToHex();
        }
    }
}