using System;
using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;

namespace Nethereum.Signer
{
    public class Transaction
    {
        public static readonly BigInteger DEFAULT_GAS_PRICE = BigInteger.Parse("20000000000");
        public static readonly BigInteger DEFAULT_GAS_LIMIT = BigInteger.Parse("21000");
        private static readonly byte[] EMPTY_BYTE_ARRAY = new byte[0];
        private static readonly byte[] ZERO_BYTE_ARRAY = {0};

        private readonly LegacyRlpSigner legacyRlpSigner;
        private readonly RLPSigner eip155Signer;
        private IRlpSigner signer;

        private const string ObsoleteWarningMsg = "This maps to the old Homestead-way of signing, which might be vulnerable to Replay Attacks";

        public Transaction(byte[] rawData)
        {
            legacyRlpSigner = new LegacyRlpSigner(rawData, 6);
            eip155Signer = new RLPSigner(rawData, 6);
            signer = eip155Signer;
            this.Signature = eip155Signer.Signature;
        }

        public Transaction(byte[] nonce, byte[] gasPrice, byte[] gasLimit, byte[] receiveAddress, byte[] value,
            byte[] data)
        {
            legacyRlpSigner = new LegacyRlpSigner(GetElementsInOrder(nonce, gasPrice, gasLimit, receiveAddress, value, data));
            eip155Signer = new RLPSigner(GetElementsInOrder(nonce, gasPrice, gasLimit, receiveAddress, value, data));
            signer = eip155Signer;
            this.Signature = eip155Signer.Signature;
        }

        public Transaction(byte[] nonce, byte[] gasPrice, byte[] gasLimit, byte[] receiveAddress, byte[] value,
            byte[] data, byte[] r, byte[] s, byte v)
        {
            legacyRlpSigner = new LegacyRlpSigner(GetElementsInOrder(nonce, gasPrice, gasLimit, receiveAddress, value, data),
                r, s, v);
            eip155Signer = new RLPSigner(GetElementsInOrder(nonce, gasPrice, gasLimit, receiveAddress, value, data),
                r, s, v);
            signer = eip155Signer;
            this.Signature = eip155Signer.Signature;
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

        public byte[] Hash => signer.Hash;

        public byte[] RawHash => signer.RawHash;

        /// <summary>
        ///     The counter used to make sure each transaction can only be processed once, you may need to regenerate the
        ///     transaction if is too low or too high, simples way is to get the number of transacations
        /// </summary>
        public byte[] Nonce => signer.Data[0] ?? ZERO_BYTE_ARRAY;

        public byte[] Value => signer.Data[4] ?? ZERO_BYTE_ARRAY;

        public byte[] ReceiveAddress => signer.Data[3];

        public byte[] GasPrice => signer.Data[1] ?? ZERO_BYTE_ARRAY;

        public byte[] GasLimit => signer.Data[2];

        public byte[] Data => signer.Data[5];

        public EthECDSASignature Signature { get; private set; }

        public EthECKey Key => signer.Key;

        public byte[] GetRLPEncoded()
        {
            return signer.GetRLPEncoded();
        }

        public byte[] GetRLPEncodedRaw()
        {
            return signer.GetRLPEncodedRaw();
        }

        [Obsolete(ObsoleteWarningMsg)]
        public void Sign(EthECKey key)
        {
            legacyRlpSigner.Sign(key);
            this.Signature = legacyRlpSigner.Signature;
            signer = legacyRlpSigner;
        }

        public void Sign(EthECKey key, int chainId)
        {
            eip155Signer.Sign(key, chainId);
            this.Signature = eip155Signer.Signature;
            signer = eip155Signer;
        }

        public void Sign(EthECKey key, Chain chain)
        {
            Sign(key, (int)chain);
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

        private static string ToHex(byte[] x)
        {
            if (x == null) return "0x";
            return x.ToHex();
        }
    }
}