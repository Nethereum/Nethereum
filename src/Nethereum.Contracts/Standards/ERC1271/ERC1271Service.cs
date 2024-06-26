using Nethereum.Contracts.Services;
using Nethereum.ABI;
using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using System.Linq;
using Nethereum.Util;

namespace Nethereum.Contracts.Standards.ERC1271
{
    public class ERC1271Service
    {
        private readonly IEthApiContractService _ethApiContractService;

        public ERC1271Service(IEthApiContractService ethApiContractService)
        {
            _ethApiContractService = ethApiContractService;
        }

        public ERC1271ContractService GetContractService(string contractAddress)
        {
            return new ERC1271ContractService(_ethApiContractService, contractAddress);
        }

    }


}
