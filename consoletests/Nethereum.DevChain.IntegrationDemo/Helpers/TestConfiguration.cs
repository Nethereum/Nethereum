using System.Numerics;

namespace Nethereum.DevChain.IntegrationDemo.Helpers;

public static class TestConfiguration
{
    public const string DefaultPrivateKey = "ac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
    public const string DefaultAddress = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
    public const int ChainId = 31337;
    public const int DefaultPort = 8545;

    public static readonly BigInteger InitialBalance = BigInteger.Parse("10000000000000000000000");

    public const string MainnetRpcUrl = "https://eth.llamarpc.com";
    public const string SepoliaRpcUrl = "https://sepolia.drpc.org";

    public const string UsdcContractAddress = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48";
    public const string DaiContractAddress = "0x6B175474E89094C44Da98b954EescdECF8e0E9Fa";

    public static string SecondaryAddress => "0x70997970C51812dc3A010C7d01b50e0d17dc79C8";
    public static string SecondaryPrivateKey => "59c6995e998f97a5a0044966f0945389dc9e86dae88c7a8412f4603b6b78690d";
}
