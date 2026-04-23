using System.Collections.Generic;
using Nethereum.Documentation;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.SSZ;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Model.SSZ.Tests
{
    public class SszTransaction4844Tests
    {
        private const string TestPrivateKey = "45a915e4d060149eb4365960e6a7a45f334393093061116b197e3240065ff2d8";

        private static Transaction4844 CreateTestTx()
        {
            var accessList = new List<AccessListItem>
            {
                new AccessListItem
                {
                    Address = "0x095e7baea6a6c7c4c2dfeb977efac326af552d87",
                    StorageKeys = new List<byte[]>
                    {
                        new byte[32],
                        "0000000000000000000000000000000000000000000000000000000000000001".HexToByteArray()
                    }
                }
            };

            var blobHashes = new List<byte[]>
            {
                "01a915e4d060149eb4365960e6a7a45f334393093061116b197e3240065f0001".HexToByteArray(),
                "01a915e4d060149eb4365960e6a7a45f334393093061116b197e3240065f0002".HexToByteArray(),
            };

            return new Transaction4844(
                chainId: (EvmUInt256)1,
                nonce: (EvmUInt256)5,
                maxPriorityFeePerGas: (EvmUInt256)2000000000,
                maxFeePerGas: (EvmUInt256)50000000000,
                gasLimit: (EvmUInt256)21000,
                receiverAddress: "0x095e7baea6a6c7c4c2dfeb977efac326af552d87",
                amount: (EvmUInt256)100000,
                data: "0x1234",
                accessList: accessList,
                maxFeePerBlobGas: (EvmUInt256)10000000000,
                blobVersionedHashes: blobHashes);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "eip4844-ssz", "SSZ hash_tree_root for blob transaction")]
        public void ShouldComputeHashTreeRoot()
        {
            var tx = CreateTestTx();
            var signer = new Transaction4844Signer();
            signer.SignTransaction(TestPrivateKey.HexToByteArray(), tx);

            var root = SszTransactionEncoder.Current.HashTreeRootTransaction4844(tx);
            Assert.NotNull(root);
            Assert.Equal(32, root.Length);
            Assert.NotEqual(new byte[32], root);
        }

        [Fact]
        public void ShouldProduceDeterministicRoot()
        {
            var tx = CreateTestTx();
            var signer = new Transaction4844Signer();
            signer.SignTransaction(TestPrivateKey.HexToByteArray(), tx);

            var root1 = SszTransactionEncoder.Current.HashTreeRootTransaction4844(tx);
            var root2 = SszTransactionEncoder.Current.HashTreeRootTransaction4844(tx);
            Assert.Equal(root1, root2);
        }

        [Fact]
        public void ShouldDifferFromTransaction1559Root()
        {
            var tx4844 = CreateTestTx();
            var signer4844 = new Transaction4844Signer();
            signer4844.SignTransaction(TestPrivateKey.HexToByteArray(), tx4844);

            var tx1559 = new Transaction1559(
                (EvmUInt256)1, (EvmUInt256)5, (EvmUInt256)2000000000, (EvmUInt256)50000000000,
                (EvmUInt256)21000, "0x095e7baea6a6c7c4c2dfeb977efac326af552d87",
                (EvmUInt256)100000, "0x1234", null);
            var signer1559 = new Transaction1559Signer();
            signer1559.SignTransaction(TestPrivateKey.HexToByteArray(), tx1559);

            var root4844 = SszTransactionEncoder.Current.HashTreeRootTransaction4844(tx4844);
            var root1559 = SszTransactionEncoder.Current.HashTreeRootTransaction1559(tx1559);

            Assert.NotEqual(root4844, root1559);
        }

        [Fact]
        public void ShouldUseBlobFeesNotBasicFees()
        {
            var tx = CreateTestTx();
            var signer = new Transaction4844Signer();
            signer.SignTransaction(TestPrivateKey.HexToByteArray(), tx);

            var root = SszTransactionEncoder.Current.HashTreeRootTransaction4844(tx);

            var blobFeeRoot = SszTransactionEncoder.Current.HashTreeRootBlobFees(
                tx.MaxFeePerGas, tx.MaxFeePerBlobGas);
            var basicFeeRoot = SszTransactionEncoder.Current.HashTreeRootBasicFees(tx.MaxFeePerGas);

            Assert.NotEqual(blobFeeRoot, basicFeeRoot);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "eip4844-ssz", "SSZ encode/decode blob transaction round-trip")]
        public void ShouldRoundTripSszEncodeDecode()
        {
            var tx = CreateTestTx();
            var signer = new Transaction4844Signer();
            signer.SignTransaction(TestPrivateKey.HexToByteArray(), tx);

            var provider = SszBlockEncodingProvider.Instance;
            var encoded = provider.EncodeTransaction(tx);
            Assert.NotNull(encoded);

            var decoded = provider.DecodeTransaction(encoded);
            Assert.IsType<Transaction4844>(decoded);

            var blob = (Transaction4844)decoded;
            Assert.Equal(tx.ChainId, blob.ChainId);
            Assert.Equal(tx.Nonce, blob.Nonce);
            Assert.Equal(tx.MaxFeePerGas, blob.MaxFeePerGas);
            Assert.Equal(tx.MaxFeePerBlobGas, blob.MaxFeePerBlobGas);
            Assert.Equal(tx.GasLimit, blob.GasLimit);
            Assert.Equal(tx.ReceiverAddress, blob.ReceiverAddress);
            Assert.Equal(tx.Amount, blob.Amount);
            Assert.Equal(2, blob.BlobVersionedHashes.Count);
            for (int i = 0; i < tx.BlobVersionedHashes.Count; i++)
                Assert.Equal(tx.BlobVersionedHashes[i], blob.BlobVersionedHashes[i]);
        }
    }
}
