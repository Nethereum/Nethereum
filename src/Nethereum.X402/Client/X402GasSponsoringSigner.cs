using Nethereum.ABI.EIP712.EIP2612;
using Nethereum.Contracts;
using Nethereum.Contracts.Standards.ERC20.ContractDefinition;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.TransactionTypes;
using Nethereum.Signer;
using Nethereum.X402.Models;
using Nethereum.X402.Permit2;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Nethereum.X402.Client;

internal sealed class X402GasSponsoringSigner
{
    private static readonly BigInteger FallbackMaxPriorityFeePerGas = Erc20ApprovalGasSponsoring.MinimumPriorityFeeCeiling;
    private static readonly BigInteger MaxApprovalAmount = BigInteger.Pow(2, 256) - 1;

    private readonly EthECKey _key;
    private readonly string _address;
    private readonly X402HttpClientOptions? _options;

    internal X402GasSponsoringSigner(EthECKey key, string address, X402HttpClientOptions? options)
    {
        _key = key;
        _address = address;
        _options = options;
    }

    internal async Task<JsonObject?> TrySignAsync(
        PaymentRequirements requirements,
        object? declaredExtensions,
        int chainId,
        BigInteger deadline)
    {
        return await TrySignEip2612PermitAsync(requirements, declaredExtensions, chainId, deadline)
            ?? await TrySignErc20ApprovalAsync(requirements, declaredExtensions, chainId);
    }

    private async Task<JsonObject?> TrySignErc20ApprovalAsync(
        PaymentRequirements requirements,
        object? declaredExtensions,
        int chainId)
    {
        if (!TryGetRpcClient(declaredExtensions, X402ExtensionKeys.Erc20ApprovalGasSponsoring, chainId, out var rpcClient))
            return null;

        var web3 = new Web3.Web3(new Web3.Accounts.Account(_key, chainId), rpcClient);

        if (await AllowanceCoversAsync(web3, requirements))
            return null;

        var approvalAmount = _options.CapSponsoredApprovalToPaymentAmount
            ? X402Number.Parse(requirements.Amount)
            : MaxApprovalAmount;
        var approve = new ApproveFunction { Spender = X402Permit2Addresses.Permit2, Value = approvalAmount }
            .CreateTransactionInput(requirements.Asset);
        approve.From = _address;
        approve.Type = new HexBigInteger(RPC.TransactionTypes.TransactionType.EIP1559.AsByte());
        approve.Gas = new HexBigInteger(Erc20ApprovalGasSponsoring.ReferenceApprovalGasLimit);
        approve.Nonce = await web3.Eth.Transactions.GetTransactionCount.SendRequestAsync(_address, BlockParameter.CreatePending());

        var fees = await SuggestFeesAsync(web3);
        approve.MaxFeePerGas = new HexBigInteger(fees.MaxFeePerGas);
        approve.MaxPriorityFeePerGas = new HexBigInteger(fees.MaxPriorityFeePerGas);

        var info = new Erc20ApprovalGasSponsoringInfo
        {
            From = _address,
            Asset = requirements.Asset,
            Spender = X402Permit2Addresses.Permit2,
            Amount = approvalAmount.ToString(),
            SignedTransaction = (await web3.TransactionManager.SignTransactionAsync(approve)).EnsureHexPrefix(),
            Version = "1"
        };

        return WrapInfo(X402ExtensionKeys.Erc20ApprovalGasSponsoring, info);
    }

    private static async Task<(BigInteger MaxFeePerGas, BigInteger MaxPriorityFeePerGas)> SuggestFeesAsync(Web3.Web3 web3)
    {
        try
        {
            var suggested = (await new Nethereum.RPC.Eth.EthMaxPriorityFeePerGas(web3.Client).SendRequestAsync()).Value;
            var priority = BigInteger.Min(BigInteger.Max(suggested, BigInteger.One), Erc20ApprovalGasSponsoring.MinimumPriorityFeeCeiling);
            var latest = await web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber.SendRequestAsync(BlockParameter.CreateLatest());
            var baseFee = latest.BaseFeePerGas!.Value;
            return ((baseFee * 3 + 1) / 2 + priority, priority);
        }
        catch (Exception)
        {
            return (Erc20ApprovalGasSponsoring.MinimumMaxFeeCeiling, FallbackMaxPriorityFeePerGas);
        }
    }

    private async Task<bool> AllowanceCoversAsync(Web3.Web3 web3, PaymentRequirements requirements)
    {
        try
        {
            var allowance = await web3.Eth.ERC20.GetContractService(requirements.Asset)
                .AllowanceQueryAsync(_address, X402Permit2Addresses.Permit2);
            return allowance >= X402Number.Parse(requirements.Amount);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private async Task<JsonObject?> TrySignEip2612PermitAsync(
        PaymentRequirements requirements,
        object? declaredExtensions,
        int chainId,
        BigInteger deadline)
    {
        if (!TryGetRpcClient(declaredExtensions, X402ExtensionKeys.Eip2612GasSponsoring, chainId, out var rpcClient))
            return null;

        var extra = ExactSchemeExtra.FromRequirements(requirements);
        if (string.IsNullOrEmpty(extra?.Name) || string.IsNullOrEmpty(extra.Version))
            return null;

        var amount = X402Number.Parse(requirements.Amount);
        var web3 = new Web3.Web3(rpcClient);

        if (await AllowanceCoversAsync(web3, requirements))
            return null;

        var nonce = await web3.Eth.GetContractHandler(requirements.Asset)
            .QueryAsync<Eip2612NoncesFunction, BigInteger>(new Eip2612NoncesFunction { Owner = _address });

        var permit = new Permit
        {
            Owner = _address,
            Spender = X402Permit2Addresses.Permit2,
            Value = amount,
            Nonce = nonce,
            Deadline = deadline
        };
        var signature = new Eip2612PermitSigner().Sign(permit, extra.Name, extra.Version, chainId, requirements.Asset, _key);

        var info = new Eip2612GasSponsoringInfo
        {
            From = _address,
            Asset = requirements.Asset,
            Spender = X402Permit2Addresses.Permit2,
            Amount = amount.ToString(),
            Nonce = nonce.ToString(),
            Deadline = deadline.ToString(),
            Signature = signature,
            Version = "1"
        };

        return WrapInfo(X402ExtensionKeys.Eip2612GasSponsoring, info);
    }

    [MemberNotNullWhen(true, nameof(_options))]
    private bool TryGetRpcClient(object? declaredExtensions, string extensionKey, int chainId, [NotNullWhen(true)] out IClient? rpcClient)
    {
        rpcClient = null;
        return X402ExtensionMerge.Declares(declaredExtensions, extensionKey)
            && _options?.RpcClientsByChainId != null
            && _options.RpcClientsByChainId.TryGetValue(chainId, out rpcClient);
    }

    private static JsonObject WrapInfo<T>(string extensionKey, T info) =>
        new() { [extensionKey] = new JsonObject { ["info"] = JsonSerializer.SerializeToNode(info) } };
}
