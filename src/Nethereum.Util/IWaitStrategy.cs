using System.Threading.Tasks;

namespace Nethereum.Util
{
    public interface IWaitStrategy
    {
        Task Apply(uint retryCount);
    }
}