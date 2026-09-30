using System.Numerics;
using System.Threading.Tasks;
using Nethereum.DevChain;
using Nethereum.JsonRpc.Client;
using Nethereum.Web3.Accounts;
using Nethereum.X402.IntegrationTests.Helpers;
using Xunit;

namespace Nethereum.X402.IntegrationTests.Blockchain;

[CollectionDefinition("X402 DevChain E2E")]
public class X402DevChainE2ECollection { }

public class X402DevChainFixture : IAsyncLifetime
{
    public const int ChainId = 31337;
    public const string TokenName = "USDC";
    public const string TokenVersion = "2";

    public const string FacilitatorKey = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
    public const string FacilitatorAddress = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
    public const string PayerKey = "0x59c6995e998f97a5a0044966f0945389dc9e86dae88c7a8412f4603b6b78690d";
    public const string PayerAddress = "0x70997970C51812dc3A010C7d01b50e0d17dc79C8";
    public const string RecipientAddress = "0x3C44CdDdB6a900fa2b585dd299e03d12FA4293BC";

    public const string PayerInitialBalance = "10000000";

    public DevChainNode Node { get; private set; } = null!;
    public string TokenAddress { get; private set; } = null!;

    public IClient Client => Node.CreateWeb3().Client;

    public async Task InitializeAsync()
    {
        var config = new DevChainConfig
        {
            ChainId = ChainId,
            BaseFee = 1_000_000_000,
            BlockGasLimit = 30_000_000,
            AutoMine = true
        };
        Node = DevChainNode.CreateInMemory(config);

        await Node.StartAsync(
            new[] { FacilitatorAddress, PayerAddress, RecipientAddress },
            Nethereum.Web3.Web3.Convert.ToWei(10000));

        var deployer = new Account(FacilitatorKey, ChainId);
        var deployerWeb3 = (Nethereum.Web3.Web3)Node.CreateWeb3(deployer);

        var usdc = new USDCDeploymentHelper(deployerWeb3, deployer);
        TokenAddress = await usdc.DeployAsync(TokenName, "USDC", 6, TokenVersion);
        await usdc.MintAsync(PayerAddress, BigInteger.Parse(PayerInitialBalance));
    }

    public Task DisposeAsync()
    {
        Node?.Dispose();
        return Task.CompletedTask;
    }
}
