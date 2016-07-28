using System;
using System.Numerics;
using NBitcoin.Crypto;
using Nethereum.ABI.Util;
using Nethereum.ABI.Util.RLP;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.RPC.Core
{
    public class Transaction
    {
        private static readonly BigInteger DEFAULT_GAS_PRICE = BigInteger.Parse("10000000000000");
        private static readonly BigInteger DEFAULT_BALANCE_GAS = BigInteger.Parse("21000");
        private static readonly byte[] EMPTY_BYTE_ARRAY = new byte[0];
        private static readonly byte[] ZERO_BYTE_ARRAY = {0};

        /* An unlimited size byte array specifying
         * input [data] of the message call or
         * Initialization code for a new contract */
        private byte[] data;

        /* the amount of "gas" to allow for the computation.
         * Gas is the fuel of the computational engine;
         * every computational step taken and every byte added
         * to the state or transaction list consumes some gas. */
        private byte[] gasLimit;

        /* the amount of ether to pay as a transaction fee
         * to the miner for each unit of gas */
        private byte[] gasPrice;

        /* a counter used to make sure each transaction can only be processed once */
        private byte[] nonce;
        /* Indicates if this transaction has been parsed
         * from the RLP-encoded data */
        private bool parsed;

        /* the address of the destination account
         * In creation transaction the receive address is - 0 */
        private byte[] receiveAddress;

        /* Tx in encoded form */
        private byte[] rlpEncoded;
        private byte[] rlpRaw;

        private byte[] sendAddress;

        /* the elliptic curve signature
         * (including public key recovery bits) */
        private ECDSASignature signature;

        /* the amount of ether to transfer (calculated as wei) */
        private byte[] value;


        public Transaction(byte[] rawData)
        {
            rlpEncoded = rawData;
            parsed = false;
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

            parsed = true;
        }

        public Transaction(byte[] nonce, byte[] gasPrice, byte[] gasLimit, byte[] receiveAddress, byte[] value,
            byte[] data, byte[] r, byte[] s, byte v) : this(nonce, gasPrice, gasLimit, receiveAddress, value, data)
        {
            signature = ECDSASignature.FromComponents(r, s, v);
        }


        public virtual void RlpParse()
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
                signature = ECDSASignature.FromComponents(r.ToBigIntegerFromRLPDecoded().ToByteArray(), s.ToBigIntegerFromRLPDecoded().ToByteArray(), v);
            }
            parsed = true;
        }

        public bool IsParsed()
        {
            return parsed;
        }

        public byte[] GetHash()
        {
            EnsuredRPLParsed();
            var plainMsg = GetEncoded();
            return new Sha3Keccack().CalculateHash(plainMsg);
        }

        public byte[] GetRawHash()
        {
            EnsuredRPLParsed();
            var plainMsg = GetEncodedRaw();
            return new Sha3Keccack().CalculateHash(plainMsg);
        }

        public byte[] GetNonce()
        {
            EnsuredRPLParsed();
            return nonce == null ? ZERO_BYTE_ARRAY : nonce;
        }

        public bool IsValueTx()
        {
            EnsuredRPLParsed();
            return value != null;
        }

        private void EnsuredRPLParsed()
        {
            if (!parsed)
            {
                RlpParse();
            }
        }

        public byte[] GetValue()
        {
            EnsuredRPLParsed();
            return value == null ? ZERO_BYTE_ARRAY : value;
        }

        public byte[] GetReceiveAddress()
        {
            EnsuredRPLParsed();
            return receiveAddress;
        }

        public byte[] GetGasPrice()
        {
            EnsuredRPLParsed();
            return gasPrice == null ? ZERO_BYTE_ARRAY : gasPrice;
        }

        public byte[] GetGasLimit()
        {
            EnsuredRPLParsed();
            return gasLimit;
        }

        public byte[] GetData()
        {
            EnsuredRPLParsed();
            return data;
        }

        public ECDSASignature GetSignature()
        {
            EnsuredRPLParsed();
            return signature;
        }


        public ECKey GetKey()
        {
            var hash = GetRawHash();
            return ECKey.RecoverFromSignature(GetRecIdFromV(signature),signature, hash, false);   
        }

        public static int GetRecIdFromV(ECDSASignature sig)
        {
            int header = sig.V;
            // The header byte: 0x1B = first key with even y, 0x1C = first key with odd y,
            //                  0x1D = second key with even y, 0x1E = second key with odd y
            if (header < 27 || header > 34)
            {
                throw new Exception("Header byte out of range: " + header);
            }
            if (header >= 31)
            {
                header -= 4;
            }
            return header - 27;
        }


        public void Sign(ECKey key)
        {
            //throw new NotImplementedException();
            signature = key.Sign(GetRawHash());
            rlpEncoded = null;
        }


        /// <summary>
        ///     For signatures you have to keep also
        ///     RLP of the transaction without any signature data
        /// </summary>
        public byte[] GetEncodedRaw()
        {
            EnsuredRPLParsed();

            if (rlpRaw != null)
            {
                return rlpRaw;
            }

            // parse null as 0 for nonce
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

            rlpRaw = RLP.EncodeList(nonce, gasPrice, gasLimit, receiveAddress, value, data);
            return rlpRaw;
        }

        public byte[] GetEncoded()
        {
            if (rlpEncoded != null)
            {
                return rlpEncoded;
            }

            // parse null as 0 for nonce
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

            byte[] v, r, s;

            if (signature != null)
            {
                v = RLP.EncodeByte(signature.V);
                r = RLP.EncodeElement(new BigInteger(signature.R.ToByteArrayUnsigned()).ToBytesForRLPEncoding());
                s = RLP.EncodeElement(new BigInteger(signature.S.ToByteArrayUnsigned()).ToBytesForRLPEncoding());
            }
            else
            {
                v = RLP.EncodeElement(EMPTY_BYTE_ARRAY);
                r = RLP.EncodeElement(EMPTY_BYTE_ARRAY);
                s = RLP.EncodeElement(EMPTY_BYTE_ARRAY);
            }

            rlpEncoded = RLP.EncodeList(nonce, gasPrice, gasLimit, receiveAddress, value, data, v, r, s);

            return rlpEncoded;
        }

        public static Transaction Create(string to, BigInteger amount, BigInteger nonce)
        {
            return Create(to, amount, nonce, DEFAULT_GAS_PRICE, DEFAULT_BALANCE_GAS);
        }

        public static Transaction Create(string to, BigInteger amount, BigInteger nonce, BigInteger gasPrice,
            BigInteger gasLimit)
        {
            return new Transaction(nonce.ToBytesForRLPEncoding(), gasPrice.ToBytesForRLPEncoding(),
                gasLimit.ToBytesForRLPEncoding(), to.ToBytesForRLPEncoding(), amount.ToBytesForRLPEncoding(), null);
        }
    }
}