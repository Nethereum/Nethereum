using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.UnitTests
{
    public class TransactionSignatureValidationTests
    {
        private const string SenderAddress = "0x1111111111111111111111111111111111111111";
        private const string CoinbaseAddress = "0x2222222222222222222222222222222222222222";
        private const string RecipientAddress = "0x3333333333333333333333333333333333333333";

        private const string HalfNPlusOne = "7fffffffffffffffffffffffffffffff5d576e7357a4501ddfe92f46681b20a1";
        private const string LowS = "01";
        private const string ValidR = "01";

        private static string Normalize(string address) =>
            AddressUtil.Current.ConvertToValid20ByteAddress(address).ToLower();

        private static ExecutionStateService State()
        {
            var accounts = new Dictionary<string, AccountState>
            {
                [Normalize(SenderAddress)] = new AccountState { Balance = new EvmUInt256(BigInteger.Parse("1000000000000000000000")) },
                [Normalize(CoinbaseAddress)] = new AccountState { Balance = new EvmUInt256(1) }
            };
            return new ExecutionStateService(new InMemoryStateReader(accounts));
        }

        private static TransactionExecutionContext Transfer(TransactionType type, string v, string s) =>
            new TransactionExecutionContext
            {
                Sender = SenderAddress,
                To = RecipientAddress,
                Value = 1,
                GasLimit = 21000,
                GasPrice = 0,
                MaxFeePerGas = 0,
                MaxPriorityFeePerGas = 0,
                IsEip1559 = type == TransactionType.EIP1559,
                TransactionType = type,
                Nonce = 0,
                BlockNumber = 1,
                Timestamp = 1704067200,
                BaseFee = 0,
                ChainId = 1,
                Coinbase = CoinbaseAddress,
                ExecutionState = State(),
                Signature = new Signature { R = ValidR.HexToByteArray(), S = s.HexToByteArray(), V = v.HexToByteArray() }
            };

        [Fact]
        public async Task Given_AHighSSignature_When_ExecutedAtHomestead_Then_ItIsRejectedAsAnInvalidSignature()
        {
            var result = await new TransactionExecutor(HardforkConfig.Homestead)
                .ExecuteAsync(Transfer(TransactionType.LegacyTransaction, "1b", HalfNPlusOne));

            Assert.True(result.IsValidationError);
            Assert.Equal(TransactionError.InvalidSignature, result.ErrorCode);
        }

        [Fact]
        public async Task Given_AHighSSignature_When_ExecutedAtFrontier_Then_ItIsAccepted()
        {
            var result = await new TransactionExecutor(HardforkConfig.Frontier)
                .ExecuteAsync(Transfer(TransactionType.LegacyTransaction, "1b", HalfNPlusOne));

            Assert.False(result.IsValidationError, result.Error);
        }

        [Fact]
        public async Task Given_AnEip155VForChainOne_When_ExecutedAtSpuriousDragon_Then_ItIsAccepted()
        {
            var result = await new TransactionExecutor(HardforkConfig.SpuriousDragon)
                .ExecuteAsync(Transfer(TransactionType.LegacyTransaction, "25", LowS));

            Assert.False(result.IsValidationError, result.Error);
        }

        [Fact]
        public async Task Given_AnEip155V_When_ExecutedAtHomestead_Then_ItIsRejectedAsAnInvalidSignature()
        {
            var result = await new TransactionExecutor(HardforkConfig.Homestead)
                .ExecuteAsync(Transfer(TransactionType.LegacyTransaction, "25", LowS));

            Assert.True(result.IsValidationError);
            Assert.Equal(TransactionError.InvalidSignature, result.ErrorCode);
        }

        [Fact]
        public async Task Given_ATypedTransactionWithYParityTwo_When_Executed_Then_ItIsRejectedAsAnInvalidSignature()
        {
            var result = await new TransactionExecutor(HardforkConfig.London)
                .ExecuteAsync(Transfer(TransactionType.EIP1559, "02", LowS));

            Assert.True(result.IsValidationError);
            Assert.Equal(TransactionError.InvalidSignature, result.ErrorCode);
        }

        [Fact]
        public async Task Given_AZeroR_When_Executed_Then_ItIsRejectedAsAnInvalidSignature()
        {
            var ctx = Transfer(TransactionType.EIP1559, "01", LowS);
            ctx.Signature = new Signature { R = new byte[0], S = LowS.HexToByteArray(), V = "01".HexToByteArray() };

            var result = await new TransactionExecutor(HardforkConfig.London).ExecuteAsync(ctx);

            Assert.True(result.IsValidationError);
            Assert.Equal(TransactionError.InvalidSignature, result.ErrorCode);
        }
    }
}
