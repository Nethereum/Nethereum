using System.Numerics;
using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.Bundler.GasEstimation;
using Nethereum.AccountAbstraction.IntegrationTests.E2E.Fixtures;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.Signer;

namespace Nethereum.AccountAbstraction.IntegrationTests.SecurityAudit71.S7.Round3
{
    public sealed class S7R3Support
    {
        private readonly DevChainBundlerFixture _fixture;

        public S7R3Support(DevChainBundlerFixture fixture)
        {
            _fixture = fixture;
        }

        public string EntryPoint => _fixture.EntryPointService.ContractAddress;

        public BundlerConfig NewConfig(int maxMempoolSize = 5000) => new()
        {
            SupportedEntryPoints = new[] { EntryPoint },
            BeneficiaryAddress = _fixture.BundlerAccount.Address,
            MaxBundleSize = 10,
            MaxBundleGas = 15_000_000,
            MaxMempoolSize = maxMempoolSize,
            MaxVerificationGas = 10_000_000,
            AutoBundleIntervalMs = 0,
            ReputationDecayIntervalMs = 0,
            StrictValidation = false,
            SimulateValidation = false,
            UnsafeMode = false,
            ChainId = DevChainBundlerFixture.CHAIN_ID
        };

        public async Task<(string Address, EthECKey Key)> AccountAsync()
        {
            return await _fixture.CreateFundedAccountAsync((ulong)Random.Shared.NextInt64(), 1m);
        }

        public async Task<PackedUserOperation> OpAsync(
            string sender,
            EthECKey key,
            BigInteger nonce,
            BigInteger priorityFee,
            BigInteger maxFee)
        {
            var op = new UserOperation
            {
                Sender = sender,
                Nonce = nonce,
                InitCode = Array.Empty<byte>(),
                CallData = Array.Empty<byte>(),
                CallGasLimit = 100_000,
                VerificationGasLimit = 500_000,
                PreVerificationGas = 0,
                MaxFeePerGas = maxFee,
                MaxPriorityFeePerGas = priorityFee
            };

            var sizing = UserOperationBuilder.PackUserOperation(op);
            sizing.Signature = new byte[65];
            var minPvg = Eip7623PreVerificationGasCalculator.CalculateMinRequired(
                sizing, Eip7623PreVerificationGasCalculator.MaxVerificationGasUsed);
            op.PreVerificationGas = minPvg + 20_000;
            op.Signature = new byte[65];

            return await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(op, key);
        }

        public async Task<(PackedUserOperation Op, string Hash)> FreshOpAsync(BigInteger priorityFee, BigInteger maxFee)
        {
            var (account, key) = await AccountAsync();
            var nonce = await _fixture.EntryPointService.GetNonceQueryAsync(account, BigInteger.Zero);
            var op = await OpAsync(account, key, nonce, priorityFee, maxFee);
            var hash = Nethereum.Hex.HexConvertors.Extensions.HexByteConvertorExtensions.ToHex(
                await _fixture.EntryPointService.GetUserOpHashQueryAsync(op), true);
            return (op, hash);
        }
    }
}
