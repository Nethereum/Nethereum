using System.Numerics;
using Nethereum.AccountAbstraction.IntegrationTests.E2E.Fixtures;
using Nethereum.AccountAbstraction.IntegrationTests.SecurityAudit71.S7.Round3;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.SecurityAudit71.S7.Round7
{
    [Collection(DevChainBundlerFixture.COLLECTION_NAME)]
    [Trait("Category", "DevChainBundler")]
    public class S7R7HashDifferentialTests
    {
        private const string PaymasterSignatureMagic = "22e325a297439656";

        private readonly DevChainBundlerFixture _fixture;
        private readonly S7R3Support _support;

        public S7R7HashDifferentialTests(DevChainBundlerFixture fixture)
        {
            _fixture = fixture;
            _support = new S7R3Support(fixture);
        }

        private async Task<UserOperation> OpAsync(string? paymaster, byte[]? paymasterData)
        {
            var (account, _) = await _support.AccountAsync();
            return new UserOperation
            {
                Sender = account,
                Nonce = 0,
                InitCode = Array.Empty<byte>(),
                CallData = Array.Empty<byte>(),
                CallGasLimit = 100_000,
                VerificationGasLimit = 300_000,
                PreVerificationGas = 60_000,
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000,
                Paymaster = paymaster,
                PaymasterData = paymasterData,
                PaymasterVerificationGasLimit = paymaster == null ? null : 100_000,
                PaymasterPostOpGasLimit = paymaster == null ? null : 100_000,
                Signature = new byte[65]
            };
        }

        private async Task AssertSameHashAsync(UserOperation userOp)
        {
            var libraryHash = UserOperationBuilder.PackAndHashEIP712UserOperation(
                userOp, _fixture.EntryPointService.ContractAddress, DevChainBundlerFixture.CHAIN_ID);
            var entryPointHash = await _fixture.EntryPointService.GetUserOpHashQueryAsync(
                UserOperationBuilder.PackUserOperation(userOp));

            Assert.Equal(entryPointHash.ToHex(true), libraryHash.ToHex(true));
        }

        [Fact]
        [Trait("Rule", "S7-R7-04")]
        [Trait("Erc", "UserOperationLib.paymasterDataKeccak")]
        public async Task PackAndHashEIP712UserOperation_WhenThereIsNoPaymaster_EqualsTheEntryPointUserOpHash()
        {
            await AssertSameHashAsync(await OpAsync(null, null));
        }

        [Fact]
        [Trait("Rule", "S7-R7-04")]
        [Trait("Erc", "UserOperationLib.paymasterDataKeccak")]
        public async Task PackAndHashEIP712UserOperation_WhenThePaymasterDataCarriesNoSignature_EqualsTheEntryPointUserOpHash()
        {
            await AssertSameHashAsync(await OpAsync("0x2222222222222222222222222222222222222222", new byte[] { 1, 2, 3, 4, 5 }));
        }

        [Fact]
        [Trait("Rule", "S7-R7-04")]
        [Trait("Erc", "UserOperationLib.paymasterDataKeccak")]
        public async Task PackAndHashEIP712UserOperation_WhenThePaymasterDataEndsWithTheMagicAndAZeroLength_EqualsTheEntryPointUserOpHash()
        {
            var data = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }
                .Concat(new byte[] { 0x00, 0x00 })
                .Concat(PaymasterSignatureMagic.HexToByteArray())
                .ToArray();

            await AssertSameHashAsync(await OpAsync("0x2222222222222222222222222222222222222222", data));
        }

        [Fact]
        [Trait("Rule", "S7-R7-04")]
        [Trait("Erc", "UserOperationLib.getPaymasterSignatureLength")]
        public async Task PackAndHashEIP712UserOperation_WhenTheDeclaredSignatureLengthReachesIntoThePaymasterHeader_IsRefusedLikeTheEntryPoint()
        {
            var data = new byte[] { 1, 2 }
                .Concat(new byte[] { 0xff, 0xff })
                .Concat(PaymasterSignatureMagic.HexToByteArray())
                .ToArray();
            var userOp = await OpAsync("0x2222222222222222222222222222222222222222", data);

            Assert.ThrowsAny<ArgumentException>(() => UserOperationBuilder.PackAndHashEIP712UserOperation(
                userOp, _fixture.EntryPointService.ContractAddress, DevChainBundlerFixture.CHAIN_ID));
            await Assert.ThrowsAnyAsync<Exception>(() => _fixture.EntryPointService.GetUserOpHashQueryAsync(
                UserOperationBuilder.PackUserOperation(userOp)));
        }
    }
}
