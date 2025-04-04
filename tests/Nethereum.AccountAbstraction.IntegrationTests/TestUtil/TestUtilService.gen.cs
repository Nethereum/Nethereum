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
using Nethereum.AccountAbstraction.IntegrationTests.TestUtil.ContractDefinition;

namespace Nethereum.AccountAbstraction.IntegrationTests.TestUtil
{
    public partial class TestUtilService: TestUtilServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, TestUtilDeployment testUtilDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<TestUtilDeployment>().SendRequestAndWaitForReceiptAsync(testUtilDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, TestUtilDeployment testUtilDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<TestUtilDeployment>().SendRequestAsync(testUtilDeployment);
        }

        public static async Task<TestUtilService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, TestUtilDeployment testUtilDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, testUtilDeployment, cancellationTokenSource);
            return new TestUtilService(web3, receipt.ContractAddress);
        }

        public TestUtilService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class TestUtilServiceBase: ContractWeb3ServiceBase
    {

        public TestUtilServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public Task<byte[]> EncodeUserOpQueryAsync(EncodeUserOpFunction encodeUserOpFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<EncodeUserOpFunction, byte[]>(encodeUserOpFunction, blockParameter);
        }

        
        public virtual Task<byte[]> EncodeUserOpQueryAsync(PackedUserOperation op, BlockParameter blockParameter = null)
        {
            var encodeUserOpFunction = new EncodeUserOpFunction();
                encodeUserOpFunction.Op = op;
            
            return ContractHandler.QueryAsync<EncodeUserOpFunction, byte[]>(encodeUserOpFunction, blockParameter);
        }

        public Task<bool> IsEip7702InitCodeQueryAsync(IsEip7702InitCodeFunction isEip7702InitCodeFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<IsEip7702InitCodeFunction, bool>(isEip7702InitCodeFunction, blockParameter);
        }

        
        public virtual Task<bool> IsEip7702InitCodeQueryAsync(byte[] initCode, BlockParameter blockParameter = null)
        {
            var isEip7702InitCodeFunction = new IsEip7702InitCodeFunction();
                isEip7702InitCodeFunction.InitCode = initCode;
            
            return ContractHandler.QueryAsync<IsEip7702InitCodeFunction, bool>(isEip7702InitCodeFunction, blockParameter);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(EncodeUserOpFunction),
                typeof(IsEip7702InitCodeFunction)
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
