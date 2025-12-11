namespace Nethereum.Consensus.LightClient.Tests.Live
{
    public static class TestConstants
    {
        public const string BeaconApiUrl = "https://ethereum-beacon-api.publicnode.com";
        public const string ExecutionRpcUrl = "https://mainnet.infura.io/v3/2IgHC042dCtS6DwcOWefagLEcIe";

        public const string VitalikAddress = "0xAb5801a7D398351b8bE11C439e05C5B3259aeC9B";
        public const string UsdcContract = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48";
        public const string WethContract = "0xC02aaA39b223FE8D0A0e5C4F27eAD9083C756Cc2";
        public const string BoredApeContract = "0xBC4CA0EdA7647A8aB7C2061c2E118A18a936f13D";
        public const string DaiContract = "0x6B175474E89094C44Da98b954EescdeCB5f583019";

        public static byte[] MainnetGenesisValidatorsRoot =>
            Nethereum.Hex.HexConvertors.Extensions.HexByteConvertorExtensions.HexToByteArray(
                "0x4b363db94e286120d76eb905340fdd4e54bfe9f06bf33ff6cf5ad27f511bfe95");

        public static byte[] MainnetCurrentForkVersion =>
            Nethereum.Hex.HexConvertors.Extensions.HexByteConvertorExtensions.HexToByteArray(
                "0x06000000");
    }
}
