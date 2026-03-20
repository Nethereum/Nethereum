using System;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.SSZ;
using Xunit;

namespace Nethereum.Model.SSZ.Tests
{
    public class SszTransaction1559RoundTripTests
    {
        [Fact]
        public void SimpleTransfer_RoundTrip()
        {
            var original = new Transaction1559(
                chainId: 1,
                nonce: 42,
                maxPriorityFeePerGas: 2000000000,
                maxFeePerGas: 30000000000,
                gasLimit: 21000,
                receiverAddress: "0xbeef000000000000000000000000000000000001",
                amount: 1000000000000000000,
                data: "",
                accessList: new List<AccessListItem>());

            var encoded = SszTransactionEncoder.Current.EncodeTransaction1559Payload(original);
            var decoded = SszTransactionEncoder.Current.DecodeTransaction1559Payload(encoded, false);

            Assert.Equal(original.ChainId, decoded.ChainId);
            Assert.Equal(original.Nonce, decoded.Nonce);
            Assert.Equal(original.MaxPriorityFeePerGas, decoded.MaxPriorityFeePerGas);
            Assert.Equal(original.MaxFeePerGas, decoded.MaxFeePerGas);
            Assert.Equal(original.GasLimit, decoded.GasLimit);
            Assert.Equal(original.ReceiverAddress.ToLower(), decoded.ReceiverAddress.ToLower());
            Assert.Equal(original.Amount, decoded.Amount);
        }

        [Fact]
        public void TransferWithData_RoundTrip()
        {
            // ERC-20 transfer: transfer(address,uint256)
            var calldata = "0xa9059cbb000000000000000000000000dead0000000000000000000000000000000000010000000000000000000000000000000000000000000000000de0b6b3a7640000";

            var original = new Transaction1559(
                chainId: 1,
                nonce: 100,
                maxPriorityFeePerGas: 1500000000,
                maxFeePerGas: 50000000000,
                gasLimit: 65000,
                receiverAddress: "0xdAC17F958D2ee523a2206206994597C13D831ec7",
                amount: 0,
                data: calldata,
                accessList: new List<AccessListItem>());

            var encoded = SszTransactionEncoder.Current.EncodeTransaction1559Payload(original);
            var decoded = SszTransactionEncoder.Current.DecodeTransaction1559Payload(encoded, false);

            Assert.Equal(original.ChainId, decoded.ChainId);
            Assert.Equal(original.Nonce, decoded.Nonce);
            Assert.Equal(original.GasLimit, decoded.GasLimit);
            Assert.Equal(original.Amount, decoded.Amount);
            Assert.Equal(original.Data.ToLower(), decoded.Data.ToLower());
        }

        [Fact]
        public void WithAccessList_RoundTrip()
        {
            var slot = "0x0000000000000000000000000000000000000000000000000000000000000001".HexToByteArray();

            var original = new Transaction1559(
                chainId: 1,
                nonce: 5,
                maxPriorityFeePerGas: 2000000000,
                maxFeePerGas: 30000000000,
                gasLimit: 100000,
                receiverAddress: "0xbeef000000000000000000000000000000000001",
                amount: 0,
                data: "0x",
                accessList: new List<AccessListItem>
                {
                    new AccessListItem("0xdead000000000000000000000000000000000001",
                        new List<byte[]> { slot }),
                    new AccessListItem("0xcafe000000000000000000000000000000000003",
                        new List<byte[]>())
                });

            var encoded = SszTransactionEncoder.Current.EncodeTransaction1559Payload(original);
            var decoded = SszTransactionEncoder.Current.DecodeTransaction1559Payload(encoded, false);

            Assert.Equal(2, decoded.AccessList.Count);
            Assert.Equal(original.AccessList[0].Address.ToLower(),
                decoded.AccessList[0].Address.ToLower());
            Assert.Single(decoded.AccessList[0].StorageKeys);
            Assert.Equal(slot, decoded.AccessList[0].StorageKeys[0]);
            Assert.Empty(decoded.AccessList[1].StorageKeys);
        }

        [Fact]
        public void ZeroValues_RoundTrip()
        {
            var original = new Transaction1559(
                chainId: 1,
                nonce: 0,
                maxPriorityFeePerGas: 0,
                maxFeePerGas: 0,
                gasLimit: 21000,
                receiverAddress: "0x0000000000000000000000000000000000000000",
                amount: 0,
                data: "",
                accessList: new List<AccessListItem>());

            var encoded = SszTransactionEncoder.Current.EncodeTransaction1559Payload(original);
            var decoded = SszTransactionEncoder.Current.DecodeTransaction1559Payload(encoded, false);

            Assert.Equal(BigInteger.Zero, decoded.Nonce);
            Assert.Equal(BigInteger.Zero, decoded.MaxPriorityFeePerGas);
            Assert.Equal(BigInteger.Zero, decoded.MaxFeePerGas);
            Assert.Equal(BigInteger.Zero, decoded.Amount);
        }

        [Fact]
        public void LargeValues_RoundTrip()
        {
            // Max uint64 nonce, large fee values
            var original = new Transaction1559(
                chainId: 137, // Polygon
                nonce: (BigInteger)ulong.MaxValue,
                maxPriorityFeePerGas: 30000000000000, // 30 Twei
                maxFeePerGas: 100000000000000, // 100 Twei
                gasLimit: 10000000,
                receiverAddress: "0xbeef000000000000000000000000000000000001",
                amount: BigInteger.Parse("115792089237316195423570985008687907853269984665640564039457584007913129639935"), // uint256 max
                data: "",
                accessList: new List<AccessListItem>());

            var encoded = SszTransactionEncoder.Current.EncodeTransaction1559Payload(original);
            var decoded = SszTransactionEncoder.Current.DecodeTransaction1559Payload(encoded, false);

            Assert.Equal((BigInteger)137, decoded.ChainId);
            Assert.Equal((BigInteger)ulong.MaxValue, decoded.Nonce);
            Assert.Equal(original.Amount, decoded.Amount);
        }

        [Fact]
        public void HashTreeRoot_StableAcrossEncodeDecode()
        {
            var original = new Transaction1559(
                chainId: 1,
                nonce: 42,
                maxPriorityFeePerGas: 2000000000,
                maxFeePerGas: 30000000000,
                gasLimit: 21000,
                receiverAddress: "0xbeef000000000000000000000000000000000001",
                amount: 1000000000000000000,
                data: "0xa9059cbb",
                accessList: new List<AccessListItem>
                {
                    new AccessListItem("0xdead000000000000000000000000000000000001",
                        new List<byte[]> { new byte[32] })
                });

            var rootBefore = SszTransactionEncoder.Current.HashTreeRootTransaction1559(original);

            var encoded = SszTransactionEncoder.Current.EncodeTransaction1559Payload(original);
            var decoded = SszTransactionEncoder.Current.DecodeTransaction1559Payload(encoded, false);

            var rootAfter = SszTransactionEncoder.Current.HashTreeRootTransaction1559(decoded);
            Assert.Equal(rootBefore, rootAfter);
        }

        [Fact]
        public void ContractCreation_RoundTrip()
        {
            // Contract creation: no receiver address
            var initcode = "0x608060405234801561001057600080fd5b50610150806100206000396000f3fe";
            var original = new Transaction1559(
                chainId: 1,
                nonce: 0,
                maxPriorityFeePerGas: 2000000000,
                maxFeePerGas: 30000000000,
                gasLimit: 500000,
                receiverAddress: null, // contract creation
                amount: 0,
                data: initcode,
                accessList: new List<AccessListItem>());

            var encoded = SszTransactionEncoder.Current.EncodeTransaction1559Payload(original);
            var decoded = SszTransactionEncoder.Current.DecodeTransaction1559Payload(encoded, isCreate: true);

            Assert.Null(decoded.ReceiverAddress);
            Assert.Equal(original.Data.ToLower(), decoded.Data.ToLower());
            Assert.Equal(original.GasLimit, decoded.GasLimit);
        }

        [Fact]
        public void ContractCreation_HashTreeRoot_StableAcrossEncodeDecode()
        {
            var original = new Transaction1559(
                chainId: 1, nonce: 0, maxPriorityFeePerGas: 2000000000,
                maxFeePerGas: 30000000000, gasLimit: 500000,
                receiverAddress: null, amount: 0,
                data: "0x6080604052", accessList: new List<AccessListItem>());

            var rootBefore = SszTransactionEncoder.Current.HashTreeRootTransaction1559(original);
            var encoded = SszTransactionEncoder.Current.EncodeTransaction1559Payload(original);
            var decoded = SszTransactionEncoder.Current.DecodeTransaction1559Payload(encoded, isCreate: true);
            var rootAfter = SszTransactionEncoder.Current.HashTreeRootTransaction1559(decoded);

            Assert.Equal(rootBefore, rootAfter);
        }

        [Fact]
        public void FullE2E_EncodePayload_WrapInTransaction_VerifyHash()
        {
            var tx = new Transaction1559(
                chainId: 1, nonce: 42,
                maxPriorityFeePerGas: 2000000000,
                maxFeePerGas: 30000000000,
                gasLimit: 21000,
                receiverAddress: "0xbeef000000000000000000000000000000000001",
                amount: 1000000000000000000,
                data: "",
                accessList: new List<AccessListItem>());

            // 1. Compute hash_tree_root from the model object
            var txRoot = SszTransactionEncoder.Current.HashTreeRootTransaction1559(tx);
            Assert.Equal(32, txRoot.Length);

            // 2. Encode payload
            var payloadEncoded = SszTransactionEncoder.Current.EncodeTransaction1559Payload(tx);

            // 3. Wrap in transaction container
            var sigBytes = new byte[65]; // dummy signature
            var fullTx = SszTransactionEncoder.Current.EncodeTransaction(
                SszTransactionEncoder.SelectorRlpBasic, payloadEncoded, sigBytes);

            // 4. Verify non-trivial encoding
            Assert.True(fullTx.Length > payloadEncoded.Length);
            Assert.Equal(SszTransactionEncoder.SelectorRlpBasic, fullTx[0]);

            // 5. Decode payload back and verify hash stability
            var decoded = SszTransactionEncoder.Current.DecodeTransaction1559Payload(payloadEncoded, false);
            var recomputedRoot = SszTransactionEncoder.Current.HashTreeRootTransaction1559(decoded);
            Assert.Equal(txRoot, recomputedRoot);
        }
    }
}
