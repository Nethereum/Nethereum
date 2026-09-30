using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AccountAbstraction.ERC7579.Modules.SmartSession;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.AccountAbstraction.UnitTests.Modules.SmartSession
{
    public class SmartSessionKeySigningServiceTests
    {
        private const string EntryPointAddress = "0x0000000071727De22E5E9d8BAf0edAc6f37da032";
        private static readonly BigInteger ChainId = 31337;

        private static readonly EthECKey SessionKey =
            new EthECKey("0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7");

        private static readonly byte[] PermissionId =
            "0x0102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f20".HexToByteArray();

        private static UserOperation FixedUserOperation() => new UserOperation
        {
            Sender = "0x000000000000000000000000000000000000dEaD",
            Nonce = 0,
            CallData = "0xa9059cbb0000000000000000000000000000000000000000000000000000000000000dead0000000000000000000000000000000000000000000000000de0b6b3a7640000".HexToByteArray(),
            InitCode = Array.Empty<byte>(),
            CallGasLimit = 100000,
            VerificationGasLimit = 150000,
            PreVerificationGas = 21000,
            MaxFeePerGas = 2000000000,
            MaxPriorityFeePerGas = 1000000000
        };

        private static string BuildTypedDataJson(UserOperation userOperation)
        {
            var (packedOp, _) = UserOperationBuilder.PackAndHashEIP712UserOperationForSigning(
                userOperation, EntryPointAddress, ChainId);
            return UserOperationBuilder.BuildUserOperationTypedDataJson(packedOp, EntryPointAddress, ChainId);
        }

        [Fact]
        public async Task SendRequestAsync_EnvelopeIsModeThenPermissionIdThenRawSignature()
        {
            var json = BuildTypedDataJson(FixedUserOperation());
            var signer = new SmartSessionKeySignTypedDataV4(SessionKey, PermissionId);

            var envelope = (await signer.SendRequestAsync(json)).HexToByteArray();

            Assert.Equal(1 + 32 + 65, envelope.Length);
            Assert.Equal(0x00, envelope[0]);
            Assert.Equal(PermissionId, envelope[1..33]);
            Assert.Equal(65, envelope[33..98].Length);
        }

        [Fact]
        public async Task SendRequestAsync_RawSignature_RecoversToSessionKeyAddress()
        {
            var userOperation = FixedUserOperation();
            var json = BuildTypedDataJson(userOperation);
            var digest = UserOperationBuilder.PackAndHashEIP712UserOperation(userOperation, EntryPointAddress, ChainId);

            var signer = new SmartSessionKeySignTypedDataV4(SessionKey, PermissionId);
            var envelope = (await signer.SendRequestAsync(json)).HexToByteArray();
            var rawSignature = envelope[33..98];

            var recoveredAddress = new MessageSigner().EcRecover(digest, rawSignature.ToHex(prefix: true));

            Assert.Equal(SessionKey.GetPublicAddress(), recoveredAddress, ignoreCase: true);
        }

        [Theory]
        [InlineData(31)]
        [InlineData(33)]
        public void Constructor_WrongLengthPermissionId_Throws(int length)
        {
            var badPermissionId = new byte[length];

            var ex = Assert.Throws<ArgumentException>(
                () => new SmartSessionKeySignTypedDataV4(SessionKey, badPermissionId));

            Assert.Contains(length.ToString(), ex.Message);
        }

        [Fact]
        public void Constructor_NullPermissionId_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new SmartSessionKeySignTypedDataV4(SessionKey, null));
        }

        [Fact]
        public void Constructor_NullSessionKey_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new SmartSessionKeySignTypedDataV4(null, PermissionId));
        }

        [Fact]
        public void SigningService_NullSessionKey_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new SmartSessionKeySigningService(null, PermissionId));
        }

        [Theory]
        [InlineData(31)]
        [InlineData(33)]
        public void SigningService_WrongLengthPermissionId_Throws(int length)
        {
            var badPermissionId = new byte[length];

            Assert.Throws<ArgumentException>(() => new SmartSessionKeySigningService(SessionKey, badPermissionId));
        }

        [Fact]
        public async Task PersonalSign_SendRequestAsync_ThrowsNotSupported()
        {
            var signingService = new SmartSessionKeySigningService(SessionKey, PermissionId);

            await Assert.ThrowsAsync<NotSupportedException>(
                () => signingService.PersonalSign.SendRequestAsync(new byte[] { 0x01 }));
        }
    }
}
