using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.DevChain;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Node.HarnessServer;
using Xunit;

namespace Nethereum.CoreChain.IntegrationTests.HttpRpc
{
    [Collection("EngineApi")]
    public class HarnessGenesisHashParityTests
    {
        private const string GethReferenceHashForGenesisOne =
            "0xc20b76a275dd52bcece82a5dc97cb60a6eb67e43468a609d1193741cd8b8c478";

        private const string GethReferenceHashForGenesisTwo =
            "0xc1819cfb2333f2f4ca3e11c6c56b66ea2a7ab43fd09b8e47f3fe6744198a80b0";

        private const string BeaconRootsRuntimeCode =
            "0x3373fffffffffffffffffffffffffffffffffffffffe14604d57602036146024575f5ffd5b5f3580" +
            "1560495762001fff810690815414603c575f5ffd5b62001fff01545f5260205ff35b5f5ffd5b62001f" +
            "ff42064281555f359062001fff015500";

        private static readonly string GenesisOneJson = $$"""
        {
            "config": {
                "chainId": 7777,
                "homesteadBlock": 0,
                "daoForkBlock": 0,
                "eip150Block": 0,
                "eip155Block": 0,
                "eip158Block": 0,
                "byzantiumBlock": 0,
                "constantinopleBlock": 0,
                "petersburgBlock": 0,
                "istanbulBlock": 0,
                "muirGlacierBlock": 0,
                "berlinBlock": 0,
                "londonBlock": 0,
                "arrowGlacierBlock": 0,
                "grayGlacierBlock": 0,
                "mergeNetsplitBlock": 0,
                "terminalTotalDifficulty": 0,
                "terminalTotalDifficultyPassed": true,
                "shanghaiTime": 0,
                "cancunTime": 0,
                "blobSchedule": {
                    "cancun": { "target": 3, "max": 6, "baseFeeUpdateFraction": 3338477 }
                }
            },
            "difficulty": "0x0",
            "nonce": "0x0000000000000042",
            "mixHash": "0x2020202020202020202020202020202020202020202020202020202020202026",
            "extraData": "0x1234",
            "gasLimit": "0x1c9c380",
            "baseFeePerGas": "0x3b9aca00",
            "timestamp": "0x64000000",
            "coinbase": "0x0000000000000000000000000000000000000000",
            "alloc": {
                "0xf39fd6e51aad88f6f4ce6ab8827279cfffb92266": { "balance": "0x21e19e0c9bab2400000" },
                "0x000f3df6d732807ef1319fb7b8bb8522d0beac02": {
                    "balance": "0x0",
                    "nonce": "0x1",
                    "code": "{{BeaconRootsRuntimeCode}}"
                }
            }
        }
        """;

        private static readonly string GenesisTwoJson = $$"""
        {
            "config": {
                "chainId": 7777,
                "homesteadBlock": 0,
                "daoForkBlock": 0,
                "eip150Block": 0,
                "eip155Block": 0,
                "eip158Block": 0,
                "byzantiumBlock": 0,
                "constantinopleBlock": 0,
                "petersburgBlock": 0,
                "istanbulBlock": 0,
                "muirGlacierBlock": 0,
                "berlinBlock": 0,
                "londonBlock": 0,
                "arrowGlacierBlock": 0,
                "grayGlacierBlock": 0,
                "mergeNetsplitBlock": 0,
                "terminalTotalDifficulty": 0,
                "terminalTotalDifficultyPassed": true,
                "shanghaiTime": 0,
                "cancunTime": 0,
                "blobSchedule": {
                    "cancun": { "target": 3, "max": 6, "baseFeeUpdateFraction": 3338477 }
                }
            },
            "difficulty": "0x0",
            "nonce": "0x0000000000000042",
            "mixHash": "0x2020202020202020202020202020202020202020202020202020202020202026",
            "extraData": "0xdeadbeef",
            "gasLimit": "0x2fefd800",
            "baseFeePerGas": "0x3b9aca00",
            "timestamp": "0x64000000",
            "coinbase": "0x0000000000000000000000000000000000000000",
            "alloc": {
                "0xf39fd6e51aad88f6f4ce6ab8827279cfffb92266": { "balance": "0x21e19e0c9bab2400000" },
                "0x000f3df6d732807ef1319fb7b8bb8522d0beac02": {
                    "balance": "0x0",
                    "nonce": "0x1",
                    "code": "{{BeaconRootsRuntimeCode}}"
                }
            }
        }
        """;

        private static async Task<string> BuildGenesisHashAsync(string genesisJson, int httpPort, int enginePort)
        {
            var dataDir = Path.Combine(Path.GetTempPath(), $"harness-genesis-parity-{Guid.NewGuid():N}");
            Directory.CreateDirectory(dataDir);
            var genesisPath = Path.Combine(dataDir, "genesis.json");
            File.WriteAllText(genesisPath, genesisJson);

            WebApplication? app = null;
            try
            {
                var options = new HarnessServerOptions
                {
                    DataDir = dataDir,
                    HttpAddr = "127.0.0.1",
                    HttpPort = httpPort,
                    AuthRpcAddr = "127.0.0.1",
                    AuthRpcPort = enginePort,
                    JwtSecretPath = Path.Combine(dataDir, "jwt.hex"),
                    GenesisPath = genesisPath,
                };

                app = await HarnessNodeHost.BuildAsync(options, Array.Empty<string>());

                var node = app.Services.GetRequiredService<DevChainNode>();
                var genesisHash = await node.GetBlockHashByNumberAsync(0);
                return genesisHash.ToHex(true);
            }
            finally
            {
                if (app != null) await app.DisposeAsync();
                try { Directory.Delete(dataDir, recursive: true); } catch (IOException) { }
            }
        }

        [Fact]
        public async Task Given_AGethValidGenesisJson_When_TheHarnessNodeBuildsItsGenesis_Then_ItsHashMatchesGethsGenesisHash()
        {
            var hash = await BuildGenesisHashAsync(GenesisOneJson, httpPort: 18590, enginePort: 18591);

            Assert.Equal(GethReferenceHashForGenesisOne, hash);
        }

        [Fact]
        public async Task Given_ADifferentExtraDataAndGasLimit_When_TheHarnessNodeBuildsItsGenesis_Then_ItsHashTracksThoseFieldsLikeGeth()
        {
            var hashOne = await BuildGenesisHashAsync(GenesisOneJson, httpPort: 18592, enginePort: 18593);
            var hashTwo = await BuildGenesisHashAsync(GenesisTwoJson, httpPort: 18594, enginePort: 18595);

            Assert.Equal(GethReferenceHashForGenesisTwo, hashTwo);
            Assert.NotEqual(hashOne, hashTwo);
        }
    }
}
