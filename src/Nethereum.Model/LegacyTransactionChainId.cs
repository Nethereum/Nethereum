using System;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;
using Nethereum.Util;

namespace Nethereum.Model
{
    public class LegacyTransactionChainId : SignedLegacyTransaction
    {
        public override TransactionType TransactionType => TransactionType.LegacyChainTransaction;
        //The R and S Hashing values
        private static readonly byte[] RHASH_DEFAULT = 0.ToBytesForRLPEncoding();
        private static readonly byte[] SHASH_DEFAULT = 0.ToBytesForRLPEncoding();

        public LegacyTransactionChainId(byte[] rawData, EvmUInt256 chainId)
        {
            //Instantiate and decode
            RlpSignerEncoder = new RLPSignedDataHashBuilder(rawData, NUMBER_ENCODING_ELEMENTS);
            if (Signature != null)
            {
                ValidateValidV(RlpSignerEncoder);
            }
            AppendDataForHashRecovery(chainId);
        }

        public LegacyTransactionChainId(RLPSignedDataHashBuilder rlpSigner)
        {
            RlpSignerEncoder = rlpSigner;
            ValidateValidV(RlpSignerEncoder);
            GetChainIdFromVAndAppendDataForHashRecovery();
        }

        private static void ValidateValidV(RLPSignedDataHashBuilder rlpSigner)
        {
            if (!rlpSigner.IsVSignatureForChain())
                throw new Exception("LegacyTransaction should be used instead of LegacyTransactionChainId, invalid V");
        }
        private void GetChainIdFromVAndAppendDataForHashRecovery()
        {
            var chainId = GetChainFromVChain();
            AppendDataForHashRecovery(chainId);
        }

        private void AppendDataForHashRecovery(EvmUInt256 chainId)
        {
            //append the chainId, r and s so it can be recovered using the raw hash
            //the encoding has only the default 6 values
            RlpSignerEncoder.AppendData(chainId.ToBytesForRLPEncoding(), RHASH_DEFAULT,
                SHASH_DEFAULT);
        }

        public LegacyTransactionChainId(byte[] rawData)
        {
            //Instantiate and decode
            RlpSignerEncoder = new RLPSignedDataHashBuilder(rawData, NUMBER_ENCODING_ELEMENTS);
            ValidateValidV(RlpSignerEncoder);
            GetChainIdFromVAndAppendDataForHashRecovery();
        }

        private EvmUInt256 GetChainFromVChain()
        {
            return VRecoveryAndChainCalculations.GetChainFromVChain(Signature.V.ToEvmUInt256FromRLPDecoded());
        }

        public LegacyTransactionChainId(byte[] nonce, byte[] gasPrice, byte[] gasLimit, byte[] receiveAddress, byte[] value,
            byte[] data, byte[] chainId)
        {
            RlpSignerEncoder =
                new RLPSignedDataHashBuilder(GetElementsInOrder(nonce, gasPrice, gasLimit, receiveAddress, value, data, chainId),
                    NUMBER_ENCODING_ELEMENTS);
        }

        public LegacyTransactionChainId(byte[] nonce, byte[] gasPrice, byte[] gasLimit, byte[] receiveAddress, byte[] value,
            byte[] data, byte[] chainId, byte[] r, byte[] s, byte[] v)
        {
            RlpSignerEncoder = new RLPSignedDataHashBuilder(
                GetElementsInOrder(nonce, gasPrice, gasLimit, receiveAddress, value, data, chainId),
                r, s, v, NUMBER_ENCODING_ELEMENTS);
        }

        public LegacyTransactionChainId(string to, EvmUInt256 amount, EvmUInt256 nonce, EvmUInt256 chainId)
            : this(to, amount, nonce, DEFAULT_GAS_PRICE, DEFAULT_GAS_LIMIT, chainId)
        {
        }

        public LegacyTransactionChainId(string to, EvmUInt256 amount, EvmUInt256 nonce, string data, EvmUInt256 chainId)
            : this(to, amount, nonce, DEFAULT_GAS_PRICE, DEFAULT_GAS_LIMIT, data, chainId)
        {
        }

        public LegacyTransactionChainId(string to, EvmUInt256 amount, EvmUInt256 nonce, EvmUInt256 gasPrice,
            EvmUInt256 gasLimit, EvmUInt256 chainId)
            : this(to, amount, nonce, gasPrice, gasLimit, "", chainId)
        {
        }

        public LegacyTransactionChainId(string to, EvmUInt256 amount, EvmUInt256 nonce, EvmUInt256 gasPrice,
            EvmUInt256 gasLimit, string data, EvmUInt256 chainId) : this(nonce.ToBytesForRLPEncoding(),
            gasPrice.ToBytesForRLPEncoding(),
            gasLimit.ToBytesForRLPEncoding(), to.HexToByteArray(), amount.ToBytesForRLPEncoding(),
            data.HexToByteArray(), chainId.ToBytesForRLPEncoding()
        )
        {
        }

        public LegacyTransactionChainId(string to, EvmUInt256 amount, EvmUInt256 nonce, EvmUInt256 gasPrice,
          EvmUInt256 gasLimit, string data, EvmUInt256 chainId, byte[] r, byte[] s, byte[] v) : this(nonce.ToBytesForRLPEncoding(),
          gasPrice.ToBytesForRLPEncoding(),
          gasLimit.ToBytesForRLPEncoding(), to.HexToByteArray(), amount.ToBytesForRLPEncoding(),
          data.HexToByteArray(), chainId.ToBytesForRLPEncoding(), r, s, v
      )
        {
        }

        public EvmUInt256 GetChainIdAsBigInteger()
        {
            return ChainId.ToEvmUInt256FromRLPDecoded();
        }

        public byte[] ChainId => RlpSignerEncoder.Data[6];

        public byte[] RHash => RlpSignerEncoder.Data[7];

        public byte[] SHash => RlpSignerEncoder.Data[8];



        public string ToJsonHex()
        {
            var data =
                $"['{Nonce.ToHex()}','{GasPrice.ToHex()}','{GasLimit.ToHex()}','{ReceiveAddress.ToHex()}','{Value.ToHex()}','{ToHex(Data)}','{ChainId.ToHex()}','{RHash.ToHex()}','{SHash.ToHex()}'";

            if (Signature != null)
                data = data + $", '{Signature.V.ToHex()}', '{Signature.R.ToHex()}', '{Signature.S.ToHex()}'";
            return data + "]";
        }



        private byte[][] GetElementsInOrder(byte[] nonce, byte[] gasPrice, byte[] gasLimit, byte[] receiveAddress,
            byte[] value,
            byte[] data, byte[] chainId)
        {
            if (receiveAddress == null)
                receiveAddress = DefaultValues.EMPTY_BYTE_ARRAY;
            //order  nonce, gasPrice, gasLimit, receiveAddress, value, data, chainId, r = 0, s =0
            return new[]
            {
                nonce, gasPrice, gasLimit, receiveAddress, value, data, chainId, RHASH_DEFAULT,
                SHASH_DEFAULT
            };
        }


    }

}
