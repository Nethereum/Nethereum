using System.Numerics;
using Nethereum.DevChain;
using Nethereum.Util;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using NethereumDapp.ContractServices.MyToken;
using NethereumDapp.ContractServices.MyToken.ContractDefinition;
using Xunit;

namespace NethereumDapp.Tests;

public class MyTokenTests : IAsyncLifetime
{
    private const int ChainId = CHAIN_ID_VALUE;
    private const string PrivateKey = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
    private const string RecipientKey = "0x59c6995e998f97a5a0044966f0945389dc9e86dae88c7a8412f4603b6b78690d";

    private DevChainNode _node = null!;
    private IWeb3 _web3 = null!;
    private Account _account = null!;
    private Account _recipient = null!;

    public async Task InitializeAsync()
    {
        _account = new Account(PrivateKey, ChainId);
        _recipient = new Account(RecipientKey, ChainId);
        _node = await DevChainNode.CreateAndStartAsync(_account, _recipient);
        _web3 = _node.CreateWeb3(_account);
    }

    public Task DisposeAsync()
    {
        _node?.Dispose();
        return Task.CompletedTask;
    }

    private async Task<MyTokenService> DeployTokenAsync(
        string name = "MyToken", string symbol = "MTK", decimal initialSupply = 1_000_000)
    {
        var deployment = new MyTokenDeployment
        {
            Name = name,
            Symbol = symbol,
            InitialSupply = Web3.Convert.ToWei(initialSupply)
        };
        return await MyTokenService.DeployContractAndGetServiceAsync(_web3, deployment);
    }

    [Fact]
    public async Task Deploy_SetsNameAndSymbol()
    {
        var service = await DeployTokenAsync("TestToken", "TT");

        var name = await service.NameQueryAsync();
        var symbol = await service.SymbolQueryAsync();

        Assert.Equal("TestToken", name);
        Assert.Equal("TT", symbol);
    }

    [Fact]
    public async Task Deploy_MintsInitialSupplyToDeployer()
    {
        var service = await DeployTokenAsync(initialSupply: 500_000);

        var balance = await service.BalanceOfQueryAsync(_account.Address);
        var totalSupply = await service.TotalSupplyQueryAsync();
        var expected = Web3.Convert.ToWei(500_000);

        Assert.Equal(expected, balance);
        Assert.Equal(expected, totalSupply);
    }

    [Fact]
    public async Task Decimals_Returns18()
    {
        var service = await DeployTokenAsync();

        var decimals = await service.DecimalsQueryAsync();

        Assert.Equal(18, decimals);
    }

    [Fact]
    public async Task Mint_IncreasesBalance()
    {
        var service = await DeployTokenAsync();
        var amount = Web3.Convert.ToWei(500);

        await service.MintRequestAndWaitForReceiptAsync(_recipient.Address, amount);

        var balance = await service.BalanceOfQueryAsync(_recipient.Address);
        Assert.Equal(amount, balance);
    }

    [Fact]
    public async Task Mint_IncreasesTotalSupply()
    {
        var service = await DeployTokenAsync(initialSupply: 1000);
        var mintAmount = Web3.Convert.ToWei(200);

        await service.MintRequestAndWaitForReceiptAsync(_recipient.Address, mintAmount);

        var totalSupply = await service.TotalSupplyQueryAsync();
        var expected = Web3.Convert.ToWei(1200);
        Assert.Equal(expected, totalSupply);
    }

    [Fact]
    public async Task Transfer_MovesTokens()
    {
        var service = await DeployTokenAsync(initialSupply: 1000);
        var amount = Web3.Convert.ToWei(100);

        await service.TransferRequestAndWaitForReceiptAsync(_recipient.Address, amount);

        var senderBalance = await service.BalanceOfQueryAsync(_account.Address);
        var recipientBalance = await service.BalanceOfQueryAsync(_recipient.Address);

        Assert.Equal(Web3.Convert.ToWei(900), senderBalance);
        Assert.Equal(Web3.Convert.ToWei(100), recipientBalance);
    }

    [Fact]
    public async Task Approve_And_TransferFrom()
    {
        var service = await DeployTokenAsync(initialSupply: 1000);
        var amount = Web3.Convert.ToWei(50);

        await service.ApproveRequestAndWaitForReceiptAsync(_recipient.Address, amount);

        var allowance = await service.AllowanceQueryAsync(_account.Address, _recipient.Address);
        Assert.Equal(amount, allowance);

        var recipientWeb3 = _node.CreateWeb3(_recipient);
        var recipientService = new MyTokenService(recipientWeb3, service.ContractHandler.ContractAddress);

        await recipientService.TransferFromRequestAndWaitForReceiptAsync(
            _account.Address, _recipient.Address, amount);

        var recipientBalance = await service.BalanceOfQueryAsync(_recipient.Address);
        Assert.Equal(amount, recipientBalance);
    }
}
