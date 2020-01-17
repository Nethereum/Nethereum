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
using Nethereum.Structs.StructSample.ContractDefinition;

namespace Nethereum.Structs.StructSample
{
    public partial class StructSampleService
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.Web3 web3, StructSampleDeployment structSampleDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<StructSampleDeployment>().SendRequestAndWaitForReceiptAsync(structSampleDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.Web3 web3, StructSampleDeployment structSampleDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<StructSampleDeployment>().SendRequestAsync(structSampleDeployment);
        }

        public static async Task<StructSampleService> DeployContractAndGetServiceAsync(Nethereum.Web3.Web3 web3, StructSampleDeployment structSampleDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, structSampleDeployment, cancellationTokenSource);
            return new StructSampleService(web3, receipt.ContractAddress);
        }

        protected Nethereum.Web3.Web3 Web3{ get; }

        public ContractHandler ContractHandler { get; }

        public StructSampleService(Nethereum.Web3.Web3 web3, string contractAddress)
        {
            Web3 = web3;
            ContractHandler = web3.Eth.GetContractHandler(contractAddress);
        }

        public Task<GetTestOutputDTO> GetTestQueryAsync(GetTestFunction getTestFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<GetTestFunction, GetTestOutputDTO>(getTestFunction, blockParameter);
        }

        public Task<GetTestOutputDTO> GetTestQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<GetTestFunction, GetTestOutputDTO>(null, blockParameter);
        }

        public Task<string> SetStorageStructRequestAsync(SetStorageStructFunction setStorageStructFunction)
        {
             return ContractHandler.SendRequestAsync(setStorageStructFunction);
        }

        public Task<TransactionReceipt> SetStorageStructRequestAndWaitForReceiptAsync(SetStorageStructFunction setStorageStructFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setStorageStructFunction, cancellationToken);
        }

        public Task<string> SetStorageStructRequestAsync(TestStruct testStruct)
        {
            var setStorageStructFunction = new SetStorageStructFunction();
                setStorageStructFunction.TestStruct = testStruct;
            
             return ContractHandler.SendRequestAsync(setStorageStructFunction);
        }

        public Task<TransactionReceipt> SetStorageStructRequestAndWaitForReceiptAsync(TestStruct testStruct, CancellationTokenSource cancellationToken = null)
        {
            var setStorageStructFunction = new SetStorageStructFunction();
                setStorageStructFunction.TestStruct = testStruct;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setStorageStructFunction, cancellationToken);
        }

        public Task<string> TestRequestAsync(TestFunction testFunction)
        {
             return ContractHandler.SendRequestAsync(testFunction);
        }

        public Task<TransactionReceipt> TestRequestAndWaitForReceiptAsync(TestFunction testFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(testFunction, cancellationToken);
        }

        public Task<string> TestRequestAsync(TestStruct testScrut)
        {
            var testFunction = new TestFunction();
                testFunction.TestScrut = testScrut;
            
             return ContractHandler.SendRequestAsync(testFunction);
        }

        public Task<TransactionReceipt> TestRequestAndWaitForReceiptAsync(TestStruct testScrut, CancellationTokenSource cancellationToken = null)
        {
            var testFunction = new TestFunction();
                testFunction.TestScrut = testScrut;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(testFunction, cancellationToken);
        }

        public Task<TestArrayOutputDTO> TestArrayQueryAsync(TestArrayFunction testArrayFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<TestArrayFunction, TestArrayOutputDTO>(testArrayFunction, blockParameter);
        }

        public Task<TestArrayOutputDTO> TestArrayQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<TestArrayFunction, TestArrayOutputDTO>(null, blockParameter);
        }

        public Task<BigInteger> Id1QueryAsync(Id1Function id1Function, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<Id1Function, BigInteger>(id1Function, blockParameter);
        }

        
        public Task<BigInteger> Id1QueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<Id1Function, BigInteger>(null, blockParameter);
        }

        public Task<BigInteger> Id2QueryAsync(Id2Function id2Function, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<Id2Function, BigInteger>(id2Function, blockParameter);
        }

        
        public Task<BigInteger> Id2QueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<Id2Function, BigInteger>(null, blockParameter);
        }

        public Task<BigInteger> Id3QueryAsync(Id3Function id3Function, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<Id3Function, BigInteger>(id3Function, blockParameter);
        }

        
        public Task<BigInteger> Id3QueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<Id3Function, BigInteger>(null, blockParameter);
        }

        public Task<string> Id4QueryAsync(Id4Function id4Function, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<Id4Function, string>(id4Function, blockParameter);
        }

        
        public Task<string> Id4QueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<Id4Function, string>(null, blockParameter);
        }

        public Task<TestStructStorageOutputDTO> TestStructStorageQueryAsync(TestStructStorageFunction testStructStorageFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<TestStructStorageFunction, TestStructStorageOutputDTO>(testStructStorageFunction, blockParameter);
        }

        public Task<TestStructStorageOutputDTO> TestStructStorageQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<TestStructStorageFunction, TestStructStorageOutputDTO>(null, blockParameter);
        }
    }
}
