using System.Diagnostics;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.AppChain.IntegrationTests.SecurityAudit71.S4.Round3
{
    public class LogFilterWidthTests
    {
        private const string Sender = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
        private const string SenderKey = "ac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private const string Emitter = "0x2000000000000000000000000000000000000002";

        private readonly ITestOutputHelper _out;

        public LogFilterWidthTests(ITestOutputHelper output) { _out = output; }

        private static string EmitterRuntime(int logs)
        {
            var counter = logs.ToString("x4");
            return "0x61" + counter + "5b" + "80" + "15" + "6015" + "57" + "6007" + "6000" + "6000" + "a1" + "6001" + "90" + "03" + "6003" + "56" + "5b" + "00";
        }

        private static string Hex40(int i) => "\"0x" + (0x3000000000000000UL + (ulong)i).ToString("x16").PadLeft(40, '0') + "\"";

        private static string Hex64(int i) => "\"0x" + (0x4000000000000000UL + (ulong)i).ToString("x16").PadLeft(64, '0') + "\"";

        private static string List(int count, Func<int, string> item)
        {
            var sb = new StringBuilder("[");
            for (var i = 0; i < count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(item(i));
            }
            return sb.Append(']').ToString();
        }

        [Theory]
        [InlineData("address", "sqlite", 30_000, 0)]
        [InlineData("address", "rocksdb", 100_000, 2000)]
        [InlineData("topic", "rocksdb", 100_000, 2000)]
        [Trait("Finding", "S4-R3-04")]
        public async Task Given_AFilterNamingTensOfThousandsOfAddressesOrTopics_When_EthGetLogsScansAChain_Then_ItIsRefusedLikeGethDoes(
            string kind, string storage, int width, int mined)
        {
            await using var host = await DevChainFuzzHost.StartAsync(verbose: false, storage: storage);
            await host.CallAsync("hardhat_setCode", "[\"" + Emitter + "\",\"" + EmitterRuntime(10000) + "\"]");
            var nonceBody = await host.CallAsync("eth_getTransactionCount", "[\"" + Sender + "\",\"latest\"]");
            using var nonceDoc = JsonDocument.Parse(nonceBody);
            var nonce = nonceDoc.RootElement.GetProperty("result").GetString()!.HexToBigInteger(false);
            var signed = new LegacyTransactionSigner().SignTransaction(
                SenderKey.HexToByteArray(), new BigInteger(31337), Emitter, BigInteger.Zero, nonce, 10_000_000_000, 12_000_000, "");
            var send = await host.CallAsync("eth_sendRawTransaction", "[\"0x" + signed.EnsureHexPrefix().RemoveHexPrefix() + "\"]");
            using var sendDoc = JsonDocument.Parse(send);
            Assert.True(sendDoc.RootElement.TryGetProperty("result", out _), send);

            for (var m = 0; m < mined; m++) await host.CallAsync("evm_mine", "[]");

            var criteria = kind == "address"
                ? "\"address\":" + List(width, Hex40)
                : "\"topics\":[" + List(width, Hex64) + "]";
            var sw = Stopwatch.StartNew();
            var body = await host.CallAsync("eth_getLogs", "[{\"fromBlock\":\"0x0\",\"toBlock\":\"latest\"," + criteria + "}]");
            sw.Stop();
            _out.WriteLine(kind + " x " + width + " on " + storage + " over " + (mined + 1) + " blocks: " + sw.ElapsedMilliseconds + " ms, answer " + (body.Length > 200 ? body.Substring(0, 200) : body));

            Assert.Contains("\"error\"", body);
        }
    }
}
