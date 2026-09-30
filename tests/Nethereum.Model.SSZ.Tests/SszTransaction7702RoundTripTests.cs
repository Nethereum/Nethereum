using System.Collections.Generic;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.SSZ;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Model.SSZ.Tests
{
    public class SszTransaction7702RoundTripTests
    {
        [Fact]
        public void SimpleCall_RoundTrip()
        {
            var original = new Transaction7702(
                chainId: 1,
                nonce: 10,
                maxPriorityFeePerGas: 2000000000,
                maxFeePerGas: 30000000000,
                gasLimit: 50000,
                receiverAddress: "0xbeef000000000000000000000000000000000001",
                amount: 0,
                data: "",
                accessList: new List<AccessListItem>(),
                authorisationList: new List<Authorisation7702Signed>());

            var encoded = SszTransactionEncoder.Current.EncodeTransaction7702Payload(original);
            var decoded = SszTransactionEncoder.Current.DecodeTransaction7702Payload(encoded);

            Assert.Equal(original.ChainId, decoded.ChainId);
            Assert.Equal(original.Nonce, decoded.Nonce);
            Assert.Equal(original.MaxPriorityFeePerGas, decoded.MaxPriorityFeePerGas);
            Assert.Equal(original.MaxFeePerGas, decoded.MaxFeePerGas);
            Assert.Equal(original.GasLimit, decoded.GasLimit);
            Assert.Equal(original.ReceiverAddress.ToLower(), decoded.ReceiverAddress.ToLower());
            Assert.Equal(original.Amount, decoded.Amount);
            Assert.Empty(decoded.AuthorisationList);
        }

        [Fact]
        public void WithAuthorisations_RoundTrip()
        {
            var auth1 = new Authorisation7702Signed(
                chainId: 1,
                address: "0xdead000000000000000000000000000000000001",
                nonce: 0,
                r: "0xabcdef0000000000000000000000000000000000000000000000000000000001".HexToByteArray(),
                s: "0x1234560000000000000000000000000000000000000000000000000000000002".HexToByteArray(),
                v: new byte[] { 0x01 });

            var auth2 = new Authorisation7702Signed(
                chainId: 137,
                address: "0xcafe000000000000000000000000000000000003",
                nonce: 5,
                r: new byte[32],
                s: new byte[32],
                v: new byte[] { 0x00 });

            var original = new Transaction7702(
                chainId: 1,
                nonce: 42,
                maxPriorityFeePerGas: 2000000000,
                maxFeePerGas: 30000000000,
                gasLimit: 100000,
                receiverAddress: "0xbeef000000000000000000000000000000000001",
                amount: 0,
                data: "0xa9059cbb",
                accessList: new List<AccessListItem>(),
                authorisationList: new List<Authorisation7702Signed> { auth1, auth2 });

            var encoded = SszTransactionEncoder.Current.EncodeTransaction7702Payload(original);
            var decoded = SszTransactionEncoder.Current.DecodeTransaction7702Payload(encoded);

            Assert.Equal(original.ChainId, decoded.ChainId);
            Assert.Equal(original.Nonce, decoded.Nonce);
            Assert.Equal(2, decoded.AuthorisationList.Count);

            Assert.Equal(auth1.ChainId, decoded.AuthorisationList[0].ChainId);
            Assert.Equal(auth1.Address.ToLower(), decoded.AuthorisationList[0].Address.ToLower());
            Assert.Equal(auth1.Nonce, decoded.AuthorisationList[0].Nonce);
            Assert.Equal(auth1.R, decoded.AuthorisationList[0].R);
            Assert.Equal(auth1.S, decoded.AuthorisationList[0].S);
            Assert.Equal(auth1.V, decoded.AuthorisationList[0].V);

            Assert.Equal((EvmUInt256)137, decoded.AuthorisationList[1].ChainId);
        }

        [Fact]
        public void WithAccessListAndAuths_RoundTrip()
        {
            var slot = "0x0000000000000000000000000000000000000000000000000000000000000001".HexToByteArray();

            var original = new Transaction7702(
                chainId: 1,
                nonce: 5,
                maxPriorityFeePerGas: 1000000000,
                maxFeePerGas: 20000000000,
                gasLimit: 200000,
                receiverAddress: "0xbeef000000000000000000000000000000000001",
                amount: 500000000000000000,
                data: "0x",
                accessList: new List<AccessListItem>
                {
                    new AccessListItem("0xdead000000000000000000000000000000000001",
                        new List<byte[]> { slot })
                },
                authorisationList: new List<Authorisation7702Signed>
                {
                    new Authorisation7702Signed(1,
                        "0xdead000000000000000000000000000000000001", 0,
                        new byte[32], new byte[32], new byte[] { 0x00 })
                });

            var encoded = SszTransactionEncoder.Current.EncodeTransaction7702Payload(original);
            var decoded = SszTransactionEncoder.Current.DecodeTransaction7702Payload(encoded);

            Assert.Single(decoded.AccessList);
            Assert.Single(decoded.AccessList[0].StorageKeys);
            Assert.Equal(slot, decoded.AccessList[0].StorageKeys[0]);
            Assert.Single(decoded.AuthorisationList);
            Assert.Equal(original.Amount, decoded.Amount);
        }

        [Fact]
        public void HashTreeRoot_StableAcrossEncodeDecode()
        {
            var original = new Transaction7702(
                chainId: 1,
                nonce: 42,
                maxPriorityFeePerGas: 2000000000,
                maxFeePerGas: 30000000000,
                gasLimit: 100000,
                receiverAddress: "0xbeef000000000000000000000000000000000001",
                amount: 0,
                data: "0xa9059cbb",
                accessList: new List<AccessListItem>(),
                authorisationList: new List<Authorisation7702Signed>
                {
                    new Authorisation7702Signed(1,
                        "0xdead000000000000000000000000000000000001", 0,
                        new byte[32], new byte[32], new byte[] { 0x01 })
                });

            var rootBefore = SszTransactionEncoder.Current.HashTreeRootTransaction7702(original);
            var encoded = SszTransactionEncoder.Current.EncodeTransaction7702Payload(original);
            var decoded = SszTransactionEncoder.Current.DecodeTransaction7702Payload(encoded);
            var rootAfter = SszTransactionEncoder.Current.HashTreeRootTransaction7702(decoded);

            Assert.Equal(rootBefore, rootAfter);
        }

        [Fact]
        public void FullE2E_EncodeDecodeVerifyHash()
        {
            var auth = new Authorisation7702Signed(1,
                "0xdead000000000000000000000000000000000001", 0,
                "0xabcdef0000000000000000000000000000000000000000000000000000000001".HexToByteArray(),
                "0x1234560000000000000000000000000000000000000000000000000000000002".HexToByteArray(),
                new byte[] { 0x01 });

            var tx = new Transaction7702(1, 42, 2000000000, 30000000000, 100000,
                "0xbeef000000000000000000000000000000000001", 0, "0xa9059cbb",
                new List<AccessListItem>(), new List<Authorisation7702Signed> { auth });

            // 1. Hash
            var txRoot = SszTransactionEncoder.Current.HashTreeRootTransaction7702(tx);

            // 2. Encode payload
            var payloadEncoded = SszTransactionEncoder.Current.EncodeTransaction7702Payload(tx);

            // 3. Wrap
            var sigBytes = SszTransactionEncoder.PackSignatureBytes(
                new byte[32], new byte[32], new byte[] { 0x01 });
            var fullTx = SszTransactionEncoder.Current.EncodeTransaction(
                SszTransactionEncoder.SelectorRlpSetCode, payloadEncoded, sigBytes);
            Assert.Equal(SszTransactionEncoder.SelectorRlpSetCode, fullTx[0]);

            // 4. Decode and verify hash stability
            var decoded = SszTransactionEncoder.Current.DecodeTransaction7702Payload(payloadEncoded);
            var recomputed = SszTransactionEncoder.Current.HashTreeRootTransaction7702(decoded);
            Assert.Equal(txRoot, recomputed);
        }
    }
}
