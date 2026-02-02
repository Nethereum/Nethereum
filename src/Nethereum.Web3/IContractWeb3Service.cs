using Nethereum.Contracts;

namespace Nethereum.Web3
{
    public interface IContractWeb3Service : IContractService
    {
        IWeb3 Web3 { get; }
    }
}
