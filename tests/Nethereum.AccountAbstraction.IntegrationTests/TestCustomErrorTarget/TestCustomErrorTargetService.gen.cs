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
using Nethereum.AccountAbstraction.IntegrationTests.TestCustomErrorTarget.ContractDefinition;

namespace Nethereum.AccountAbstraction.IntegrationTests.TestCustomErrorTarget
{
    public partial class TestCustomErrorTargetService: TestCustomErrorTargetServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, TestCustomErrorTargetDeployment testCustomErrorTargetDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<TestCustomErrorTargetDeployment>().SendRequestAndWaitForReceiptAsync(testCustomErrorTargetDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, TestCustomErrorTargetDeployment testCustomErrorTargetDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<TestCustomErrorTargetDeployment>().SendRequestAsync(testCustomErrorTargetDeployment);
        }

        public static async Task<TestCustomErrorTargetService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, TestCustomErrorTargetDeployment testCustomErrorTargetDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, testCustomErrorTargetDeployment, cancellationTokenSource);
            return new TestCustomErrorTargetService(web3, receipt.ContractAddress);
        }

        public TestCustomErrorTargetService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class TestCustomErrorTargetServiceBase: ContractWeb3ServiceBase
    {

        public TestCustomErrorTargetServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public virtual Task<string> BookSlotRequestAsync(BookSlotFunction bookSlotFunction)
        {
             return ContractHandler.SendRequestAsync(bookSlotFunction);
        }

        public virtual Task<TransactionReceipt> BookSlotRequestAndWaitForReceiptAsync(BookSlotFunction bookSlotFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(bookSlotFunction, cancellationToken);
        }

        public virtual Task<string> FailWithReasonRequestAsync(FailWithReasonFunction failWithReasonFunction)
        {
             return ContractHandler.SendRequestAsync(failWithReasonFunction);
        }

        public virtual Task<string> FailWithReasonRequestAsync()
        {
             return ContractHandler.SendRequestAsync<FailWithReasonFunction>();
        }

        public virtual Task<TransactionReceipt> FailWithReasonRequestAndWaitForReceiptAsync(FailWithReasonFunction failWithReasonFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(failWithReasonFunction, cancellationToken);
        }

        public virtual Task<TransactionReceipt> FailWithReasonRequestAndWaitForReceiptAsync(CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync<FailWithReasonFunction>(null, cancellationToken);
        }


        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(BookSlotFunction),
                typeof(FailWithReasonFunction)
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
                typeof(SlotTakenError)
            };
        }
    }
}
