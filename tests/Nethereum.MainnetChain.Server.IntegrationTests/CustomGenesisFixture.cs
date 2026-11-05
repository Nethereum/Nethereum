using Nethereum.CoreChain.Genesis;

namespace Nethereum.MainnetChain.Server.IntegrationTests
{
    internal static class CustomGenesisFixture
    {
        public const string FundedPrivateKey = "0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7";
        public const string FundedAddress = "0x12890D2cce102216644c59daE5baed380d84830c";
        public const long ChainId = 32382;

        public static string Json(string osakaTime = "0") => $@"{{
  ""config"": {{
    ""chainId"": {ChainId},
    ""homesteadBlock"": 0, ""eip150Block"": 0, ""eip155Block"": 0, ""eip158Block"": 0,
    ""byzantiumBlock"": 0, ""constantinopleBlock"": 0, ""petersburgBlock"": 0, ""istanbulBlock"": 0,
    ""berlinBlock"": 0, ""londonBlock"": 0, ""mergeNetsplitBlock"": 0,
    ""terminalTotalDifficulty"": 0,
    ""shanghaiTime"": 0, ""cancunTime"": 0, ""pragueTime"": 0, ""osakaTime"": {osakaTime},
    ""depositContractAddress"": ""0x00000000219ab540356cbb839cbe05303d7705fa"",
    ""blobSchedule"": {{
      ""cancun"": {{ ""target"": 3, ""max"": 6, ""baseFeeUpdateFraction"": 3338477 }},
      ""prague"": {{ ""target"": 6, ""max"": 9, ""baseFeeUpdateFraction"": 5007716 }},
      ""osaka"": {{ ""target"": 6, ""max"": 9, ""baseFeeUpdateFraction"": 5007716 }}
    }}
  }},
  ""nonce"": ""0x0"",
  ""timestamp"": ""0x0"",
  ""extraData"": ""0x"",
  ""gasLimit"": ""0x1c9c380"",
  ""difficulty"": ""0x0"",
  ""mixHash"": ""0x0000000000000000000000000000000000000000000000000000000000000000"",
  ""coinbase"": ""0x0000000000000000000000000000000000000000"",
  ""baseFeePerGas"": ""0x3b9aca00"",
  ""alloc"": {{
    ""{FundedAddress}"": {{ ""balance"": ""0x56bc75e2d63100000"" }}
  }}
}}";

        public static StandardGenesisDocument Document(string osakaTime = "0") =>
            StandardGenesisLoader.Parse(Json(osakaTime));
    }
}
