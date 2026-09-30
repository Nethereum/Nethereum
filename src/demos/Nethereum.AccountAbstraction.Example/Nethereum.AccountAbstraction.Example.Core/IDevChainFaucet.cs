using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.AccountAbstraction.Example.Core
{
    public interface IDevChainFaucet
    {
        Task FundAsync(string address, decimal ether = 10m);

        Task<BigInteger> GetBalanceAsync(string address);
    }
}
