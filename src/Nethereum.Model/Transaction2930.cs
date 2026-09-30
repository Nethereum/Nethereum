using System.Collections.Generic;
using Nethereum.Util;

namespace Nethereum.Model
{
    public class Transaction2930: SignedTypeTransaction
    {
        public Transaction2930(EvmUInt256 chainId, EvmUInt256? nonce, EvmUInt256? gasPrice,
            EvmUInt256? gasLimit, string receiverAddress, EvmUInt256? amount, string data, List<AccessListItem> accessList)
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

        public Transaction2930(EvmUInt256 chainId, EvmUInt256 nonce, EvmUInt256 gasPrice,
            EvmUInt256 gasLimit, string receiverAddress, EvmUInt256 amount, string data, List<AccessListItem> accessList, Signature signature) :
            this(chainId, nonce, gasPrice, gasLimit, receiverAddress, amount, data, accessList)
        {
            Signature = signature;
        }

        public EvmUInt256 ChainId { get; private set; }
        public EvmUInt256? Nonce { get; private set; }
        public EvmUInt256? GasPrice { get; private set; }
        public EvmUInt256? GasLimit { get; private set; }

        public string ReceiverAddress { get; private set; }
        public EvmUInt256? Amount { get; private set; }

        public string Data { get; private set; }
        public List<AccessListItem> AccessList { get; private set; }

        public override TransactionType TransactionType => TransactionType.LegacyEIP2930;

        public override byte[] GetRLPEncoded()
        {
            return OriginalRlpEncoded ?? Transaction2930Encoder.Current.Encode(this);
        }

        public override byte[] GetRLPEncodedRaw()
        {
            return Transaction2930Encoder.Current.EncodeRaw(this);
        }

    }
}
