using System.Numerics;
using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.Bundler.GasEstimation;
using Nethereum.AccountAbstraction.EntryPoint.ContractDefinition;
using Nethereum.AccountAbstraction.IntegrationTests.E2E.Fixtures;
using Nethereum.AccountAbstraction.IntegrationTests.SecurityAudit71.S7.Round3;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.AccountAbstraction.IntegrationTests.SecurityAudit71.S7.Round7
{
    public sealed class S7R7PaymasterSupport
    {
        private readonly DevChainBundlerFixture _fixture;
        private readonly S7R3Support _support;

        public S7R7PaymasterSupport(DevChainBundlerFixture fixture)
        {
            _fixture = fixture;
            _support = new S7R3Support(fixture);
        }

        public string EntryPoint => _fixture.EntryPointService.ContractAddress;

        public BundlerConfig SimulatingConfig()
        {
            var config = _support.NewConfig();
            config.SimulateValidation = true;
            config.EnableERC7562Validation = false;
            return config;
        }

        public async Task<string> DeployPaymasterReturningAsync(BigInteger validationData)
        {
            var word = ToWord(validationData);
            var runtime = new List<byte> { 0x60, 0x40, 0x60, 0x00, 0x52, 0x7f };
            runtime.AddRange(word);
            runtime.AddRange(new byte[] { 0x60, 0x20, 0x52, 0x60, 0x60, 0x60, 0x00, 0xf3 });

            var init = new List<byte> { 0x60, (byte)runtime.Count, 0x80, 0x60, 0x0b, 0x60, 0x00, 0x39, 0x60, 0x00, 0xf3 };
            init.AddRange(runtime);

            var receipt = await _fixture.Web3.Eth.TransactionManager.SendTransactionAndWaitForReceiptAsync(
                new TransactionInput
                {
                    From = _fixture.OperatorAccount.Address,
                    Data = init.ToArray().ToHex(true),
                    Gas = new HexBigInteger(300_000)
                });

            var paymaster = receipt.ContractAddress;
            await _fixture.EntryPointService.DepositToRequestAndWaitForReceiptAsync(
                new DepositToFunction
                {
                    Account = paymaster,
                    AmountToSend = Nethereum.Web3.Web3.Convert.ToWei(1),
                    FromAddress = _fixture.OperatorAccount.Address
                });
            return paymaster;
        }

        public async Task<(PackedUserOperation Op, string Sender)> PaymasterOpAsync(string paymaster)
        {
            var (account, key) = await _support.AccountAsync();
            var nonce = await _fixture.EntryPointService.GetNonceQueryAsync(account, BigInteger.Zero);
            var op = new UserOperation
            {
                Sender = account,
                Nonce = nonce,
                InitCode = Array.Empty<byte>(),
                CallData = Array.Empty<byte>(),
                CallGasLimit = 100_000,
                VerificationGasLimit = 300_000,
                PreVerificationGas = 0,
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000,
                Paymaster = paymaster,
                PaymasterData = Array.Empty<byte>(),
                PaymasterVerificationGasLimit = 100_000,
                PaymasterPostOpGasLimit = 100_000
            };

            var sizing = UserOperationBuilder.PackUserOperation(op);
            sizing.Signature = new byte[65];
            op.PreVerificationGas = Eip7623PreVerificationGasCalculator.CalculateMinRequired(
                sizing, Eip7623PreVerificationGasCalculator.MaxVerificationGasUsed) + 20_000;
            op.Signature = new byte[65];

            var signed = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(op, key);
            return (signed, account);
        }

        private static byte[] ToWord(BigInteger value)
        {
            var bytes = value.ToByteArray(isUnsigned: true, isBigEndian: true);
            var word = new byte[32];
            Buffer.BlockCopy(bytes, 0, word, 32 - bytes.Length, bytes.Length);
            return word;
        }
    }
}
