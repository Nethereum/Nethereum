using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.ABI.ABIDeserialisation;
using Nethereum.ABI.ABIRepository;
using Nethereum.BlockchainStore.EFCore;
using Nethereum.DataServices.ABIInfoStorage;

namespace Nethereum.Explorer.Services;

public class AbiCacheService
{
    private readonly ConcurrentDictionary<string, ABIInfo?> _cache = new();

    public bool TryGetValue(string key, out ABIInfo? value) => _cache.TryGetValue(key, out value);
    public void Set(string key, ABIInfo? value) => _cache[key] = value;
    public void Remove(string key) => _cache.TryRemove(key, out _);
}

public class AbiStorageService : IAbiStorageService
{
    private readonly IBlockchainDbContextFactory _contextFactory;
    private readonly IABIInfoStorage _compositeStorage;
    private readonly ILogger<AbiStorageService> _logger;
    private readonly AbiCacheService _cache;
    private readonly long _chainId;

    public AbiStorageService(
        IBlockchainDbContextFactory contextFactory,
        IABIInfoStorage compositeStorage,
        ILogger<AbiStorageService> logger,
        AbiCacheService cache,
        IOptions<ExplorerOptions> options)
    {
        _contextFactory = contextFactory;
        _compositeStorage = compositeStorage;
        _logger = logger;
        _cache = cache;
        _chainId = options.Value.ChainId;
    }

    public async Task<ABIInfo?> GetContractAbiAsync(string contractAddress)
    {
        contractAddress = contractAddress.ToLowerInvariant();

        if (_cache.TryGetValue(contractAddress, out var cached))
            return cached;

        using var context = _contextFactory.CreateContext();
        var contract = await context.Contracts
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Address == contractAddress);

        if (contract != null && !string.IsNullOrEmpty(contract.ABI))
        {
            var abiInfo = ABIInfo.FromABI(contract.ABI, contractAddress, contract.Name, null, _chainId);
            abiInfo.InitialiseContractABI();
            _cache.Set(contractAddress, abiInfo);
            return abiInfo;
        }

        try
        {
            var externalAbi = await _compositeStorage.GetABIInfoAsync(_chainId, contractAddress);
            if (externalAbi?.ContractABI != null)
            {
                await PersistToContractsTableAsync(contractAddress, externalAbi);
                _cache.Set(contractAddress, externalAbi);
                return externalAbi;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch ABI from external source for {Address}", contractAddress);
        }

        _cache.Set(contractAddress, null);
        return null;
    }

    public async Task StoreAbiAsync(string contractAddress, string abi, string? name = null, AbiSource source = AbiSource.LocalUpload)
    {
        contractAddress = contractAddress.ToLowerInvariant();
        _cache.Remove(contractAddress);

        var contractAbi = ABIDeserialiserFactory.DeserialiseContractABI(abi);
        if (contractAbi == null) return;

        using var context = _contextFactory.CreateContext();
        var existing = await context.Contracts
            .FirstOrDefaultAsync(c => c.Address == contractAddress);

        if (existing != null)
        {
            existing.ABI = abi;
            if (!string.IsNullOrEmpty(name))
                existing.Name = name;
        }
        else
        {
            context.Contracts.Add(new BlockchainProcessing.BlockStorage.Entities.Contract
            {
                Address = contractAddress,
                ABI = abi,
                Name = name ?? "",
                Code = "",
                Creator = "",
                TransactionHash = ""
            });
        }

        await context.SaveChangesAsync();
    }

    private async Task PersistToContractsTableAsync(string contractAddress, ABIInfo abiInfo)
    {
        try
        {
            using var context = _contextFactory.CreateContext();
            var existing = await context.Contracts
                .FirstOrDefaultAsync(c => c.Address == contractAddress);

            var abiString = abiInfo.ABI ?? "";

            if (existing != null)
            {
                if (string.IsNullOrEmpty(existing.ABI))
                {
                    existing.ABI = abiString;
                    if (!string.IsNullOrEmpty(abiInfo.ContractName))
                        existing.Name = abiInfo.ContractName;
                    await context.SaveChangesAsync();
                }
            }
            else
            {
                context.Contracts.Add(new BlockchainProcessing.BlockStorage.Entities.Contract
                {
                    Address = contractAddress,
                    ABI = abiString,
                    Name = abiInfo.ContractName ?? "",
                    Code = "",
                    Creator = "",
                    TransactionHash = ""
                });
                await context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist ABI to contracts table for {Address}", contractAddress);
        }
    }
}
