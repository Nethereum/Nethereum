using System.Collections.Generic;
using System.Numerics;

namespace Nethereum.Model
{
    public class Transaction2930: SignedTypeTransaction
    {
        public Transaction2930(BigInteger chainId, BigInteger? nonce, BigInteger? gasPrice,
            BigInteger? gasLimit, string receiverAddress, BigInteger? amount, string data, List<AccessListItem> accessList)
        {
            ChainId = chainId;
            Nonce = nonce;
            GasPrice = gasPrice;
            GasLimit = gasLimit;
            ReceiverAddress = receiverAddress;
            Amount = amount;
            Data = data;
            AccessList = accessList;
        }

        public Transaction2930(BigInteger chainId, BigInteger nonce, BigInteger gasPrice,
            BigInteger gasLimit, string receiverAddress, BigInteger amount, string data, List<AccessListItem> accessList, Signature signature) :
            this(chainId, nonce, gasPrice, gasLimit, receiverAddress, amount, data, accessList)
        {
            Signature = signature;
        }

        public BigInteger ChainId { get; private set; }
        public BigInteger? Nonce { get; private set; }
        public BigInteger? GasPrice { get; private set; }
        public BigInteger? GasLimit { get; private set; }

        public string ReceiverAddress { get; private set; }
        public BigInteger? Amount { get; private set; }

        public string Data { get; private set; }
        public List<AccessListItem> AccessList { get; private set; }

        public override TransactionType TransactionType => TransactionType.LegacyEIP2930;

        public override byte[] GetRLPEncoded()
        {
            return Transaction2930Encoder.Current.Encode(this);
        }

        public override byte[] GetRLPEncodedRaw()
        {
            return Transaction2930Encoder.Current.EncodeRaw(this);
        }

    }
}