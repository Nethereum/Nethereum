using System.Numerics;
using Microsoft.Extensions.Logging;
using Nethereum.Contracts.Standards.ERC20;
using Nethereum.Contracts.Standards.ERC721;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Explorer.Services;

public class RpcQueryService : IRpcQueryService
{
    private readonly ExplorerWeb3Factory _web3Factory;
    private readonly ILogger<RpcQueryService> _logger;

    public RpcQueryService(ExplorerWeb3Factory web3Factory, ILogger<RpcQueryService> logger)
    {
        _web3Factory = web3Factory;
        _logger = logger;
    }

    public bool IsAvailable => _web3Factory.IsAvailable;

    public async Task<BigInteger> GetBalanceAsync(string address)
    {
        var web3 = _web3Factory.GetWeb3();
        if (web3 == null) return BigInteger.Zero;
        var balance = await web3.Eth.GetBalance.SendRequestAsync(address, BlockParameter.CreateLatest());
        return balance.Value;
    }

    public async Task<BigInteger> GetChainIdAsync()
    {
        var web3 = _web3Factory.GetWeb3();
        if (web3 == null) return BigInteger.Zero;
        var chainId = await web3.Eth.ChainId.SendRequestAsync();
        return chainId.Value;
    }

    public async Task<BigInteger> GetGasPriceAsync()
    {
        var web3 = _web3Factory.GetWeb3();
        if (web3 == null) return BigInteger.Zero;
        var gasPrice = await web3.Eth.GasPrice.SendRequestAsync();
        return gasPrice.Value;
    }

    public async Task<BigInteger> GetTransactionCountAsync(string address)
    {
        var web3 = _web3Factory.GetWeb3();
        if (web3 == null) return BigInteger.Zero;
        var nonce = await web3.Eth.Transactions.GetTransactionCount.SendRequestAsync(address, BlockParameter.CreateLatest());
        return nonce.Value;
    }

    public async Task<string> GetCodeAsync(string address)
    {
        var web3 = _web3Factory.GetWeb3();
        if (web3 == null) return "0x";
        return await web3.Eth.GetCode.SendRequestAsync(address, BlockParameter.CreateLatest());
    }

    public async Task<TokenInfo?> GetTokenInfoAsync(string tokenAddress, string holderAddress)
    {
        var web3 = _web3Factory.GetWeb3();
        if (web3 == null) return null;
        try
        {
            var service = new ERC20ContractService(web3.Eth, tokenAddress);

            var nameTask = service.NameQueryAsync();
            var symbolTask = service.SymbolQueryAsync();
            var decimalsTask = service.DecimalsQueryAsync();
            var balanceTask = service.BalanceOfQueryAsync(holderAddress);

            await Task.WhenAll(nameTask, symbolTask, decimalsTask, balanceTask);

            return new TokenInfo
            {
                Address = tokenAddress,
                Name = nameTask.Result,
                Symbol = symbolTask.Result,
                Decimals = decimalsTask.Result,
                Balance = balanceTask.Result
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to fetch ERC20 token info for {Address}", tokenAddress);
            return null;
        }
    }

    public async Task<BlobTransactionInfo?> GetBlobTransactionInfoAsync(string txHash)
    {
        var web3 = _web3Factory.GetWeb3();
        if (web3 == null) return null;
        try
        {
            var result = await web3.Client.SendRequestAsync<Newtonsoft.Json.Linq.JObject>("eth_getTransactionByHash", null, txHash);
            if (result == null) return null;

            var blobHashes = result["blobVersionedHashes"]?.ToObject<List<string>>();
            var maxFeePerBlobGas = result["maxFeePerBlobGas"]?.ToString();

            if (blobHashes == null || !blobHashes.Any()) return null;

            return new BlobTransactionInfo
            {
                MaxFeePerBlobGas = maxFeePerBlobGas ?? "0",
                BlobVersionedHashes = blobHashes
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to fetch blob transaction info for {TxHash}", txHash);
            return null;
        }
    }

    public async Task<NftTokenInfo?> GetNftTokenInfoAsync(string contractAddress, string tokenId)
    {
        var web3 = _web3Factory.GetWeb3();
        if (web3 == null) return null;
        try
        {
            var service = new ERC721ContractService(web3.Eth, contractAddress);

            string? name = null, symbol = null, tokenUri = null;
            try { name = await service.NameQueryAsync(); } catch (Exception ex) { _logger.LogDebug(ex, "ERC721 name() failed for {Address}", contractAddress); }
            try { symbol = await service.SymbolQueryAsync(); } catch (Exception ex) { _logger.LogDebug(ex, "ERC721 symbol() failed for {Address}", contractAddress); }
            try { tokenUri = await service.TokenURIQueryAsync(ExplorerFormatUtils.ParseBigInteger(tokenId)); } catch (Exception ex) { _logger.LogDebug(ex, "ERC721 tokenURI() failed for {Address} #{TokenId}", contractAddress, tokenId); }

            return new NftTokenInfo
            {
                ContractAddress = contractAddress,
                TokenId = tokenId,
                Name = name,
                Symbol = symbol,
                TokenUri = tokenUri
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to fetch NFT info for {Address} #{TokenId}", contractAddress, tokenId);
            return null;
        }
    }

    public async Task<PendingTransactionsResult> GetPendingTransactionsAsync()
    {
        var web3 = _web3Factory.GetWeb3();
        if (web3 == null) return new PendingTransactionsResult();
        try
        {
            var result = await web3.Client.SendRequestAsync<Newtonsoft.Json.Linq.JObject>("txpool_content");
            if (result == null) return new PendingTransactionsResult();

            var pending = ParseTxPoolSection(result["pending"] as Newtonsoft.Json.Linq.JObject);
            var queued = ParseTxPoolSection(result["queued"] as Newtonsoft.Json.Linq.JObject);

            return new PendingTransactionsResult { Pending = pending, Queued = queued, IsSupported = true };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "txpool_content not available");
            return new PendingTransactionsResult();
        }
    }

    private static List<PendingTransactionInfo> ParseTxPoolSection(Newtonsoft.Json.Linq.JObject? section)
    {
        var list = new List<PendingTransactionInfo>();
        if (section == null) return list;

        foreach (var addressEntry in section.Properties())
        {
            var fromAddress = addressEntry.Name;
            if (addressEntry.Value is not Newtonsoft.Json.Linq.JObject nonceMap) continue;

            foreach (var nonceEntry in nonceMap.Properties())
            {
                if (nonceEntry.Value is not Newtonsoft.Json.Linq.JObject tx) continue;

                list.Add(new PendingTransactionInfo
                {
                    Hash = tx["hash"]?.ToString() ?? "",
                    From = fromAddress,
                    To = tx["to"]?.ToString(),
                    Value = tx["value"]?.ToString() ?? "0x0",
                    GasPrice = tx["gasPrice"]?.ToString() ?? "0x0",
                    Gas = tx["gas"]?.ToString() ?? "0x0",
                    Nonce = tx["nonce"]?.ToString() ?? "0x0",
                    Input = tx["input"]?.ToString()
                });
            }
        }

        return list;
    }

    public async Task<List<AuthorizationInfo>> GetTransactionAuthorizationsAsync(string txHash)
    {
        var web3 = _web3Factory.GetWeb3();
        if (web3 == null) return new();
        try
        {
            var tx = await web3.Eth.Transactions.GetTransactionByHash.SendRequestAsync(txHash);
            if (tx?.AuthorisationList == null || !tx.AuthorisationList.Any())
                return new();

            return tx.AuthorisationList.Select(a => new AuthorizationInfo
            {
                ChainId = a.ChainId?.Value.ToString() ?? "0",
                Address = a.Address ?? "",
                Nonce = a.Nonce?.Value.ToString() ?? "0",
                YParity = a.YParity ?? "",
                R = a.R ?? "",
                S = a.S ?? ""
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to fetch authorization list for {TxHash}", txHash);
            return new();
        }
    }
}
