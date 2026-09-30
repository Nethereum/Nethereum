using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AccountAbstraction.Signing;
using Nethereum.Accounts.AccountMessageSigning;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.AccountAbstraction.Tests
{
    public class AccountSigningServiceUserOpSignerTwinTests
    {
        private const string EntryPointAddress = "0x0000000071727De22E5E9d8BAf0edAc6f37da032";
        private static readonly BigInteger ChainId = 31337;
        private static readonly EthECKey Signer =
            new EthECKey("0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7");

        private static readonly IErc7579ValidatorModule Validator =
            new EcdsaValidatorModule("0x00000000000000000000000000000000000000aa");

        public static IEnumerable<object[]> FixedUserOperations()
            => ByteIdentitySignerOracleTests.FixedUserOperations();

        public static IEnumerable<object[]> FixedUserOperationsWithGoldenSignatures()
        {
            var operationsByCaseName = FixedUserOperations()
                .ToDictionary(row => (string)row[0], row => row[1]);

            foreach (var golden in ByteIdentitySignerOracleTests.GoldenSignatures())
            {
                var caseName = (string)golden[0];
                var expectedSignatureHex = (string)golden[1];
                yield return new object[] { caseName, operationsByCaseName[caseName], expectedSignatureHex };
            }
        }

        private static async Task<byte[]> SignViaOfflineProductionPathAsync(UserOperation userOperation, IErc7579ValidatorModule? validator)
        {
            var (packedOp, _) = UserOperationBuilder.PackAndHashEIP712UserOperationForSigning(userOperation, EntryPointAddress, ChainId);
            var json = UserOperationBuilder.BuildUserOperationTypedDataJson(packedOp, EntryPointAddress, ChainId);
            var sig = (await new AccountSigningOfflineService(Signer).SignTypedDataV4.SendRequestAsync(json)).HexToByteArray();
            return validator != null ? validator.ApplySignaturePrefix(sig) : sig;
        }

        [Theory]
        [MemberData(nameof(FixedUserOperationsWithGoldenSignatures))]
        public async Task NullValidator_OfflineProductionPath_MatchesFrozenGolden(
            string caseName, UserOperation userOperation, string expectedSignatureHex)
        {
            var signature = await SignViaOfflineProductionPathAsync(userOperation, validator: null);

            Assert.Equal(expectedSignatureHex, signature.ToHex(prefix: true), ignoreCase: true);
        }

        [Theory]
        [MemberData(nameof(FixedUserOperations))]
        public async Task WithValidator_OfflineProductionPath_PrependsValidatorAndKeepsSignatureBytesEqual(
            string caseName, UserOperation userOperation)
        {
            var nullValidatorSignature = await SignViaOfflineProductionPathAsync(userOperation, validator: null);
            var withValidatorSignature = await SignViaOfflineProductionPathAsync(userOperation, Validator);

            var validatorPrefix = withValidatorSignature[..20];
            var remainingSignature = withValidatorSignature[20..];

            Assert.Equal(Validator.Address.HexToByteArray().ToHex(prefix: true), validatorPrefix.ToHex(prefix: true));
            Assert.Equal(65, remainingSignature.Length);
            Assert.Equal(nullValidatorSignature.ToHex(prefix: true), remainingSignature.ToHex(prefix: true));
        }
    }
}
