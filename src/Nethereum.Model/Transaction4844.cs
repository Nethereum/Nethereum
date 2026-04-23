using System.Collections.Generic;
using Nethereum.Util;

namespace Nethereum.Model
{
    public class BlobSidecar
    {
        public List<byte[]> Blobs { get; set; }
        public List<byte[]> Commitments { get; set; }
        public List<byte[]> Proofs { get; set; }

        public BlobSidecar()
        {
            Blobs = new List<byte[]>();
            Commitments = new List<byte[]>();
            Proofs = new List<byte[]>();
        }

        public BlobSidecar(List<byte[]> blobs, List<byte[]> commitments, List<byte[]> proofs)
        {
            Blobs = blobs;
            Commitments = commitments;
            Proofs = proofs;
        }
    }

    public class Transaction4844 : SignedTypeTransaction
    {
        public Transaction4844(EvmUInt256 chainId, EvmUInt256? nonce, EvmUInt256? maxPriorityFeePerGas, EvmUInt256? maxFeePerGas,
            EvmUInt256? gasLimit, string receiverAddress, EvmUInt256? amount, string data, List<AccessListItem> accessList,
            EvmUInt256? maxFeePerBlobGas, List<byte[]> blobVersionedHashes)
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
            MaxFeePerBlobGas = maxFeePerBlobGas;
            BlobVersionedHashes = blobVersionedHashes;
        }

        public Transaction4844(EvmUInt256 chainId, EvmUInt256? nonce, EvmUInt256? maxPriorityFeePerGas, EvmUInt256? maxFeePerGas,
            EvmUInt256? gasLimit, string receiverAddress, EvmUInt256? amount, string data, List<AccessListItem> accessList,
            EvmUInt256? maxFeePerBlobGas, List<byte[]> blobVersionedHashes, Signature signature) :
            this(chainId, nonce, maxPriorityFeePerGas, maxFeePerGas, gasLimit, receiverAddress, amount, data, accessList, maxFeePerBlobGas, blobVersionedHashes)
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
        public EvmUInt256? MaxFeePerBlobGas { get; private set; }
        public List<byte[]> BlobVersionedHashes { get; private set; }
        public BlobSidecar Sidecar { get; set; }

        public override TransactionType TransactionType => TransactionType.Blob;

        public override byte[] GetRLPEncoded()
        {
            return OriginalRlpEncoded ?? Transaction4844Encoder.Current.Encode(this);
        }

        public override byte[] GetRLPEncodedRaw()
        {
            return Transaction4844Encoder.Current.EncodeRaw(this);
        }

        public byte[] GetRLPEncodedWithSidecar()
        {
            return Transaction4844Encoder.Current.EncodeWithSidecar(this);
        }
    }
}
