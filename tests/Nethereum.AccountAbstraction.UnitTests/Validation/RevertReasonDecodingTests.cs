using System.Numerics;
using System.Reflection;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.Model;
using Nethereum.AccountAbstraction.Bundler.Execution;
using Nethereum.AccountAbstraction.Bundler.Validation;
using Nethereum.AccountAbstraction.EntryPoint.ContractDefinition;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.AccountAbstraction.UnitTests.Validation
{
    public class RevertReasonDecodingTests
    {
        private const string ERROR_STRING_SELECTOR = "0x08c379a0";
        private const string DECODABLE_REASON = "this is a long revert reason string we are looking for";

        private static byte[] EncodeStandardErrorRevert(string message)
        {
            var encoder = new FunctionCallEncoder();
            var hex = encoder.EncodeRequest(
                ERROR_STRING_SELECTOR,
                new[] { new Parameter("string", "message", 1) },
                message);
            return hex.HexToByteArray();
        }

        private static string EncodeFailedOpWithRevert(BigInteger opIndex, string reason, byte[] inner)
        {
            var errorAbi = ABITypedRegistry.GetError<FailedOpWithRevertError>();
            var encoder = new FunctionCallEncoder();
            return encoder.EncodeRequest(errorAbi.Sha3Signature, errorAbi.InputParameters, opIndex, reason, inner);
        }

        private static string InvokeParseEntryPointError(SmartContractCustomErrorRevertException ex)
        {
            var method = typeof(UserOpValidator).GetMethod(
                "ParseEntryPointError", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);
            return (string)method!.Invoke(null, new object[] { ex })!;
        }

        private static Exception InvokeTranslateEntryPointRevert(SmartContractCustomErrorRevertException ex)
        {
            var method = typeof(BundleExecutor).GetMethod(
                "TranslateEntryPointRevert", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);
            return (Exception)method!.Invoke(null, new object[] { ex })!;
        }

        [Fact]
        public void ParseEntryPointError_FailedOpWithRevert_DecodesInnerReason_NotRawHex()
        {
            var innerBytes = EncodeStandardErrorRevert(DECODABLE_REASON);
            var encodedData = EncodeFailedOpWithRevert(0, "AA23 reverted", innerBytes);
            var ex = new SmartContractCustomErrorRevertException(encodedData);

            var message = InvokeParseEntryPointError(ex);

            Assert.Contains(DECODABLE_REASON, message);
            Assert.DoesNotContain(innerBytes.ToHex(true), message);
        }

        [Fact]
        public void TranslateEntryPointRevert_FailedOpWithRevert_DecodesInnerReason_NotRawHex()
        {
            var innerBytes = EncodeStandardErrorRevert(DECODABLE_REASON);
            var encodedData = EncodeFailedOpWithRevert(1, "AA33 reverted", innerBytes);
            var ex = new SmartContractCustomErrorRevertException(encodedData);

            var translated = InvokeTranslateEntryPointRevert(ex);

            var failedOp = Assert.IsType<BundleFailedOpException>(translated);
            Assert.Equal(1, failedOp.OpIndex);
            Assert.Contains(DECODABLE_REASON, failedOp.Message);
            Assert.DoesNotContain(innerBytes.ToHex(true), failedOp.Message);
        }

        [Fact]
        public void ParseEntryPointError_UndecodableInner_FallsBackToHex()
        {
            var innerBytes = new byte[] { 0xde, 0xad, 0xbe, 0xef };
            var encodedData = EncodeFailedOpWithRevert(0, "AA23 reverted", innerBytes);
            var ex = new SmartContractCustomErrorRevertException(encodedData);

            var message = InvokeParseEntryPointError(ex);

            Assert.Contains(innerBytes.ToHex(true), message);
        }
    }
}
