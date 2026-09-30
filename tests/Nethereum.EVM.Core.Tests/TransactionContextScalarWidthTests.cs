using System.Numerics;
using Nethereum.EVM.Execution;
using Nethereum.EVM.Witness;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.Core.Tests
{
    /// <summary>
    /// The transaction-to-context mapping carries the transaction's own scalars
    /// at the width the wire gives them: RLP encodes nonce, gas price and gas
    /// limit as arbitrary-length big-endian quantities, and every carrier from
    /// the decoded transaction to <see cref="TransactionExecutionContext"/> is
    /// <see cref="EvmUInt256"/>.
    ///
    /// <para>EIP-2681: <i>"Limit account nonce to be between 0 and 2^64-1."</i>
    /// The upper half of that range does not survive a signed 64-bit carrier —
    /// it truncates to a negative <c>long</c> and sign-extends back to a value
    /// near 2^256, while a quantity above 2^64 loses its high limbs outright
    /// and can reach the context as zero. Neither shows up in a fixture that
    /// keeps its scalars small, so these tests are the evidence for this leg.</para>
    /// </summary>
    public class TransactionContextScalarWidthTests
    {
        private static readonly BigInteger TwoPow64 = BigInteger.One << 64;
        private static readonly BigInteger TwoPow128 = BigInteger.One << 128;

        private const string Recipient = "0x1111111111111111111111111111111111111111";

        private static TransactionExecutionContext MapFromRlp(
            BigInteger nonce, BigInteger gasPrice, BigInteger gasLimit)
        {
            var signedHex = new LegacyTransactionSigner().SignTransaction(
                TestTransactionHelper.DefaultPrivateKey,
                Recipient,
                BigInteger.Zero,
                nonce,
                gasPrice,
                gasLimit);

            var block = new BlockWitnessData
            {
                BlockNumber = 1,
                Timestamp = 1_700_000_000,
                BaseFee = 7,
                BlockGasLimit = 30_000_000,
                ChainId = 1,
                Coinbase = "0x0000000000000000000000000000000000000000"
            };

            return TransactionContextFactory.FromRlpEncoded(
                signedHex.HexToByteArray(),
                TestTransactionHelper.GetDefaultSenderAddress(),
                block,
                null);
        }

        [Fact]
        public void Given_ANonceAbove2Pow63_When_MappedFromRlp_Then_TheContextCarriesItUntruncated()
        {
            var nonce = TwoPow64 - 2;

            var ctx = MapFromRlp(nonce, 10, 100_000);

            Assert.Equal(nonce, ctx.Nonce.ToBigInteger());
        }

        [Fact]
        public void Given_AMaxFeePerGasOf2Pow128_When_MappedFromRlp_Then_TheContextDoesNotZeroIt()
        {
            var ctx = MapFromRlp(0, TwoPow128, 100_000);

            Assert.Equal(TwoPow128, ctx.GasPrice.ToBigInteger());
        }

        [Fact]
        public void Given_AGasLimitAtMaxUint64_When_MappedFromRlp_Then_TheContextCarriesItUntruncated()
        {
            var gasLimit = TwoPow64 - 1;

            var ctx = MapFromRlp(0, 10, gasLimit);

            Assert.Equal(gasLimit, ctx.GasLimit.ToBigInteger());
        }

        [Fact]
        public void Given_AnOrdinaryNonceAndGasPrice_When_MappedFromRlp_Then_TheContextIsUnchanged()
        {
            var ctx = MapFromRlp(42, 1_000_000_000, 21_000);

            Assert.Equal(new BigInteger(42), ctx.Nonce.ToBigInteger());
            Assert.Equal(new BigInteger(1_000_000_000), ctx.GasPrice.ToBigInteger());
            Assert.Equal(new BigInteger(21_000), ctx.GasLimit.ToBigInteger());
        }
    }
}
