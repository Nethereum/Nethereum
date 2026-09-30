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
using Nethereum.AccountAbstraction.Example.Contracts.SmartSessionRegistryStub.ContractDefinition;

namespace Nethereum.AccountAbstraction.Example.Contracts.SmartSessionRegistryStub
{
    public partial class SmartSessionRegistryStubService: SmartSessionRegistryStubServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, SmartSessionRegistryStubDeployment smartSessionRegistryStubDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<SmartSessionRegistryStubDeployment>().SendRequestAndWaitForReceiptAsync(smartSessionRegistryStubDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, SmartSessionRegistryStubDeployment smartSessionRegistryStubDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<SmartSessionRegistryStubDeployment>().SendRequestAsync(smartSessionRegistryStubDeployment);
        }

        public static async Task<SmartSessionRegistryStubService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, SmartSessionRegistryStubDeployment smartSessionRegistryStubDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, smartSessionRegistryStubDeployment, cancellationTokenSource);
            return new SmartSessionRegistryStubService(web3, receipt.ContractAddress);
        }

        public SmartSessionRegistryStubService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class SmartSessionRegistryStubServiceBase: ContractWeb3ServiceBase
    {

        public SmartSessionRegistryStubServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }















        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(Check2Function),
                typeof(Check3Function),
                typeof(Check1Function),
                typeof(CheckFunction),
                typeof(CheckForAccountFunction),
                typeof(CheckForAccount1Function),
                typeof(TrustAttestersFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {

            };
        }

        public override List<Type> GetAllErrorTypes()
        {
            return new List<Type>
            {

            };
        }
    }
}
