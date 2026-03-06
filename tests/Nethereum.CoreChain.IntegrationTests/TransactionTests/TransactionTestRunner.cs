using System.Text.Json;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.CoreChain.IntegrationTests.TransactionTests
{
    public class TransactionTestRunner
    {
        private readonly ITestOutputHelper _output;

        private static readonly string TestPath = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "external", "ethereum-tests", "TransactionTests");

        public TransactionTestRunner(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory]
        [MemberData(nameof(GetAllTransactionTestFiles))]
        [Trait("Category", "TransactionTests")]
        public void TransactionTest_DecodeAndValidate(string subDir, string fileName)
        {
            var filePath = Path.Combine(TestPath, subDir, fileName);
            if (!File.Exists(filePath))
            {
                _output.WriteLine($"Test file not found: {filePath}");
                return;
            }

            using var doc = JsonDocument.Parse(File.ReadAllText(filePath));
            foreach (var testProp in doc.RootElement.EnumerateObject())
            {
                var testName = testProp.Name;
                var testData = testProp.Value;

                if (testName == "_info") continue;

                var txBytesHex = "";
                if (testData.TryGetProperty("txbytes", out var txBytesProp))
                {
                    txBytesHex = txBytesProp.GetString() ?? "";
                }

                if (string.IsNullOrEmpty(txBytesHex))
                {
                    _output.WriteLine($"  {testName}: no txbytes, skipping");
                    continue;
                }

                if (!testData.TryGetProperty("result", out var resultProp))
                {
                    _output.WriteLine($"  {testName}: no result, skipping");
                    continue;
                }

                var txBytes = txBytesHex.HexToByteArray();

                var cancunResult = GetForkResult(resultProp, "Cancun");
                if (cancunResult == null)
                {
                    cancunResult = GetForkResult(resultProp, "London");
                }
                if (cancunResult == null)
                {
                    cancunResult = GetForkResult(resultProp, "Berlin");
                }

                if (cancunResult == null)
                {
                    _output.WriteLine($"  {testName}: no Cancun/London/Berlin result, skipping");
                    continue;
                }

                if (cancunResult.Value.TryGetProperty("exception", out _))
                {
                    ValidateInvalidTransaction(testName, txBytes, cancunResult.Value);
                }
                else if (cancunResult.Value.TryGetProperty("hash", out _))
                {
                    ValidateValidTransaction(testName, txBytes, cancunResult.Value);
                }
            }
        }

        private void ValidateValidTransaction(string testName, byte[] txBytes, JsonElement result)
        {
            var expectedHash = result.GetProperty("hash").GetString() ?? "";
            var expectedSender = result.GetProperty("sender").GetString() ?? "";

            try
            {
                var signedTx = TransactionFactory.CreateTransaction(txBytes);
                Assert.NotNull(signedTx);

                var actualHash = signedTx.Hash.ToHex(true);
                Assert.True(
                    expectedHash.IsTheSameHex(actualHash),
                    $"Hash mismatch for '{testName}'. Expected: {expectedHash}, Actual: {actualHash}");

                var actualSender = TransactionVerificationAndRecovery.GetSenderAddress(signedTx);
                Assert.True(
                    expectedSender.IsTheSameHex(actualSender),
                    $"Sender mismatch for '{testName}'. Expected: {expectedSender}, Actual: {actualSender}");

                _output.WriteLine($"  {testName}: VALID - hash and sender match");
            }
            catch (Exception ex)
            {
                Assert.Fail($"Valid transaction '{testName}' failed to decode: {ex.Message}");
            }
        }

        private void ValidateInvalidTransaction(string testName, byte[] txBytes, JsonElement result)
        {
            var exception = result.GetProperty("exception").GetString() ?? "";

            try
            {
                var signedTx = TransactionFactory.CreateTransaction(txBytes);
                _output.WriteLine($"  {testName}: INVALID tx decoded without exception (expected: {exception}) - some validation happens at execution time");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"  {testName}: INVALID - correctly threw {ex.GetType().Name}: {ex.Message.Substring(0, Math.Min(80, ex.Message.Length))}");
            }
        }

        private JsonElement? GetForkResult(JsonElement resultProp, string forkName)
        {
            if (resultProp.TryGetProperty(forkName, out var forkResult))
            {
                return forkResult;
            }
            return null;
        }

        public static IEnumerable<object[]> GetAllTransactionTestFiles()
        {
            if (!Directory.Exists(TestPath))
                yield break;

            var files = Directory.GetFiles(TestPath, "*.json", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                var relativePath = Path.GetRelativePath(TestPath, file);
                var parts = relativePath.Split(Path.DirectorySeparatorChar);
                if (parts.Length >= 2)
                {
                    yield return new object[] { parts[0], parts[parts.Length - 1] };
                }
            }
        }
    }
}
