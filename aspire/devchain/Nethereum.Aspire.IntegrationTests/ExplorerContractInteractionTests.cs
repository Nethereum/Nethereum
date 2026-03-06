using Nethereum.ABI.FunctionEncoding;
using Nethereum.Aspire.IntegrationTests.Infrastructure;
using Nethereum.Explorer.Services;
using Newtonsoft.Json.Linq;
using System.Numerics;
using Xunit;

namespace Nethereum.Aspire.IntegrationTests;

[Collection("Aspire")]
public class ExplorerContractInteractionTests
{
    private readonly AspireFixture _fixture;

    public ExplorerContractInteractionTests(AspireFixture fixture)
    {
        _fixture = fixture;
    }

    public const string FULL_ERC20_ABI = @"[
        {""inputs"":[],""stateMutability"":""nonpayable"",""type"":""constructor""},
        {""inputs"":[],""name"":""name"",""outputs"":[{""internalType"":""string"",""name"":"""",""type"":""string""}],""stateMutability"":""view"",""type"":""function""},
        {""inputs"":[],""name"":""symbol"",""outputs"":[{""internalType"":""string"",""name"":"""",""type"":""string""}],""stateMutability"":""view"",""type"":""function""},
        {""inputs"":[],""name"":""decimals"",""outputs"":[{""internalType"":""uint8"",""name"":"""",""type"":""uint8""}],""stateMutability"":""view"",""type"":""function""},
        {""inputs"":[],""name"":""totalSupply"",""outputs"":[{""internalType"":""uint256"",""name"":"""",""type"":""uint256""}],""stateMutability"":""view"",""type"":""function""},
        {""inputs"":[{""internalType"":""address"",""name"":""account"",""type"":""address""}],""name"":""balanceOf"",""outputs"":[{""internalType"":""uint256"",""name"":"""",""type"":""uint256""}],""stateMutability"":""view"",""type"":""function""},
        {""inputs"":[{""internalType"":""address"",""name"":""owner"",""type"":""address""},{""internalType"":""address"",""name"":""spender"",""type"":""address""}],""name"":""allowance"",""outputs"":[{""internalType"":""uint256"",""name"":"""",""type"":""uint256""}],""stateMutability"":""view"",""type"":""function""},
        {""inputs"":[{""internalType"":""address"",""name"":""spender"",""type"":""address""},{""internalType"":""uint256"",""name"":""value"",""type"":""uint256""}],""name"":""approve"",""outputs"":[{""internalType"":""bool"",""name"":"""",""type"":""bool""}],""stateMutability"":""nonpayable"",""type"":""function""},
        {""inputs"":[{""internalType"":""address"",""name"":""from"",""type"":""address""},{""internalType"":""address"",""name"":""to"",""type"":""address""},{""internalType"":""uint256"",""name"":""value"",""type"":""uint256""}],""name"":""transferFrom"",""outputs"":[{""internalType"":""bool"",""name"":"""",""type"":""bool""}],""stateMutability"":""nonpayable"",""type"":""function""},
        {""inputs"":[{""internalType"":""address"",""name"":""to"",""type"":""address""},{""internalType"":""uint256"",""name"":""amount"",""type"":""uint256""}],""name"":""mint"",""outputs"":[],""stateMutability"":""nonpayable"",""type"":""function""},
        {""inputs"":[{""internalType"":""address"",""name"":""to"",""type"":""address""},{""internalType"":""uint256"",""name"":""value"",""type"":""uint256""}],""name"":""transfer"",""outputs"":[{""internalType"":""bool"",""name"":"""",""type"":""bool""}],""stateMutability"":""nonpayable"",""type"":""function""},
        {""anonymous"":false,""inputs"":[{""indexed"":true,""internalType"":""address"",""name"":""from"",""type"":""address""},{""indexed"":true,""internalType"":""address"",""name"":""to"",""type"":""address""},{""indexed"":false,""internalType"":""uint256"",""name"":""value"",""type"":""uint256""}],""name"":""Transfer"",""type"":""event""},
        {""anonymous"":false,""inputs"":[{""indexed"":true,""internalType"":""address"",""name"":""owner"",""type"":""address""},{""indexed"":true,""internalType"":""address"",""name"":""spender"",""type"":""address""},{""indexed"":false,""internalType"":""uint256"",""name"":""value"",""type"":""uint256""}],""name"":""Approval"",""type"":""event""}
    ]";

    [Fact]
    public async Task ReadContract_Name_ReturnsTokenName()
    {
        var contractAddress = await ERC20TestHelper.DeployAsync(_fixture.Web3);

        var service = CreateContractInteractionService();
        var result = await service.CallReadFunctionAsync(contractAddress, FULL_ERC20_ABI, "name", new JObject());

        Assert.NotNull(result);
        var nameValue = result!.GetValue("returnValue1")?.ToString()
            ?? result.Properties().FirstOrDefault()?.Value?.ToString();
        Assert.Equal("Mock Token", nameValue);
    }

    [Fact]
    public async Task ReadContract_Symbol_ReturnsTokenSymbol()
    {
        var contractAddress = await ERC20TestHelper.DeployAsync(_fixture.Web3);

        var service = CreateContractInteractionService();
        var result = await service.CallReadFunctionAsync(contractAddress, FULL_ERC20_ABI, "symbol", new JObject());

        Assert.NotNull(result);
        var symbolValue = result!.GetValue("returnValue1")?.ToString()
            ?? result.Properties().FirstOrDefault()?.Value?.ToString();
        Assert.Equal("MOCK", symbolValue);
    }

    [Fact]
    public async Task ReadContract_Decimals_Returns18()
    {
        var contractAddress = await ERC20TestHelper.DeployAsync(_fixture.Web3);

        var service = CreateContractInteractionService();
        var result = await service.CallReadFunctionAsync(contractAddress, FULL_ERC20_ABI, "decimals", new JObject());

        Assert.NotNull(result);
        var decimalsValue = result!.Properties().FirstOrDefault()?.Value?.ToString();
        Assert.Equal("18", decimalsValue);
    }

    [Fact]
    public async Task ReadContract_BalanceOf_ReturnsCorrectBalance()
    {
        var contractAddress = await ERC20TestHelper.DeployAsync(_fixture.Web3);

        var mintAmount = BigInteger.Parse("1000000000000000000000");
        await ERC20TestHelper.MintAsync(_fixture.Web3, contractAddress, AspireFixture.Address, mintAmount);

        var service = CreateContractInteractionService();
        var inputValues = new JObject { ["account"] = AspireFixture.Address };
        var result = await service.CallReadFunctionAsync(contractAddress, FULL_ERC20_ABI, "balanceOf", inputValues);

        Assert.NotNull(result);
        var balanceStr = result!.Properties().FirstOrDefault()?.Value?.ToString();
        Assert.NotNull(balanceStr);
        Assert.True(BigInteger.TryParse(balanceStr, out var balance));
        Assert.Equal(mintAmount, balance);
    }

    [Fact]
    public async Task ReadContract_TotalSupply_AfterMint_ReturnsCorrectSupply()
    {
        var contractAddress = await ERC20TestHelper.DeployAsync(_fixture.Web3);

        var mintAmount = BigInteger.Parse("5000000000000000000000");
        await ERC20TestHelper.MintAsync(_fixture.Web3, contractAddress, AspireFixture.Address, mintAmount);

        var service = CreateContractInteractionService();
        var result = await service.CallReadFunctionAsync(contractAddress, FULL_ERC20_ABI, "totalSupply", new JObject());

        Assert.NotNull(result);
        var supplyStr = result!.Properties().FirstOrDefault()?.Value?.ToString();
        Assert.NotNull(supplyStr);
        Assert.True(BigInteger.TryParse(supplyStr, out var supply));
        Assert.Equal(mintAmount, supply);
    }

    [Fact]
    public async Task WriteContract_Mint_ThenReadBalance_Succeeds()
    {
        var contractAddress = await ERC20TestHelper.DeployAsync(_fixture.Web3);

        var service = CreateSignerContractInteractionService();

        var mintInput = new JObject
        {
            ["to"] = AspireFixture.Address,
            ["amount"] = "2000000000000000000000"
        };
        var receipt = await service.SendWriteFunctionAsync(contractAddress, FULL_ERC20_ABI, "mint", mintInput);

        Assert.NotNull(receipt);
        Assert.False(receipt!.HasErrors(), $"Mint transaction failed: status={receipt.Status?.Value}");

        var readService = CreateContractInteractionService();
        var balanceInput = new JObject { ["account"] = AspireFixture.Address };
        var result = await readService.CallReadFunctionAsync(contractAddress, FULL_ERC20_ABI, "balanceOf", balanceInput);

        Assert.NotNull(result);
        var balanceStr = result!.Properties().FirstOrDefault()?.Value?.ToString();
        Assert.True(BigInteger.TryParse(balanceStr, out var balance));
        Assert.Equal(BigInteger.Parse("2000000000000000000000"), balance);
    }

    [Fact]
    public async Task WriteContract_Transfer_UpdatesBalances()
    {
        var contractAddress = await ERC20TestHelper.DeployAsync(_fixture.Web3);

        var mintAmount = BigInteger.Parse("1000000000000000000000");
        await ERC20TestHelper.MintAsync(_fixture.Web3, contractAddress, AspireFixture.Address, mintAmount);

        var service = CreateSignerContractInteractionService();
        var recipient = "0x70997970C51812dc3A010C7d01b50e0d17dc79C8";
        var transferAmount = "100000000000000000000";

        var transferInput = new JObject
        {
            ["to"] = recipient,
            ["value"] = transferAmount
        };
        var receipt = await service.SendWriteFunctionAsync(contractAddress, FULL_ERC20_ABI, "transfer", transferInput);

        Assert.NotNull(receipt);
        Assert.False(receipt!.HasErrors(), $"Transfer transaction failed");

        var readService = CreateContractInteractionService();

        var senderBalance = await readService.CallReadFunctionAsync(
            contractAddress, FULL_ERC20_ABI, "balanceOf", new JObject { ["account"] = AspireFixture.Address });
        var recipientBalance = await readService.CallReadFunctionAsync(
            contractAddress, FULL_ERC20_ABI, "balanceOf", new JObject { ["account"] = recipient });

        Assert.NotNull(senderBalance);
        Assert.NotNull(recipientBalance);

        var senderBal = BigInteger.Parse(senderBalance!.Properties().FirstOrDefault()?.Value?.ToString()!);
        var recipientBal = BigInteger.Parse(recipientBalance!.Properties().FirstOrDefault()?.Value?.ToString()!);

        Assert.Equal(BigInteger.Parse("900000000000000000000"), senderBal);
        Assert.Equal(BigInteger.Parse("100000000000000000000"), recipientBal);
    }

    [Fact]
    public async Task AbiCache_StoreAndRetrieve_WorksEndToEnd()
    {
        var contractAddress = await ERC20TestHelper.DeployAsync(_fixture.Web3);

        var chainHead = await _fixture.Web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();

        await using var connection = await _fixture.CreateDbConnectionAsync();
        await TestContractDeployer.WaitForIndexerCaughtUpAsync(connection, (long)chainHead.Value);

        await using var insertCmd = connection.CreateCommand();
        insertCmd.CommandText = @"INSERT INTO ""Contracts"" (address, abi, name, code, creator, transactionhash)
            VALUES (@address, @abi, @name, '', '', '')
            ON CONFLICT DO NOTHING";
        insertCmd.Parameters.AddWithValue("address", contractAddress.ToLower());
        insertCmd.Parameters.AddWithValue("abi", FULL_ERC20_ABI);
        insertCmd.Parameters.AddWithValue("name", "Mock Token");
        await insertCmd.ExecuteNonQueryAsync();

        await using var selectCmd = connection.CreateCommand();
        selectCmd.CommandText = @"SELECT abi FROM ""Contracts"" WHERE address = @address";
        selectCmd.Parameters.AddWithValue("address", contractAddress.ToLower());
        var storedAbi = (string?)(await selectCmd.ExecuteScalarAsync());

        Assert.NotNull(storedAbi);
        Assert.Contains("balanceOf", storedAbi);
        Assert.Contains("transfer", storedAbi);
        Assert.Contains("name", storedAbi);

        var service = CreateContractInteractionService();
        var result = await service.CallReadFunctionAsync(contractAddress, storedAbi!, "name", new JObject());
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ReadContract_Allowance_WithTwoAddressParams_Works()
    {
        var contractAddress = await ERC20TestHelper.DeployAsync(_fixture.Web3);

        var service = CreateContractInteractionService();
        var inputValues = new JObject
        {
            ["owner"] = AspireFixture.Address,
            ["spender"] = "0x70997970C51812dc3A010C7d01b50e0d17dc79C8"
        };
        var result = await service.CallReadFunctionAsync(contractAddress, FULL_ERC20_ABI, "allowance", inputValues);

        Assert.NotNull(result);
        var allowanceStr = result!.Properties().FirstOrDefault()?.Value?.ToString();
        Assert.Equal("0", allowanceStr);
    }

    [Fact]
    public async Task ServiceFlags_HasRpcConnection_IsTrue()
    {
        var service = CreateContractInteractionService();
        Assert.True(service.HasRpcConnection, "Explorer should have RPC connection to devchain");
    }

    [Fact]
    public async Task ServiceFlags_HasDevAccount_IsTrue()
    {
        var service = CreateSignerContractInteractionService();
        Assert.True(service.HasDevAccount, "Explorer should have a dev account configured");
        Assert.NotNull(service.DevAccountAddress);
    }

    private ContractInteractionService CreateContractInteractionService()
    {
        var rpcUrl = _fixture.DevChainClient.BaseAddress!.ToString();
        var config = new TestConfiguration(new Dictionary<string, string?>
        {
            ["Explorer:RpcUrl"] = rpcUrl
        });
        return new ContractInteractionService(config);
    }

    private ContractInteractionService CreateSignerContractInteractionService()
    {
        var rpcUrl = _fixture.DevChainClient.BaseAddress!.ToString();
        var config = new TestConfiguration(new Dictionary<string, string?>
        {
            ["Explorer:RpcUrl"] = rpcUrl,
            ["Explorer:DevAccountPrivateKey"] = AspireFixture.PrivateKey
        });
        return new ContractInteractionService(config);
    }

    private class TestConfiguration : global::Microsoft.Extensions.Configuration.IConfiguration
    {
        private readonly Dictionary<string, string?> _data;

        public TestConfiguration(Dictionary<string, string?> data)
        {
            _data = data;
        }

        public string? this[string key]
        {
            get => _data.TryGetValue(key, out var val) ? val : null;
            set => _data[key] = value;
        }

        public global::Microsoft.Extensions.Configuration.IConfigurationSection GetSection(string key) => throw new NotImplementedException();
        public IEnumerable<global::Microsoft.Extensions.Configuration.IConfigurationSection> GetChildren() => throw new NotImplementedException();
        public global::Microsoft.Extensions.Primitives.IChangeToken GetReloadToken() => throw new NotImplementedException();
    }
}
