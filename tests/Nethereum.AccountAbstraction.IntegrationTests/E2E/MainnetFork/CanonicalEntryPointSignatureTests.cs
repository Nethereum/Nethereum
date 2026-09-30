using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AccountAbstraction.EntryPoint;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.XUnitEthereumClients;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.E2E.MainnetFork
{
    [CollectionDefinition(AnvilMainnetForkFixture.COLLECTION_NAME)]
    public class AnvilMainnetForkCollection : ICollectionFixture<AnvilMainnetForkFixture> { }

    [Collection(AnvilMainnetForkFixture.COLLECTION_NAME)]
    [Trait("Category", "MainnetFork")]
    [Trait("ERC", "4337")]
    public class CanonicalEntryPointSignatureTests
    {
        private readonly AnvilMainnetForkFixture _fixture;

        public CanonicalEntryPointSignatureTests(AnvilMainnetForkFixture fixture)
        {
            _fixture = fixture;
        }

        [SkippableTheory]
        [InlineData(EntryPointAddresses.V08, "v0.8")]
        [InlineData(EntryPointAddresses.V09, "v0.9")]
        public async Task CanonicalEntryPoint_IsDeployedOnFork(string entryPointAddress, string version)
        {
            Skip.If(!_fixture.IsAvailable, _fixture.UnavailableReason);

            var code = await _fixture.GetWeb3().Eth.GetCode.SendRequestAsync(entryPointAddress);
            Assert.True(!string.IsNullOrEmpty(code) && code.Length > 2,
                $"Canonical EntryPoint {version} has no code at {entryPointAddress} on the fork (block {_fixture.ForkBlockNumber})");
        }

        [SkippableTheory]
        [InlineData(EntryPointAddresses.V08, "v0.8")]
        [InlineData(EntryPointAddresses.V09, "v0.9")]
        public async Task SignedUserOpHash_MatchesCanonicalEntryPoint(string entryPointAddress, string version)
        {
            Skip.If(!_fixture.IsAvailable, _fixture.UnavailableReason);

            var web3 = _fixture.GetWeb3();
            var signerKey = EthECKey.GenerateKey();
            var entryPointService = new EntryPointService(web3.Eth, entryPointAddress);

            var variants = new (string name, UserOperation op)[]
            {
                ("plain", BaseUserOperation(signerKey)),
                ("initCode", WithInitCode(BaseUserOperation(signerKey))),
                ("factoryOnlyInitCode", WithFactoryOnlyInitCode(BaseUserOperation(signerKey))),
                ("paymaster", WithPaymaster(BaseUserOperation(signerKey))),
                ("paymasterEmptyData", WithPaymasterWithoutData(BaseUserOperation(signerKey)))
            };

            foreach (var (name, userOp) in variants)
            {
                var packedOp = UserOperationBuilder.PackAndSignEIP712UserOperation(
                    userOp, entryPointAddress, _fixture.ChainId, signerKey);

                var canonicalHash = await entryPointService.GetUserOpHashQueryAsync(packedOp);

                var signature = EthECDSASignatureFactory.ExtractECDSASignature(packedOp.Signature.ToHex(true));
                var recovered = EthECKey.RecoverFromSignature(signature, canonicalHash).GetPublicAddress();

                Assert.True(recovered.IsTheSameAddress(signerKey.GetPublicAddress()),
                    $"Our signed userOpHash does not match the canonical {version} EntryPoint hash " +
                    $"for the '{name}' variant (EntryPoint {entryPointAddress}, fork block {_fixture.ForkBlockNumber})");
            }
        }

        [SkippableTheory]
        [InlineData(EntryPointAddresses.V08, "v0.8")]
        [InlineData(EntryPointAddresses.V09, "v0.9")]
        public async Task HashUserOperation_MatchesCanonicalEntryPoint(string entryPointAddress, string version)
        {
            Skip.If(!_fixture.IsAvailable, _fixture.UnavailableReason);

            var web3 = _fixture.GetWeb3();
            var signerKey = EthECKey.GenerateKey();
            var entryPointService = new EntryPointService(web3.Eth, entryPointAddress);

            var packedOp = UserOperationBuilder.PackAndSignEIP712UserOperation(
                BaseUserOperation(signerKey), entryPointAddress, _fixture.ChainId, signerKey);

            var canonicalHash = await entryPointService.GetUserOpHashQueryAsync(packedOp);
            var ourHash = UserOperationBuilder.HashUserOperation(packedOp, entryPointAddress, _fixture.ChainId);

            Assert.Equal(canonicalHash.ToHex(), ourHash.ToHex());
        }

        private static UserOperation BaseUserOperation(EthECKey signerKey)
        {
            return new UserOperation
            {
                Sender = signerKey.GetPublicAddress(),
                Nonce = 1,
                CallData = new byte[] { 0xca, 0x11, 0xda, 0x7a },
                CallGasLimit = 100_000,
                VerificationGasLimit = 150_000,
                PreVerificationGas = 50_000,
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };
        }

        private static UserOperation WithInitCode(UserOperation op)
        {
            var factory = "0x00000000000000000000000000000000000000f1".HexToByteArray();
            var factoryData = new byte[] { 0x01, 0x02, 0x03 };
            op.InitCode = ByteUtil.Merge(factory, factoryData);
            return op;
        }

        private static UserOperation WithFactoryOnlyInitCode(UserOperation op)
        {
            op.InitCode = "0x00000000000000000000000000000000000000f1".HexToByteArray();
            return op;
        }

        private static UserOperation WithPaymaster(UserOperation op)
        {
            op.Paymaster = "0x00000000000000000000000000000000000000a1";
            op.PaymasterVerificationGasLimit = 60_000;
            op.PaymasterPostOpGasLimit = 20_000;
            op.PaymasterData = new byte[] { 0xaa, 0xbb };
            return op;
        }

        private static UserOperation WithPaymasterWithoutData(UserOperation op)
        {
            op.Paymaster = "0x00000000000000000000000000000000000000a1";
            op.PaymasterVerificationGasLimit = 60_000;
            op.PaymasterPostOpGasLimit = 20_000;
            return op;
        }
    }
}
