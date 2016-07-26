using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.ABI.Util.RLP;
using NBitcoin.Crypto;
using Org.BouncyCastle.Asn1.Pkcs;
using Nethereum.ABI.Util;

namespace Nethereum.RPC.Core
{
    public class Transaction
    {
        private static readonly BigInteger DEFAULT_GAS_PRICE = BigInteger.Parse("10000000000000");
        private static readonly BigInteger DEFAULT_BALANCE_GAS = BigInteger.Parse("21000");
        private static readonly byte[] EMPTY_BYTE_ARRAY = new byte[0];
        private static readonly byte[] ZERO_BYTE_ARRAY = { 0 };

        /* SHA3 hash of the RLP encoded transaction */
        private byte[] hash;

        /* a counter used to make sure each transaction can only be processed once */
        private byte[] nonce;

        /* the amount of ether to transfer (calculated as wei) */
        private byte[] value;

        /* the address of the destination account
         * In creation transaction the receive address is - 0 */
        private byte[] receiveAddress;

        /* the amount of ether to pay as a transaction fee
         * to the miner for each unit of gas */
        private byte[] gasPrice;

        /* the amount of "gas" to allow for the computation.
         * Gas is the fuel of the computational engine;
         * every computational step taken and every byte added
         * to the state or transaction list consumes some gas. */
        private byte[] gasLimit;

        /* An unlimited size byte array specifying
         * input [data] of the message call or
         * Initialization code for a new contract */
        private byte[] data;

        /* the elliptic curve signature
         * (including public key recovery bits) */
        private ECDSASignature signature;

        private byte[] sendAddress;

        /* Tx in encoded form */
        private byte[] rlpEncoded;
        private byte[] rlpRaw;
        /* Indicates if this transaction has been parsed
         * from the RLP-encoded data */
        private bool parsed = false;


        public Transaction(byte[] rawData)
        {
            this.rlpEncoded = rawData;
            parsed = false;
        }

        public Transaction(byte[] nonce, byte[] gasPrice, byte[] gasLimit, byte[] receiveAddress, byte[] value, byte[] data)
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

        public Transaction(byte[] nonce, byte[] gasPrice, byte[] gasLimit, byte[] receiveAddress, byte[] value, byte[] data, byte[] r, byte[] s, byte v) : this(nonce, gasPrice, gasLimit, receiveAddress, value, data)
        {
            this.signature = ECDSASignature.FromComponents(r, s, v);
        }

        

        public virtual void RlpParse()
        {

            RLPCollection decodedTxList = RLP.Decode(rlpEncoded);
            RLPCollection transaction = (RLPCollection)decodedTxList[0];

            this.nonce = transaction[0].RLPData;
            this.gasPrice = transaction[1].RLPData;
            this.gasLimit = transaction[2].RLPData;
            this.receiveAddress = transaction[3].RLPData;
            this.value = transaction[4].RLPData;
            this.data = transaction[5].RLPData;
            // only parse signature in case tx is signed
            if (transaction[6].RLPData != null)
            {
                byte v = transaction[6].RLPData[0];
                byte[] r = transaction[7].RLPData;
                byte[] s = transaction[8].RLPData;
                this.signature = ECDSASignature.FromComponents(r, s, v);
            }
            else
            {
                //logger.Debug("RLP encoded tx is not signed!");
            }
            this.parsed = true;
            this.hash = GetHash();
        }

        public bool IsParsed()
        {
            return parsed;
        }

        public byte[] GetHash()
        {
            if (!parsed)
            {
                RlpParse();
            }
            byte[] plainMsg = this.GetEncoded();
            return new Sha3Keccack().CalculateHash(plainMsg);
        }

        public byte[] GetRawHash()
        {
            if (!parsed)
            {
                RlpParse();
            }
            byte[] plainMsg = this.GetEncodedRaw();
            return new Sha3Keccack().CalculateHash(plainMsg);
        }


        public byte[] GetNonce()
        {
            if (!parsed)
            {
                RlpParse();
            }

            return nonce == null ? ZERO_BYTE_ARRAY : nonce;
        }

        public bool IsValueTx()
        {
            if (!parsed)
            {
                RlpParse();
            }
            return value != null;
        }

        public byte[] GetValue()
        {
            if (!parsed)
            {
                RlpParse();
            }
            return value == null ? ZERO_BYTE_ARRAY : value;
        }

        public byte[] GetReceiveAddress()
        {
            if (!parsed)
            {
                RlpParse();
            }
            return receiveAddress;
        }

        public byte[] GetGasPrice()
        {
            if (!parsed)
            {
                RlpParse();
            }
            return gasPrice == null ? ZERO_BYTE_ARRAY : gasPrice;
        }

        public byte[] GetGasLimit()
        {
            if (!parsed)
            {
                RlpParse();
            }
            return gasLimit;
        }
        
        public byte[] GetData()
        {
            if (!parsed)
            {
                RlpParse();
            }
            return data;
        }

        public ECDSASignature GetSignature()
        {
            if (!parsed)
            {
                RlpParse();
            }
            return signature;
        }

        
        public ECKey GetKey()
        {
            byte[] hash = GetRawHash();
            //TODO:
            //return ECKey.RecoverFromSignature(signature.v, signature, hash);
            return null;
        }


        public void Sign(ECKey key)
        {
            this.signature = key.Sign(this.GetRawHash());
            this.rlpEncoded = null;
        }


        /// <summary>
        /// For signatures you have to keep also
        /// RLP of the transaction without any signature data
        /// </summary>
        public byte[] GetEncodedRaw()
        {
            if (!parsed)
            {
                RlpParse();
            }
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
            byte[] gasPrice = RLP.EncodeElement(this.gasPrice);
            byte[] gasLimit = RLP.EncodeElement(this.gasLimit);
            byte[] receiveAddress = RLP.EncodeElement(this.receiveAddress);
            byte[] value = RLP.EncodeElement(this.value);
            byte[] data = RLP.EncodeElement(this.data);

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
            byte[] gasPrice = RLP.EncodeElement(this.gasPrice);
            byte[] gasLimit = RLP.EncodeElement(this.gasLimit);
            byte[] receiveAddress = RLP.EncodeElement(this.receiveAddress);
            byte[] value = RLP.EncodeElement(this.value);
            byte[] data = RLP.EncodeElement(this.data);

            byte[] v, r, s;

            if (signature != null)
            {
                //TODO: V ? R and S bytes? what about encoding / decoding and endianism 
                //v = RLP.EncodeByte(signature.v);
                //r = RLP.EncodeElement(signature.R);
                //s = RLP.EncodeElement(signature.S);
                v = RLP.EncodeElement(EMPTY_BYTE_ARRAY);
                r = RLP.EncodeElement(EMPTY_BYTE_ARRAY);
                s = RLP.EncodeElement(EMPTY_BYTE_ARRAY);

            }
            else
            {
                v = RLP.EncodeElement(EMPTY_BYTE_ARRAY);
                r = RLP.EncodeElement(EMPTY_BYTE_ARRAY);
                s = RLP.EncodeElement(EMPTY_BYTE_ARRAY);
            }

            this.rlpEncoded = RLP.EncodeList(nonce, gasPrice, gasLimit, receiveAddress, value, data, v, r, s);

            this.hash = this.GetHash();

            return rlpEncoded;
        }

        public override int GetHashCode()
        {

            byte[] hash = this.GetHash();
            int hashCode = 0;

            for (int i = 0; i < hash.Length; ++i)
            {
                hashCode += hash[i] * i;
            }

            return hashCode;
        }

        public override bool Equals(object obj)
        {

            if (!(obj is Transaction))
            {
                return false;
            }
            Transaction tx = (Transaction)obj;

            return tx.GetHashCode() == this.GetHashCode();
        }

        //public static Transaction CreateDefault(string to, BigInteger amount, BigInteger nonce)
        //{
        //    return Create(to, amount, nonce, DEFAULT_GAS_PRICE, DEFAULT_BALANCE_GAS);
        //}

        //public static Transaction Create(string to, BigInteger amount, BigInteger nonce, BigInteger gasPrice, BigInteger gasLimit)
        //{
             //TODO: USE RLP PRE ENCODERS
        //}
    }


}

