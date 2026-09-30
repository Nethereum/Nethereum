using System.Numerics;
using System.Text;
using Nethereum.AccountAbstraction.Bundler.Mempool;
using Nethereum.AccountAbstraction.Bundler.RocksDB.Serialization;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RLP;
using Nethereum.RPC.Eth.DTOs;
using Xunit;
using BigInteger = System.Numerics.BigInteger;

namespace Nethereum.AccountAbstraction.Bundler.RocksDB.UnitTests
{
    public class MempoolEntrySerializerTests
    {
        private static MempoolEntry CreateEntry()
        {
            return new MempoolEntry
            {
                UserOpHash = "0x" + new string('a', 64),
                EntryPoint = "0x433709009B8330FDa32311DF1C2AFA402eD8D009",
                UserOperation = new PackedUserOperation
                {
                    Sender = "0x1111111111111111111111111111111111111111",
                    Nonce = BigInteger.One,
                    CallData = new byte[] { 0x01, 0x02, 0x03 },
                    Signature = new byte[] { 0xab, 0xcd }
                },
                Priority = BigInteger.One,
                State = MempoolEntryState.Pending,
                SubmittedAt = DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_000)
            };
        }

        private static Authorisation CreateAuth()
        {
            return new Authorisation
            {
                ChainId = new HexBigInteger(1),
                Address = "0x1234567890123456789012345678901234567890",
                Nonce = new HexBigInteger(7),
                YParity = "0x1",
                R = "0x" + new string('b', 64),
                S = "0x" + new string('c', 64)
            };
        }

        [Fact]
        public void RoundTrip_WithEip7702Auth_PreservesAllSixFields()
        {
            var entry = CreateEntry();
            entry.Eip7702Auth = CreateAuth();

            var bytes = MempoolEntrySerializer.Serialize(entry);
            var restored = MempoolEntrySerializer.Deserialize(bytes);

            Assert.NotNull(restored);
            Assert.NotNull(restored!.Eip7702Auth);
            Assert.Equal(entry.Eip7702Auth.ChainId.Value, restored.Eip7702Auth!.ChainId.Value);
            Assert.Equal(entry.Eip7702Auth.Address, restored.Eip7702Auth.Address);
            Assert.Equal(entry.Eip7702Auth.Nonce.Value, restored.Eip7702Auth.Nonce.Value);
            Assert.Equal(entry.Eip7702Auth.YParity, restored.Eip7702Auth.YParity);
            Assert.Equal(entry.Eip7702Auth.R, restored.Eip7702Auth.R);
            Assert.Equal(entry.Eip7702Auth.S, restored.Eip7702Auth.S);
        }

        [Fact]
        public void RoundTrip_WithZeroChainIdAndNonce_PreservesZeroSentinel()
        {
            // EIP-7702's chainId=0 is the "valid on any chain" sentinel, and nonce=0 is a fresh
            // EOA's first delegation. Both encode to empty RLP bytes, so a present auth must decode
            // them as the value 0 (not null) - the whole-auth empty element is the only "no auth".
            var entry = CreateEntry();
            entry.Eip7702Auth = new Authorisation
            {
                ChainId = new HexBigInteger(0),
                Address = "0x1234567890123456789012345678901234567890",
                Nonce = new HexBigInteger(0),
                YParity = "0x0",
                R = "0x" + new string('b', 64),
                S = "0x" + new string('c', 64)
            };

            var bytes = MempoolEntrySerializer.Serialize(entry);
            var restored = MempoolEntrySerializer.Deserialize(bytes);

            Assert.NotNull(restored);
            Assert.NotNull(restored!.Eip7702Auth);
            Assert.NotNull(restored.Eip7702Auth!.ChainId);
            Assert.Equal(BigInteger.Zero, restored.Eip7702Auth.ChainId.Value);
            Assert.NotNull(restored.Eip7702Auth.Nonce);
            Assert.Equal(BigInteger.Zero, restored.Eip7702Auth.Nonce.Value);
            Assert.Equal("0x0", restored.Eip7702Auth.YParity);
            Assert.Equal(entry.Eip7702Auth.Address, restored.Eip7702Auth.Address);
        }

        [Fact]
        public void RoundTrip_WithoutEip7702Auth_LeavesItNull()
        {
            var entry = CreateEntry();
            Assert.Null(entry.Eip7702Auth);

            var bytes = MempoolEntrySerializer.Serialize(entry);
            var restored = MempoolEntrySerializer.Deserialize(bytes);

            Assert.NotNull(restored);
            Assert.Null(restored!.Eip7702Auth);
            Assert.Equal(entry.UserOpHash, restored.UserOpHash);
            Assert.Equal(entry.EntryPoint, restored.EntryPoint);
            Assert.Equal(entry.UserOperation.Sender, restored.UserOperation.Sender);
        }

        [Fact]
        public void Deserialize_OldFormatRowWithoutAuthElement_LeavesEip7702AuthNull()
        {
            var entry = CreateEntry();

            // A payload written before the EIP-7702 element existed: exactly the 17-element
            // fixed list (indices 0-16, ending at BlockHash). The count guard must leave
            // Eip7702Auth null rather than throw on the missing trailing element.
            var oldFormatBytes = SerializeOldFormat(entry);

            var restored = MempoolEntrySerializer.Deserialize(oldFormatBytes);

            Assert.NotNull(restored);
            Assert.Null(restored!.Eip7702Auth);
            Assert.Equal(entry.UserOpHash, restored.UserOpHash);
            Assert.Equal(entry.EntryPoint, restored.EntryPoint);
            Assert.Equal(entry.State, restored.State);
            Assert.Equal(entry.Priority, restored.Priority);
            Assert.Equal(entry.UserOperation.Sender, restored.UserOperation.Sender);
            Assert.Equal(entry.UserOperation.Nonce, restored.UserOperation.Nonce);
        }

        // Mirrors the pre-EIP-7702 serializer output: the 17-element fixed list ending at
        // BlockHash, with no trailing authorisation element. Used to prove backward compatibility.
        private static byte[] SerializeOldFormat(MempoolEntry entry)
        {
            var userOpBytes = SerializeOldPackedUserOperation(entry.UserOperation);

            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(Encoding.UTF8.GetBytes(entry.UserOpHash ?? "")),
                RLP.RLP.EncodeElement(userOpBytes),
                RLP.RLP.EncodeElement(Encoding.UTF8.GetBytes(entry.EntryPoint ?? "")),
                RLP.RLP.EncodeElement(entry.SubmittedAt.ToUnixTimeMilliseconds().ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(((int)entry.State).ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(Encoding.UTF8.GetBytes(entry.TransactionHash ?? "")),
                RLP.RLP.EncodeElement(entry.BlockNumber?.ToBytesForRLPEncoding() ?? Array.Empty<byte>()),
                RLP.RLP.EncodeElement(Encoding.UTF8.GetBytes(entry.Error ?? "")),
                RLP.RLP.EncodeElement(entry.RetryCount.ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(entry.Prefund.ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(Encoding.UTF8.GetBytes(entry.Aggregator ?? "")),
                RLP.RLP.EncodeElement(entry.ValidUntil.HasValue ? ((BigInteger)entry.ValidUntil.Value).ToBytesForRLPEncoding() : Array.Empty<byte>()),
                RLP.RLP.EncodeElement(entry.ValidAfter.HasValue ? ((BigInteger)entry.ValidAfter.Value).ToBytesForRLPEncoding() : Array.Empty<byte>()),
                RLP.RLP.EncodeElement(Encoding.UTF8.GetBytes(entry.Factory ?? "")),
                RLP.RLP.EncodeElement(Encoding.UTF8.GetBytes(entry.Paymaster ?? "")),
                RLP.RLP.EncodeElement(entry.Priority.ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(Encoding.UTF8.GetBytes(entry.BlockHash ?? ""))
            );
        }

        private static byte[] SerializeOldPackedUserOperation(PackedUserOperation op)
        {
            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(op.Sender?.HexToByteArray() ?? Array.Empty<byte>()),
                RLP.RLP.EncodeElement(op.Nonce.ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(op.InitCode ?? Array.Empty<byte>()),
                RLP.RLP.EncodeElement(op.CallData ?? Array.Empty<byte>()),
                RLP.RLP.EncodeElement(op.AccountGasLimits ?? Array.Empty<byte>()),
                RLP.RLP.EncodeElement(op.PreVerificationGas.ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(op.GasFees ?? Array.Empty<byte>()),
                RLP.RLP.EncodeElement(op.PaymasterAndData ?? Array.Empty<byte>()),
                RLP.RLP.EncodeElement(op.Signature ?? Array.Empty<byte>())
            );
        }
    }
}
