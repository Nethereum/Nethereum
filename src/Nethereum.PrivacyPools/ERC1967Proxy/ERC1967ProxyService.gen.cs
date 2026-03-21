using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Web3;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts.CQS;
using Nethereum.Contracts.ContractHandlers;
using Nethereum.Contracts;
using System.Threading;
using Nethereum.PrivacyPools.ERC1967Proxy.ContractDefinition;

namespace Nethereum.PrivacyPools.ERC1967Proxy
{
    public partial class ERC1967ProxyService: ERC1967ProxyServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, ERC1967ProxyDeployment eRC1967ProxyDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<ERC1967ProxyDeployment>().SendRequestAndWaitForReceiptAsync(eRC1967ProxyDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, ERC1967ProxyDeployment eRC1967ProxyDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<ERC1967ProxyDeployment>().SendRequestAsync(eRC1967ProxyDeployment);
        }

        public static async Task<ERC1967ProxyService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, ERC1967ProxyDeployment eRC1967ProxyDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, eRC1967ProxyDeployment, cancellationTokenSource);
            return new ERC1967ProxyService(web3, receipt.ContractAddress);
        }

        public ERC1967ProxyService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class ERC1967ProxyServiceBase: ContractWeb3ServiceBase
    {

        public ERC1967ProxyServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {

            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(UpgradedEventDTO)
            };
        }

        public override List<Type> GetAllErrorTypes()
        {
            return new List<Type>
            {
                typeof(AddressEmptyCodeError),
                typeof(ERC1967InvalidImplementationError),
                typeof(ERC1967NonPayableError),
                typeof(FailedCallError)
            };
        }
    }
}
