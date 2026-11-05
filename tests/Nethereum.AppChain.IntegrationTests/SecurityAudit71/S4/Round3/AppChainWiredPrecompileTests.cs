using System.Numerics;
using System.Text.Json;
using Nethereum.AppChain.IntegrationTests.SecurityAudit71.S4.Round1;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.AppChain.IntegrationTests.SecurityAudit71.S4.Round3
{
    public class AppChainWiredPrecompileTests
    {
        private readonly ITestOutputHelper _out;

        public AppChainWiredPrecompileTests(ITestOutputHelper output) { _out = output; }

        [Theory]
        [InlineData("0x000000000000000000000000000000000000000a")]
        [InlineData("0x000000000000000000000000000000000000000f")]
        [Trait("Finding", "S4-R3-05")]
        public async Task Given_AnAppChainWithTheKzgAndBlsBackendsWired_When_ATransactionCallsThoseAddresses_Then_TheTransactionIsMinedAndNothingIsDropped(string precompile)
        {
            CopyHerumiNativeLibraryNextToTheTestAssembly();
            await using var host = await AppChainRpcHost.StartAsync(config =>
            {
                config.Consensus.AllowEmptyBlocks = true;
                config.Registry = Nethereum.AppChain.Server.AppChainPrecompileBackends.Build();
            });
            var key = EthECKey.GenerateKey();
            var chainId = JsonDocument.Parse(await host.CallAsync("eth_chainId", "[]")).RootElement.GetProperty("result").GetString()!.HexToBigInteger(false);
            var signed = new LegacyTransactionSigner().SignTransaction(
                key.GetPrivateKey().HexToByteArray(), chainId, precompile, BigInteger.Zero, BigInteger.Zero, 0, 500_000, "00");
            var send = await host.CallAsync("eth_sendRawTransaction", "[\"0x" + signed.EnsureHexPrefix().RemoveHexPrefix() + "\"]");
            using var sendDoc = JsonDocument.Parse(send);
            var hash = sendDoc.RootElement.GetProperty("result").GetString()!;

            string receipt = "";
            for (var i = 0; i < 30; i++)
            {
                await Task.Delay(1000);
                receipt = await host.CallAsync("eth_getTransactionReceipt", "[\"" + hash + "\"]");
                if (!receipt.Contains("\"result\":null")) break;
            }
            _out.WriteLine("receipt " + receipt);
            Assert.DoesNotContain("\"result\":null", receipt);
        }

        private static void CopyHerumiNativeLibraryNextToTheTestAssembly()
        {
            var runtime = OperatingSystem.IsWindows() ? "win-x64" : "linux-x64";
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                var native = Path.Combine(directory.FullName, "src", "Nethereum.Signer.Bls.Herumi", "runtimes", runtime, "native");
                if (Directory.Exists(native))
                {
                    foreach (var file in Directory.GetFiles(native))
                    {
                        var target = Path.Combine(AppContext.BaseDirectory, Path.GetFileName(file));
                        if (!File.Exists(target)) File.Copy(file, target);
                    }
                    return;
                }
                directory = directory.Parent;
            }
        }
    }
}
