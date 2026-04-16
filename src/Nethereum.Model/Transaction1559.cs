using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.Model
{
    public class Transaction1559 : SignedTypeTransaction
    {
        public Transaction1559(EvmUInt256 chainId, EvmUInt256? nonce, EvmUInt256? maxPriorityFeePerGas, EvmUInt256? maxFeePerGas,
            EvmUInt256? gasLimit, string receiverAddress, EvmUInt256? amount, string data, List<AccessListItem> accessList)
        {
            ChainId = chainId;
            Nonce = nonce;
            MaxPriorityFeePerGas = maxPriorityFeePerGas;
            MaxFeePerGas = maxFeePerGas;
            GasLimit = gasLimit;
            ReceiverAddress = receiverAddress;
            Amount = amount;
            Data = data;
            AccessList = accessList;
        }

        public Transaction1559(EvmUInt256 chainId, EvmUInt256 nonce, EvmUInt256 maxPriorityFeePerGas, EvmUInt256 maxFeePerGas,
            EvmUInt256 gasLimit, string receiverAddress, EvmUInt256 amount, string data, List<AccessListItem> accessList, Signature signature) :
            this(chainId, nonce, maxPriorityFeePerGas, maxFeePerGas, gasLimit, receiverAddress, amount, data, accessList)
        {
            Signature = signature;
        }

        public EvmUInt256 ChainId { get; private set; }
        public EvmUInt256? Nonce { get; private set; }
        public EvmUInt256? MaxPriorityFeePerGas { get; private set; }

        public EvmUInt256? MaxFeePerGas { get; private set; }
        public EvmUInt256? GasLimit { get; private set; }

        public string ReceiverAddress { get; private set; }
        public EvmUInt256? Amount { get; private set; }

        public string Data { get; private set; }
        public List<AccessListItem> AccessList { get; private set; }

        public override TransactionType TransactionType => TransactionType.EIP1559;

        public override byte[] GetRLPEncoded()
        {
            return OriginalRlpEncoded ?? Transaction1559Encoder.Current.Encode(this);
        }

        public override byte[] GetRLPEncodedRaw()
        {
            return Transaction1559Encoder.Current.EncodeRaw(this);
        }

    }
}
