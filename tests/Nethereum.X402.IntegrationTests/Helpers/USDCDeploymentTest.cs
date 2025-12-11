using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using System.Numerics;

namespace Nethereum.X402.IntegrationTests.Helpers;

/// <summary>
/// Tests for USDC contract deployment helper
/// </summary>
public class USDCDeploymentTest
{
    private const string RPC_URL = "http://localhost:8545";
    private const int CHAIN_ID = 84532;
    private const string DEPLOYER_KEY = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
    private const string TEST_ADDRESS = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";

    [Fact]
    public async Task Given_ValidParameters_When_DeployingContract_Then_ContractIsDeployedSuccessfully()
    {
        // Arrange
        var account = new Account(DEPLOYER_KEY, CHAIN_ID);
        var web3 = new Nethereum.Web3.Web3(account, RPC_URL);
        var helper = new USDCDeploymentHelper(web3, account);

        // Act
        var address = await helper.DeployAsync("USD Coin", "USDC", 6, "2");

        // Assert
        Assert.NotNull(address);
        Assert.StartsWith("0x", address);
        Assert.Equal(42, address.Length); // 0x + 40 hex chars

        // Verify contract is deployed at that address
        var code = await web3.Eth.GetCode.SendRequestAsync(address);
        Assert.NotNull(code);
        Assert.NotEqual("0x", code); // Should have code
        Assert.NotEmpty(code);
    }

    [Fact]
    public async Task Given_DeployedContract_When_MintingTokens_Then_BalanceIsUpdated()
    {
        // Arrange
        var account = new Account(DEPLOYER_KEY, CHAIN_ID);
        var web3 = new Nethereum.Web3.Web3(account, RPC_URL);
        var helper = new USDCDeploymentHelper(web3, account);

        await helper.DeployAsync("USD Coin", "USDC", 6, "2");

        // Act - Mint 1000 USDC (1000 * 10^6 atomic units)
        var mintAmount = new BigInteger(1000) * BigInteger.Pow(10, 6);
        var mintReceipt = await helper.MintAsync(TEST_ADDRESS, mintAmount);

        // Assert - Check mint was successful
        Assert.NotNull(mintReceipt);
        Assert.NotNull(mintReceipt.TransactionHash);
        Assert.True(mintReceipt.Status?.Value == 1, "Mint transaction should succeed");

        // Verify balance
        var balance = await helper.GetBalanceAsync(TEST_ADDRESS);
        Assert.Equal(mintAmount, balance);
    }

    [Fact]
    public async Task Given_DeployedContract_When_GettingMetadata_Then_MetadataIsCorrect()
    {
        // Arrange
        var account = new Account(DEPLOYER_KEY, CHAIN_ID);
        var web3 = new Nethereum.Web3.Web3(account, RPC_URL);
        var helper = new USDCDeploymentHelper(web3, account);

        await helper.DeployAsync("Test Token", "TST", 18, "1");

        // Act & Assert
        var name = await helper.GetNameAsync();
        Assert.Equal("Test Token", name);

        var symbol = await helper.GetSymbolAsync();
        Assert.Equal("TST", symbol);

        var decimals = await helper.GetDecimalsAsync();
        Assert.Equal((byte)18, decimals);

        var version = await helper.GetVersionAsync();
        Assert.Equal("1", version);

        var domainSeparator = await helper.GetDomainSeparatorAsync();
        Assert.NotNull(domainSeparator);
        Assert.Equal(32, domainSeparator.Length); // bytes32 = 32 bytes
    }
}
