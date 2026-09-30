using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AccountAbstraction.ERC7579.Modules.MultiGuardian;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.AccountAbstraction.UnitTests.Modules.MultiGuardian
{
    public class MultiGuardianSignTypedDataV4Tests
    {
        private const string EntryPointAddress = "0x0000000071727De22E5E9d8BAf0edAc6f37da032";
        private static readonly BigInteger ChainId = 31337;

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
        public async Task SendRequestAsync_BlobLength_IsExactlyThresholdTimes65()
        {
            var guardians = new[] { EthECKey.GenerateKey(), EthECKey.GenerateKey(), EthECKey.GenerateKey() };
            var signer = new MultiGuardianSignTypedDataV4(guardians, 2);

            var blob = (await signer.SendRequestAsync(BuildTypedDataJson(FixedUserOperation()))).HexToByteArray();

            Assert.Equal(2 * 65, blob.Length);
        }

        [Fact]
        public async Task SendRequestAsync_SlotsRecoverToGuardians_OverTheIndependentlyComputedDigest()
        {
            var userOperation = FixedUserOperation();
            var json = BuildTypedDataJson(userOperation);
            var digest = UserOperationBuilder.PackAndHashEIP712UserOperation(userOperation, EntryPointAddress, ChainId);

            var guardians = new[] { EthECKey.GenerateKey(), EthECKey.GenerateKey() };
            var threshold = 2;
            var signer = new MultiGuardianSignTypedDataV4(guardians, threshold);

            var blob = (await signer.SendRequestAsync(json)).HexToByteArray();
            Assert.Equal(threshold * 65, blob.Length);

            var expectedOrder = guardians
                .Select(g => g.GetPublicAddress())
                .OrderBy(a => a.HexToBigInteger(false))
                .ToArray();

            var messageSigner = new EthereumMessageSigner();
            for (var i = 0; i < threshold; i++)
            {
                var slot = blob.Skip(i * 65).Take(65).ToArray();
                var recovered = messageSigner.EcRecover(digest, slot.ToHex(true));
                Assert.Equal(expectedOrder[i], recovered, ignoreCase: true);
            }
        }

        [Fact]
        public async Task SendRequestAsync_NullOrEmptyJson_Throws()
        {
            var signer = new MultiGuardianSignTypedDataV4(new[] { EthECKey.GenerateKey() }, 1);

            await Assert.ThrowsAsync<ArgumentException>(() => signer.SendRequestAsync(null!));
            await Assert.ThrowsAsync<ArgumentException>(() => signer.SendRequestAsync(string.Empty));
        }

        [Fact]
        public void Constructor_NullGuardians_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new MultiGuardianSignTypedDataV4(null!, 1));
        }

        [Fact]
        public void Constructor_ThresholdZero_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => new MultiGuardianSignTypedDataV4(new[] { EthECKey.GenerateKey() }, 0));
        }

        [Fact]
        public void Constructor_ThresholdGreaterThanGuardianCount_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => new MultiGuardianSignTypedDataV4(new[] { EthECKey.GenerateKey() }, 2));
        }

        [Fact]
        public void BuildRequest_ThrowsNotImplemented()
        {
            var signer = new MultiGuardianSignTypedDataV4(new[] { EthECKey.GenerateKey() }, 1);
            Assert.Throws<NotImplementedException>(() => signer.BuildRequest("{}"));
        }
    }
}
