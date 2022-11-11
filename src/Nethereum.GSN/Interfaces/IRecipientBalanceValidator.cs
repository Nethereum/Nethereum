using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.GSN.Interfaces
{
    public interface IRecipientBalanceValidator
    {
        Task Validate(string hubAddress, string recipient, BigInteger gasLimit, BigInteger gasPrice, BigInteger relayFee);
    }
}