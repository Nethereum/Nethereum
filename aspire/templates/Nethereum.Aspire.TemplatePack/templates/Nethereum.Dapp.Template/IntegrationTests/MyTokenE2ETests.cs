using System.Numerics;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using NethereumDapp.ContractServices.MyToken;
using NethereumDapp.ContractServices.MyToken.ContractDefinition;
using Xunit;

namespace NethereumDapp.IntegrationTests;

public class MyTokenE2ETests
{
    private const int ChainId = CHAIN_ID_VALUE;
    private const string PrivateKey = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";

    private readonly IWeb3 _web3;

    public MyTokenE2ETests()
    {
        var rpcUrl = Environment.GetEnvironmentVariable("DEVCHAIN_URL") ?? "http://localhost:8545";
        var account = new Account(PrivateKey, ChainId);
        _web3 = new Web3(account, rpcUrl);
    }

    [Fact]
    public async Task DevChain_IsResponding()
    {
        var blockNumber = await _web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
        Assert.True(blockNumber.Value >= 0);
    }

    [Fact]
    public async Task Deploy_And_QueryToken()
    {
        var deployment = new MyTokenDeployment
        {
            Name = "E2EToken",
            Symbol = "E2E",
            InitialSupply = Web3.Convert.ToWei(1_000_000)
        };

        var service = await MyTokenService.DeployContractAndGetServiceAsync(_web3, deployment);

        var name = await service.NameQueryAsync();
        var symbol = await service.SymbolQueryAsync();
        var totalSupply = await service.TotalSupplyQueryAsync();

        Assert.Equal("E2EToken", name);
        Assert.Equal("E2E", symbol);
        Assert.Equal(Web3.Convert.ToWei(1_000_000), totalSupply);
    }

    [Fact]
    public async Task Deploy_Transfer_And_VerifyIndexed()
    {
        var deployment = new MyTokenDeployment
        {
            Name = "IndexTest",
            Symbol = "IDX",
            InitialSupply = Web3.Convert.ToWei(10_000)
        };

        var service = await MyTokenService.DeployContractAndGetServiceAsync(_web3, deployment);

        var recipient = "0x70997970C51812dc3A010C7d01b50e0d17dc79C8";
        var amount = Web3.Convert.ToWei(100);

        var receipt = await service.TransferRequestAndWaitForReceiptAsync(recipient, amount);

        Assert.True(receipt.Succeeded());

        var recipientBalance = await service.BalanceOfQueryAsync(recipient);
        Assert.Equal(amount, recipientBalance);

        // Allow time for the indexer to process the block
        await Task.Delay(3000);

        // Verify the transaction is on-chain
        var tx = await _web3.Eth.Transactions.GetTransactionByHash.SendRequestAsync(receipt.TransactionHash);
        Assert.NotNull(tx);
        Assert.Equal(service.ContractHandler.ContractAddress.ToLower(), tx.To.ToLower());
    }
}
