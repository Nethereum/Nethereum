using Moq;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.Blocks;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.RPC.IntegrationTests.Testers.Service
{
    public class ServiceTestBase
    {
        protected readonly Mock<IEthBlockNumber> _blockNumberMock = new Mock<IEthBlockNumber>();

        protected void SetupBlockNumberMock(BigInteger currentBlockNumber)
        {
            _blockNumberMock.Setup(b => b.SendRequestAsync(null))
                .Returns(() =>
                {
                    return Task.FromResult(currentBlockNumber.ToHexBigInteger());
                });
        }
    }
}
