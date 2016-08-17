using System.Numerics;
using NBitcoin.Crypto;
using Nethereum.ABI.Util;
using Nethereum.ABI.Util.RLP;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Core.Signing.Crypto;

namespace Nethereum.Core
{
    public class Transaction
    {
        private static readonly BigInteger DEFAULT_GAS_PRICE = BigInteger.Parse("10000000000000");
        private static readonly BigInteger DEFAULT_BALANCE_GAS = BigInteger.Parse("21000");
        private static readonly byte[] EMPTY_BYTE_ARRAY = new byte[0];
        private static readonly byte[] ZERO_BYTE_ARRAY = {0};

        private byte[] data;
        private bool decoded;
        private byte[] gasLimit;
        private byte[] gasPrice;
        private byte[] nonce;
        private byte[] receiveAddress;

        private byte[] rlpEncoded;
        private byte[] rlpRaw;

        private ECDSASignature signature;
        private byte[] value;

        public Transaction(byte[] rawData)
        {
            rlpEncoded = rawData;
            decoded = false;
        }

        public Transaction(byte[] nonce, byte[] gasPrice, byte[] gasLimit, byte[] receiveAddress, byte[] value,
            byte[] data)
        {
            this.nonce = nonce;
            this.gasPrice = gasPrice;
            this.gasLimit = gasLimit;
            this.receiveAddress = receiveAddress;
            this.value = value;
            this.data = data;

            if (receiveAddress == null)
            {
                this.receiveAddress = EMPTY_BYTE_ARRAY;
            }

            decoded = true;
        }

        public Transaction(byte[] nonce, byte[] gasPrice, byte[] gasLimit, byte[] receiveAddress, byte[] value,
            byte[] data, byte[] r, byte[] s, byte v) : this(nonce, gasPrice, gasLimit, receiveAddress, value, data)
        {
            signature = EthECDSASignatureFactory.FromComponents(r, s, v);
        }

        public byte[] Hash
        {
            get
            {
                EnsuredRPLDecoded();
                var plainMsg = GetRLPEncoded();
                return new Sha3Keccack().CalculateHash(plainMsg);
            }
        }

        public byte[] RawHash
        {
            get
            {
                EnsuredRPLDecoded();
                var plainMsg = GetRLPEncodedRaw();
                return new Sha3Keccack().CalculateHash(plainMsg);
            }
        }

        /// <summary>
        ///     The counter used to make sure each transaction can only be processed once, you may need to regenerate the
        ///     transaction if is too low or too high, simples way is to get the number of transacations
        /// </summary>
        public byte[] Nonce
        {
            get
            {
                EnsuredRPLDecoded();
                return nonce ?? ZERO_BYTE_ARRAY;
            }
        }

        public byte[] Value
        {
            get
            {
                EnsuredRPLDecoded();
                return value ?? ZERO_BYTE_ARRAY;
            }
        }

        public byte[] ReceiveAddress
        {
            get
            {
                EnsuredRPLDecoded();
                return receiveAddress;
            }
        }

        public byte[] GasPrice
        {
            get
            {
                EnsuredRPLDecoded();
                return gasPrice ?? ZERO_BYTE_ARRAY;
            }
        }

        public byte[] GasLimit
        {
            get
            {
                EnsuredRPLDecoded();
                return gasLimit;
            }
        }

        public byte[] Data
        {
            get
            {
                EnsuredRPLDecoded();
                return data;
            }
        }

        public ECDSASignature Signature
        {
            get
            {
                EnsuredRPLDecoded();
                return signature;
            }
        }

        public ECKey Key
        {
            get
            {
                return EthECKey.RecoverFromSignature(Signature, RawHash);
            }   
        } 


        public virtual void RlpDecode()
        {
            var decodedTxList = RLP.Decode(rlpEncoded);
            var transaction = (RLPCollection) decodedTxList[0];

            nonce = transaction[0].RLPData;
            gasPrice = transaction[1].RLPData;
            gasLimit = transaction[2].RLPData;
            receiveAddress = transaction[3].RLPData;
            value = transaction[4].RLPData;
            data = transaction[5].RLPData;
            // only parse signature in case tx is signed
            if (transaction[6].RLPData != null)
            {
                var v = transaction[6].RLPData[0];
                var r = transaction[7].RLPData;
                var s = transaction[8].RLPData;

                signature = EthECDSASignatureFactory.FromComponents(r, s, v);
            }
            decoded = true;
        }

        public string ToJsonHex()
        {
            var s = "['{0}','{1}','{2}','{3}','{4}','{5}','{6}','{7}','{8}']";
            return string.Format(s, nonce.ToHex(),
                gasPrice.ToHex(), gasLimit.ToHex(), receiveAddress.ToHex(), value.ToHex(), ToHex(data),
                signature.V.ToString("X"),
                signature.R.ToByteArrayUnsigned().ToHex(),
                signature.S.ToByteArrayUnsigned().ToHex());
        }

        private string ToHex(byte[] x)
        {
            if (x == null) return "0x";
            return x.ToHex();
        }

        private void EnsuredRPLDecoded()
        {
            if (!decoded)
            {
                RlpDecode();
            }
        }

        public void Sign(ECKey key)
        {
            signature = key.SignAndCalculateV(RawHash);
            rlpEncoded = null;
        }

        public byte[] GetRLPEncodedRaw()
        {
            EnsuredRPLDecoded();

            if (rlpRaw != null)
            {
                return rlpRaw;
            }
            rlpRaw = BuildRLPEncoded(true);
            return rlpRaw;
        }

        public byte[] BuildRLPEncoded(bool raw)
        {
            byte[] nonce = null;
            if (this.nonce == null || this.nonce.Length == 1 && this.nonce[0] == 0)
            {
                nonce = RLP.EncodeElement(null);
            }
            else
            {
                nonce = RLP.EncodeElement(this.nonce);
            }
            var gasPrice = RLP.EncodeElement(this.gasPrice);
            var gasLimit = RLP.EncodeElement(this.gasLimit);
            var receiveAddress = RLP.EncodeElement(this.receiveAddress);
            var value = RLP.EncodeElement(this.value);
            var data = RLP.EncodeElement(this.data);

            if (raw)
            {
                return RLP.EncodeList(nonce, gasPrice, gasLimit, receiveAddress, value, data);
            }
            byte[] v, r, s;

            if (signature != null)
            {
                v = RLP.EncodeByte(signature.V);
                r = RLP.EncodeElement(signature.R.ToByteArrayUnsigned());
                s = RLP.EncodeElement(signature.S.ToByteArrayUnsigned());
            }
            else
            {
                v = RLP.EncodeElement(EMPTY_BYTE_ARRAY);
                r = RLP.EncodeElement(EMPTY_BYTE_ARRAY);
                s = RLP.EncodeElement(EMPTY_BYTE_ARRAY);
            }

            return RLP.EncodeList(nonce, gasPrice, gasLimit, receiveAddress, value, data, v, r, s);
        }

        public byte[] GetRLPEncoded()
        {
            if (rlpEncoded != null)
            {
                return rlpEncoded;
            }
            rlpEncoded = BuildRLPEncoded(false);
            return rlpEncoded;
        }

        public Transaction (string to, BigInteger amount, BigInteger nonce):this(to, amount, nonce, DEFAULT_GAS_PRICE, DEFAULT_BALANCE_GAS) { 
        }

        public Transaction(string to, BigInteger amount, BigInteger nonce, string data) : this(to, amount, nonce, DEFAULT_GAS_PRICE, DEFAULT_BALANCE_GAS, data)
        {
        }

        public Transaction(string to, BigInteger amount, BigInteger nonce, BigInteger gasPrice,BigInteger gasLimit) : this(to, amount, nonce, gasPrice, gasLimit, "")
        {

        }

        public Transaction(string to, BigInteger amount, BigInteger nonce, BigInteger gasPrice,
            BigInteger gasLimit, string data) : this(nonce.ToBytesForRLPEncoding(), gasPrice.ToBytesForRLPEncoding(),
                gasLimit.ToBytesForRLPEncoding(), to.HexToByteArray(), amount.ToBytesForRLPEncoding(), data.HexToByteArray())
        {

        }

    }
}