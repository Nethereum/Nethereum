using System.Collections.Generic;
using System.Numerics;

namespace Nethereum.Model
{
    public class Transaction7702 : SignedTypeTransaction
    {
        public Transaction7702(BigInteger chainId, BigInteger? nonce, BigInteger? maxPriorityFeePerGas, BigInteger? maxFeePerGas,
            BigInteger? gasLimit, string receiverAddress, BigInteger? amount, string data, List<AccessListItem> accessList,
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

        public Transaction7702(BigInteger chainId, BigInteger? nonce, BigInteger? maxPriorityFeePerGas, BigInteger? maxFeePerGas,
            BigInteger? gasLimit, string receiverAddress, BigInteger? amount, string data, List<AccessListItem> accessList,
            List<Authorisation7702Signed> authorisationList, Signature signature) :
            this(chainId, nonce, maxPriorityFeePerGas, maxFeePerGas, gasLimit, receiverAddress, amount, data, accessList, authorisationList)
        {
            Signature = signature;
        }

        public BigInteger ChainId { get; private set; }
        public BigInteger? Nonce { get; private set; }
        public BigInteger? MaxPriorityFeePerGas { get; private set; }
        public BigInteger? MaxFeePerGas { get; private set; }
        public BigInteger? GasLimit { get; private set; }
        public string ReceiverAddress { get; private set; }
        public BigInteger? Amount { get; private set; }
        public string Data { get; private set; }
        public List<AccessListItem> AccessList { get; private set; }
        public List<Authorisation7702Signed> AuthorisationList { get; private set; }

        public override TransactionType TransactionType => TransactionType.EIP7702;

        public override byte[] GetRLPEncoded()
        {
            return Transaction7702Encoder.Current.Encode(this);
        }

        public override byte[] GetRLPEncodedRaw()
        {
            return Transaction7702Encoder.Current.EncodeRaw(this);
        }
    }
}