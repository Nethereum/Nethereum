using Nethereum.ABI.ABIRepository;

namespace Nethereum.Explorer.Services;

public interface IAbiStorageService
{
    Task<ABIInfo?> GetContractAbiAsync(string contractAddress);
    Task StoreAbiAsync(string contractAddress, string abi, string? name = null, AbiSource source = AbiSource.LocalUpload);
}
