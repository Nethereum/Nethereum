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
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter.ContractDefinition;

namespace Nethereum.AccountAbstraction.IntegrationTests.TestCounter
{
    public partial class TestCounterService: TestCounterServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, TestCounterDeployment testCounterDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<TestCounterDeployment>().SendRequestAndWaitForReceiptAsync(testCounterDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, TestCounterDeployment testCounterDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<TestCounterDeployment>().SendRequestAsync(testCounterDeployment);
        }

        public static async Task<TestCounterService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, TestCounterDeployment testCounterDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, testCounterDeployment, cancellationTokenSource);
            return new TestCounterService(web3, receipt.ContractAddress);
        }

        public TestCounterService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class TestCounterServiceBase: ContractWeb3ServiceBase
    {

        public TestCounterServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public virtual Task<string> CountRequestAsync(CountFunction countFunction)
        {
             return ContractHandler.SendRequestAsync(countFunction);
        }

        public virtual Task<string> CountRequestAsync()
        {
             return ContractHandler.SendRequestAsync<CountFunction>();
        }

        public virtual Task<TransactionReceipt> CountRequestAndWaitForReceiptAsync(CountFunction countFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(countFunction, cancellationToken);
        }

        public virtual Task<TransactionReceipt> CountRequestAndWaitForReceiptAsync(CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync<CountFunction>(null, cancellationToken);
        }

        public Task<BigInteger> CountersQueryAsync(CountersFunction countersFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<CountersFunction, BigInteger>(countersFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> CountersQueryAsync(string returnValue1, BlockParameter blockParameter = null)
        {
            var countersFunction = new CountersFunction();
                countersFunction.ReturnValue1 = returnValue1;
            
            return ContractHandler.QueryAsync<CountersFunction, BigInteger>(countersFunction, blockParameter);
        }

        public virtual Task<string> GasWasterRequestAsync(GasWasterFunction gasWasterFunction)
        {
             return ContractHandler.SendRequestAsync(gasWasterFunction);
        }

        public virtual Task<TransactionReceipt> GasWasterRequestAndWaitForReceiptAsync(GasWasterFunction gasWasterFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(gasWasterFunction, cancellationToken);
        }

        public virtual Task<string> GasWasterRequestAsync(BigInteger repeat, string returnValue2)
        {
            var gasWasterFunction = new GasWasterFunction();
                gasWasterFunction.Repeat = repeat;
                gasWasterFunction.ReturnValue2 = returnValue2;
            
             return ContractHandler.SendRequestAsync(gasWasterFunction);
        }

        public virtual Task<TransactionReceipt> GasWasterRequestAndWaitForReceiptAsync(BigInteger repeat, string returnValue2, CancellationTokenSource cancellationToken = null)
        {
            var gasWasterFunction = new GasWasterFunction();
                gasWasterFunction.Repeat = repeat;
                gasWasterFunction.ReturnValue2 = returnValue2;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(gasWasterFunction, cancellationToken);
        }

        public virtual Task<string> JustemitRequestAsync(JustemitFunction justemitFunction)
        {
             return ContractHandler.SendRequestAsync(justemitFunction);
        }

        public virtual Task<string> JustemitRequestAsync()
        {
             return ContractHandler.SendRequestAsync<JustemitFunction>();
        }

        public virtual Task<TransactionReceipt> JustemitRequestAndWaitForReceiptAsync(JustemitFunction justemitFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(justemitFunction, cancellationToken);
        }

        public virtual Task<TransactionReceipt> JustemitRequestAndWaitForReceiptAsync(CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync<JustemitFunction>(null, cancellationToken);
        }

        public Task<BigInteger> OffsetQueryAsync(OffsetFunction offsetFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<OffsetFunction, BigInteger>(offsetFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> OffsetQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<OffsetFunction, BigInteger>(null, blockParameter);
        }

        public Task<BigInteger> XxxQueryAsync(XxxFunction xxxFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<XxxFunction, BigInteger>(xxxFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> XxxQueryAsync(BigInteger returnValue1, BlockParameter blockParameter = null)
        {
            var xxxFunction = new XxxFunction();
                xxxFunction.ReturnValue1 = returnValue1;
            
            return ContractHandler.QueryAsync<XxxFunction, BigInteger>(xxxFunction, blockParameter);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(CountFunction),
                typeof(CountersFunction),
                typeof(GasWasterFunction),
                typeof(JustemitFunction),
                typeof(OffsetFunction),
                typeof(XxxFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(CalledFromEventDTO)
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
