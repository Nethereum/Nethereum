using System.Threading.Tasks;

namespace Nethereum.Utils
{
    public interface IWaitStrategy
    {
        Task Apply(uint retryCount);
    }
}