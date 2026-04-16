using System.Collections.Generic;
using Nethereum.Util;

namespace Nethereum.Model
{
    public class Gas7702
    {
        public const int PER_AUTH_BASE_COST = 12500;
        public const int PER_EMPTY_ACCOUNT_COST	= 25000;
    }

    public class Transaction7702 : SignedTypeTransaction
    {
        public Transaction7702(EvmUInt256 chainId, EvmUInt256? nonce, EvmUInt256? maxPriorityFeePerGas, EvmUInt256? maxFeePerGas,
            EvmUInt256? gasLimit, string receiverAddress, EvmUInt256? amount, string data, List<AccessListItem> accessList,
            List<Authorisation7702Signed> authorisationList)
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
            AuthorisationList = authorisationList;
        }

        public Transaction7702(EvmUInt256 chainId, EvmUInt256? nonce, EvmUInt256? maxPriorityFeePerGas, EvmUInt256? maxFeePerGas,
            EvmUInt256? gasLimit, string receiverAddress, EvmUInt256? amount, string data, List<AccessListItem> accessList,
            List<Authorisation7702Signed> authorisationList, Signature signature) :
            this(chainId, nonce, maxPriorityFeePerGas, maxFeePerGas, gasLimit, receiverAddress, amount, data, accessList, authorisationList)
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
        public List<Authorisation7702Signed> AuthorisationList { get; private set; }

        public override TransactionType TransactionType => TransactionType.EIP7702;

        public override byte[] GetRLPEncoded()
        {
            return OriginalRlpEncoded ?? Transaction7702Encoder.Current.Encode(this);
        }

        public override byte[] GetRLPEncodedRaw()
        {
            return Transaction7702Encoder.Current.EncodeRaw(this);
        }
    }
}
