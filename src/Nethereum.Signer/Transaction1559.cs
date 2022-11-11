using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Util;

namespace Nethereum.Signer
{

    public class Transaction1559: SignedTypeTransaction
    {
        public Transaction1559(BigInteger chainId, BigInteger? nonce, BigInteger? maxPriorityFeePerGas, BigInteger? maxFeePerGas,
            BigInteger? gasLimit, string receiverAddress, BigInteger? amount, string data, List<AccessListItem> accessList)
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

        public Transaction1559(BigInteger chainId, BigInteger nonce, BigInteger maxPriorityFeePerGas, BigInteger maxFeePerGas,
            BigInteger gasLimit, string receiverAddress, BigInteger amount, string data, List<AccessListItem> accessList, EthECDSASignature signature) :
            this(chainId, nonce, maxPriorityFeePerGas, maxFeePerGas, gasLimit, receiverAddress, amount, data, accessList)
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

        public override TransactionType TransactionType => TransactionType.EIP1559;

        public override byte[] GetRLPEncoded()
        {
            return Transaction1559Encoder.Current.Encode(this);
        }

        public override byte[] GetRLPEncodedRaw()
        {
            return Transaction1559Encoder.Current.EncodeRaw(this);
        }

#if !DOTNET35
        public override async Task SignExternallyAsync(IEthExternalSigner externalSigner)
        {
           await  externalSigner.SignAsync(this).ConfigureAwait(false);
        }
#endif
    }
}