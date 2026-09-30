using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using System.Numerics;

namespace Nethereum.X402.IntegrationTests.Helpers;

public class USDCDeploymentHelper
{
    private readonly Nethereum.Web3.Web3 _web3;
    private readonly Account _deployerAccount;
    private string? _contractAddress;
    private Contract? _contract;

    private const string CONTRACT_ABI = @"[
        {""inputs"":[{""internalType"":""address"",""name"":""account"",""type"":""address""}],""name"":""balanceOf"",""outputs"":[{""internalType"":""uint256"",""name"":"""",""type"":""uint256""}],""stateMutability"":""view"",""type"":""function""},
        {""inputs"":[],""name"":""name"",""outputs"":[{""internalType"":""string"",""name"":"""",""type"":""string""}],""stateMutability"":""view"",""type"":""function""},
        {""inputs"":[],""name"":""symbol"",""outputs"":[{""internalType"":""string"",""name"":"""",""type"":""string""}],""stateMutability"":""view"",""type"":""function""},
        {""inputs"":[],""name"":""decimals"",""outputs"":[{""internalType"":""uint8"",""name"":"""",""type"":""uint8""}],""stateMutability"":""view"",""type"":""function""},
        {""inputs"":[{""internalType"":""address"",""name"":""to"",""type"":""address""},{""internalType"":""uint256"",""name"":""amount"",""type"":""uint256""}],""name"":""mint"",""outputs"":[],""stateMutability"":""nonpayable"",""type"":""function""},
        {""inputs"":[{""internalType"":""address"",""name"":""from"",""type"":""address""},{""internalType"":""address"",""name"":""to"",""type"":""address""},{""internalType"":""uint256"",""name"":""value"",""type"":""uint256""},{""internalType"":""uint256"",""name"":""validAfter"",""type"":""uint256""},{""internalType"":""uint256"",""name"":""validBefore"",""type"":""uint256""},{""internalType"":""bytes32"",""name"":""nonce"",""type"":""bytes32""},{""internalType"":""uint8"",""name"":""v"",""type"":""uint8""},{""internalType"":""bytes32"",""name"":""r"",""type"":""bytes32""},{""internalType"":""bytes32"",""name"":""s"",""type"":""bytes32""}],""name"":""transferWithAuthorization"",""outputs"":[],""stateMutability"":""nonpayable"",""type"":""function""},
        {""inputs"":[],""name"":""DOMAIN_SEPARATOR"",""outputs"":[{""internalType"":""bytes32"",""name"":"""",""type"":""bytes32""}],""stateMutability"":""view"",""type"":""function""},
        {""inputs"":[],""name"":""version"",""outputs"":[{""internalType"":""string"",""name"":"""",""type"":""string""}],""stateMutability"":""view"",""type"":""function""}
    ]";

    public USDCDeploymentHelper(Nethereum.Web3.Web3 web3, Account deployerAccount)
    {
        _web3 = web3;
        _deployerAccount = deployerAccount;
    }

    public async Task<string> DeployAsync(string tokenName = "USD Coin", string tokenSymbol = "USDC", byte decimals = 6, string version = "2")
    {
        var deploymentMessage = new MockUSDCDeployment
        {
            TokenName = tokenName,
            TokenSymbol = tokenSymbol,
            Decimals = decimals,
            Version = version,
            FromAddress = _deployerAccount.Address,
            Gas = 5000000
        };

        var deploymentHandler = _web3.Eth.GetContractDeploymentHandler<MockUSDCDeployment>();
        var receipt = await deploymentHandler.SendRequestAndWaitForReceiptAsync(deploymentMessage);

        if (receipt.Status != null && receipt.Status.Value == 0)
        {
            throw new InvalidOperationException($"Contract deployment failed. Transaction hash: {receipt.TransactionHash}");
        }

        _contractAddress = receipt.ContractAddress;
        _contract = _web3.Eth.GetContract(CONTRACT_ABI, _contractAddress);

        return _contractAddress;
    }

    public async Task<TransactionReceipt> MintAsync(string toAddress, BigInteger amount)
    {
        EnsureContractDeployed();

        var mintFunction = _contract!.GetFunction("mint");
        var transactionHash = await mintFunction.SendTransactionAsync(
            _deployerAccount.Address,
            new HexBigInteger(3000000),
            null,
            null,
            toAddress,
            amount);

        var receipt = await _web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(transactionHash);

        while (receipt == null)
        {
            await Task.Delay(500);
            receipt = await _web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(transactionHash);
        }

        return receipt;
    }

    public async Task<BigInteger> GetBalanceAsync(string address)
    {
        EnsureContractDeployed();

        var balanceOfFunction = _contract!.GetFunction("balanceOf");
        var balance = await balanceOfFunction.CallAsync<BigInteger>(address);

        return balance;
    }

    public async Task<string> GetNameAsync()
    {
        EnsureContractDeployed();

        var nameFunction = _contract!.GetFunction("name");
        return await nameFunction.CallAsync<string>();
    }

    public async Task<string> GetSymbolAsync()
    {
        EnsureContractDeployed();

        var symbolFunction = _contract!.GetFunction("symbol");
        return await symbolFunction.CallAsync<string>();
    }

    public async Task<byte> GetDecimalsAsync()
    {
        EnsureContractDeployed();

        var decimalsFunction = _contract!.GetFunction("decimals");
        return await decimalsFunction.CallAsync<byte>();
    }

    public async Task<string> GetVersionAsync()
    {
        EnsureContractDeployed();

        var versionFunction = _contract!.GetFunction("version");
        return await versionFunction.CallAsync<string>();
    }

    public async Task<byte[]> GetDomainSeparatorAsync()
    {
        EnsureContractDeployed();

        var domainSeparatorFunction = _contract!.GetFunction("DOMAIN_SEPARATOR");
        return await domainSeparatorFunction.CallAsync<byte[]>();
    }

    public string ContractAddress
    {
        get
        {
            EnsureContractDeployed();
            return _contractAddress!;
        }
    }

    public string GetContractABI()
    {
        return CONTRACT_ABI;
    }

    private void EnsureContractDeployed()
    {
        if (string.IsNullOrWhiteSpace(_contractAddress) || _contract == null)
        {
            throw new InvalidOperationException("Contract not deployed. Call DeployAsync() first.");
        }
    }
}
