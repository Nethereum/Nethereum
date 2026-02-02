using Nethereum.Contracts.ContractHandlers;

namespace Nethereum.Contracts
{
    public interface IContractHandlerService
    {
        ContractHandler ContractHandler { get; set; }
        string ContractAddress { get; }
    }
}
